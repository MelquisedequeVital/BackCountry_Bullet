using System;
using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// O "cérebro" do fluxo do jogo: Jogando → Pausado → Morto → Vitória.
/// O menu (feito por outra pessoa) só precisa:
///  - ligar os BOTÕES nos métodos públicos (Continuar, TentarDeNovo, ReiniciarTudo, IrParaOMenuPrincipal, Sair);
///  - mostrar/esconder as TELAS nos eventos (Ao Pausar, Ao Continuar, Ao Morrer, Ao Vencer, Ao Tentar De Novo).
/// Este script já cuida de: pausar o tempo, soltar/prender o cursor, Esc para pausar,
/// travar os comandos do jogador enquanto o jogo está parado e renascer no último PontoDeRetorno.
/// Enquanto ninguém ligar telas nos eventos, ele mostra telas provisórias simples (texto + teclas) para testar.
/// Coloque UM por cena (prefab Prefabs/Fluxo/FluxoDoJogo).
/// </summary>
public class FluxoDoJogo : MonoBehaviour
{
    public enum Estado { Jogando, Pausado, Morto, Vitoria }

    [Header("Morte")]
    [Tooltip("Segundos entre o jogador cair e a tela de morte aparecer (tempo para a animação de morte)")]
    [Min(0f)] public float atrasoDaTelaDeMorte = 2.5f;
    [Tooltip("Ao tentar de novo, os inimigos que ainda estão vivos recuperam a vida toda")]
    public bool curarInimigosAoTentarDeNovo = true;

    [Header("Vitória")]
    [Tooltip("Quando esta conversa terminar, o jogador venceu (a conversa final do Coronel: NPCBoss_Depois)")]
    public Dialogo conversaDaVitoria;
    [Min(0f)] public float atrasoDaTelaDeVitoria = 1f;

    [Header("Pausa")]
    [Tooltip("Esc pausa e despausa o jogo")]
    public bool escPausa = true;

    [Header("Cenas")]
    [Tooltip("Nome da cena do menu principal (precisa estar na lista de cenas do Build Profiles)")]
    public string cenaDoMenuPrincipal = "MenuPrincipal";

    [Header("Telas provisórias (para testar sem o menu pronto)")]
    [Tooltip("Mostra textos simples na tela quando o evento correspondente não tem nenhuma tela ligada")]
    public bool usarTelasProvisorias = true;
    public string textoDaMorte = "Severino caiu... mas cangaceiro não desiste.";
    public string textoDaVitoria = "A água voltou pra vila.";

    [Header("Eventos (ligue aqui as telas do menu)")]
    public UnityEvent aoPausar;
    public UnityEvent aoContinuar;
    [Tooltip("A tela de morte deve aparecer aqui (botões: Tentar de novo, Menu principal)")]
    public UnityEvent aoMorrer;
    [Tooltip("Esconda a tela de morte aqui")]
    public UnityEvent aoTentarDeNovo;
    [Tooltip("A tela de vitória deve aparecer aqui (botões: Jogar de novo, Menu principal)")]
    public UnityEvent aoVencer;

    // ---------- Acesso para outros scripts ----------

    public static FluxoDoJogo Instancia { get; private set; }

    /// <summary>O jogo está pausado, com o jogador morto ou na vitória: os comandos do jogador não devem responder.</summary>
    public static bool JogoParado => Instancia != null && Instancia.EstadoAtual != Estado.Jogando;

    /// <summary>Disparado sempre que o estado muda (para quem preferir código em vez dos eventos do Inspector).</summary>
    public static event Action<Estado> AoMudarEstado;

    public Estado EstadoAtual { get; private set; } = Estado.Jogando;

    // ---------- Estado interno ----------

    private Vida vidaDoJogador;
    private MorteDoJogador morteDoJogador;
    private StarterAssetsInputs entradas;
    private PlayerInput entradaDoJogador;
    private Vector3 posicaoDeRetorno;
    private Quaternion rotacaoDeRetorno;
    private Text textoProvisorio;
    private GameObject telaProvisoria;

    // ---------- Ciclo de vida ----------

    // Rede de segurança: se a cena tem jogador mas ninguém colocou o FluxoDoJogo, cria um sozinho
    // (pausa, morte e "Tentar de novo" funcionam; a vitória precisa do prefab, que já vem com a conversa final ligada).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarSeFaltar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        GarantirNaCena();
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo) => GarantirNaCena();

    private static void GarantirNaCena()
    {
        if (FindAnyObjectByType<FluxoDoJogo>() != null || GameObject.FindGameObjectWithTag("Player") == null) return;
        Debug.LogWarning("FluxoDoJogo: não há um FluxoDoJogo nesta cena; criei um provisório. " +
                         "Arraste o prefab Prefabs/Fluxo/FluxoDoJogo para a cena (sem ele a tela de vitória não aparece).");
        new GameObject("FluxoDoJogo (criado sozinho)").AddComponent<FluxoDoJogo>();
    }


    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning("FluxoDoJogo: já existe um nesta cena; este foi desligado.", this);
            enabled = false;
            return;
        }
        Instancia = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        EncontrarJogador();
        if (vidaDoJogador != null)
        {
            // O primeiro ponto de retorno é onde o jogador começa
            posicaoDeRetorno = vidaDoJogador.transform.position;
            rotacaoDeRetorno = vidaDoJogador.transform.rotation;
            vidaDoJogador.AoMorrer += AoJogadorMorrer;
        }
        CaixaDeDialogo.AoTerminarConversa += AoTerminarConversa;

        // Vindo do menu, a janela já está com foco e o Starter Assets não prende o cursor sozinho
        // (ele só prende quando a janela ganha foco). Então prende aqui, no começo da cena.
        AplicarCursor(EstadoAtual == Estado.Jogando);
    }

    void OnDestroy()
    {
        if (Instancia != this) return;
        Instancia = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (vidaDoJogador != null) vidaDoJogador.AoMorrer -= AoJogadorMorrer;
        CaixaDeDialogo.AoTerminarConversa -= AoTerminarConversa;
    }

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

        if (escPausa && teclado.escapeKey.wasPressedThisFrame)
        {
            if (EstadoAtual == Estado.Jogando) Pausar();
            else if (EstadoAtual == Estado.Pausado) Continuar();
        }

        // Teclas das telas provisórias
        if (telaProvisoria == null || !telaProvisoria.activeSelf) return;
        if (EstadoAtual == Estado.Morto && teclado.rKey.wasPressedThisFrame) TentarDeNovo();
        if (EstadoAtual == Estado.Vitoria && teclado.rKey.wasPressedThisFrame) ReiniciarTudo();
        if (EstadoAtual != Estado.Jogando && teclado.mKey.wasPressedThisFrame) IrParaOMenuPrincipal();
    }

    // =====================================================================
    // Botões do menu (ligue no OnClick dos botões)
    // =====================================================================

    /// <summary>Pausa o jogo (para o tempo, solta o cursor).</summary>
    public void Pausar()
    {
        if (EstadoAtual != Estado.Jogando) return;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        MudarEstado(Estado.Pausado);
        Mostrar(aoPausar, "PAUSADO\n\n[Esc] Continuar    [M] Menu principal");
    }

    /// <summary>Volta ao jogo depois da pausa.</summary>
    public void Continuar()
    {
        if (EstadoAtual != Estado.Pausado) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        EsconderTelaProvisoria();
        MudarEstado(Estado.Jogando);
        aoContinuar?.Invoke();
    }

    /// <summary>Depois de morrer: o jogador renasce no último ponto de retorno, com a vida cheia.</summary>
    public void TentarDeNovo()
    {
        if (EstadoAtual != Estado.Morto || morteDoJogador == null) return;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        EsconderTelaProvisoria();
        if (curarInimigosAoTentarDeNovo) CurarInimigosVivos();
        morteDoJogador.Reviver(posicaoDeRetorno, rotacaoDeRetorno);

        MudarEstado(Estado.Jogando);
        aoTentarDeNovo?.Invoke();
    }

    /// <summary>Recomeça a cena atual do zero (tudo volta ao começo, inclusive as conversas).</summary>
    public void ReiniciarTudo()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Vai para a cena do menu principal.</summary>
    public void IrParaOMenuPrincipal()
    {
        if (!Application.CanStreamedLevelBeLoaded(cenaDoMenuPrincipal))
        {
            Debug.LogWarning($"FluxoDoJogo: a cena '{cenaDoMenuPrincipal}' não existe ou não está no Build Profiles " +
                             "(File → Build Profiles → Scene List).", this);
            return;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(cenaDoMenuPrincipal);
    }

    /// <summary>Fecha o jogo (no Editor, só para o Play).</summary>
    public void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =====================================================================
    // Ponto de retorno
    // =====================================================================

    /// <summary>Daqui em diante o jogador renasce neste lugar (chamado pelos PontoDeRetorno).</summary>
    public void DefinirPontoDeRetorno(Vector3 posicao, Quaternion rotacao)
    {
        posicaoDeRetorno = posicao;
        rotacaoDeRetorno = rotacao;
    }

    // =====================================================================
    // Morte e vitória
    // =====================================================================

    private void AoJogadorMorrer() => StartCoroutine(MostrarMorteDepois());

    private IEnumerator MostrarMorteDepois()
    {
        if (EstadoAtual == Estado.Pausado) Continuar();
        MudarEstado(Estado.Morto); // já trava os comandos; a tela vem depois da animação
        yield return new WaitForSecondsRealtime(atrasoDaTelaDeMorte);
        if (EstadoAtual != Estado.Morto) yield break;

        Mostrar(aoMorrer, $"{textoDaMorte}\n\n[R] Tentar de novo    [M] Menu principal");
    }

    private void AoTerminarConversa(Dialogo dialogo)
    {
        if (conversaDaVitoria != null && dialogo == conversaDaVitoria && EstadoAtual == Estado.Jogando)
            StartCoroutine(MostrarVitoriaDepois());
    }

    private IEnumerator MostrarVitoriaDepois()
    {
        MudarEstado(Estado.Vitoria);
        yield return new WaitForSecondsRealtime(atrasoDaTelaDeVitoria);
        Mostrar(aoVencer, $"{textoDaVitoria}\n\n[R] Jogar de novo    [M] Menu principal");
    }

    // =====================================================================
    // Ajuda
    // =====================================================================

    private void MudarEstado(Estado novo)
    {
        EstadoAtual = novo;
        bool jogando = novo == Estado.Jogando;

        AplicarCursor(jogando);
        if (entradaDoJogador != null && novo == Estado.Pausado) entradaDoJogador.DeactivateInput();
        if (entradaDoJogador != null && jogando) entradaDoJogador.ActivateInput();

        AoMudarEstado?.Invoke(novo);
    }

    // Cursor: preso no jogo, solto nas telas (para clicar nos botões)
    private void AplicarCursor(bool jogando)
    {
        Cursor.lockState = jogando ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !jogando;
        if (entradas != null)
        {
            entradas.cursorLocked = jogando;      // o Starter Assets não prende o cursor de novo sozinho
            entradas.cursorInputForLook = jogando; // a câmera não gira com o mouse nas telas
            if (!jogando) { entradas.LookInput(Vector2.zero); entradas.MoveInput(Vector2.zero); entradas.SprintInput(false); }
        }
    }

    // Chama o evento do menu; se ninguém ligou uma tela nele, mostra a tela provisória
    private void Mostrar(UnityEvent evento, string textoProvisorioDaTela)
    {
        bool temTelaLigada = evento != null && evento.GetPersistentEventCount() > 0;
        evento?.Invoke();
        if (!temTelaLigada && usarTelasProvisorias) MostrarTelaProvisoria(textoProvisorioDaTela);
    }

    private void CurarInimigosVivos()
    {
        foreach (InimigoBase inimigo in FindObjectsByType<InimigoBase>())
        {
            if (inimigo.TryGetComponent(out Vida vida) && vida.EstaVivo) vida.Curar(vida.vidaMaxima);
        }
    }

    private void EncontrarJogador()
    {
        GameObject comTag = GameObject.FindGameObjectWithTag("Player");
        if (comTag == null)
        {
            Debug.LogWarning("FluxoDoJogo: nenhum objeto com a tag 'Player' na cena.", this);
            return;
        }
        vidaDoJogador = comTag.GetComponentInParent<Vida>();
        if (vidaDoJogador == null) vidaDoJogador = comTag.GetComponentInChildren<Vida>();
        if (vidaDoJogador == null) return;

        morteDoJogador = vidaDoJogador.GetComponent<MorteDoJogador>();
        entradas = vidaDoJogador.GetComponentInChildren<StarterAssetsInputs>();
        entradaDoJogador = vidaDoJogador.GetComponentInChildren<PlayerInput>();
        if (morteDoJogador == null)
            Debug.LogWarning("FluxoDoJogo: o jogador não tem o componente MorteDoJogador; \"Tentar de novo\" não vai funcionar.", this);
    }

    // ---------- Tela provisória ----------

    private void MostrarTelaProvisoria(string texto)
    {
        if (telaProvisoria == null) MontarTelaProvisoria();
        textoProvisorio.text = texto;
        telaProvisoria.SetActive(true);
    }

    private void EsconderTelaProvisoria()
    {
        if (telaProvisoria != null) telaProvisoria.SetActive(false);
    }

    private void MontarTelaProvisoria()
    {
        telaProvisoria = new GameObject("Tela Provisoria", typeof(Canvas), typeof(CanvasScaler));
        telaProvisoria.transform.SetParent(transform, false);
        Canvas canvas = telaProvisoria.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler escalador = telaProvisoria.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        var fundo = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        fundo.transform.SetParent(telaProvisoria.transform, false);
        RectTransform rectFundo = (RectTransform)fundo.transform;
        rectFundo.anchorMin = Vector2.zero;
        rectFundo.anchorMax = Vector2.one;
        rectFundo.offsetMin = rectFundo.offsetMax = Vector2.zero;
        fundo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var objetoTexto = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(telaProvisoria.transform, false);
        RectTransform rectTexto = (RectTransform)objetoTexto.transform;
        rectTexto.anchorMin = Vector2.zero;
        rectTexto.anchorMax = Vector2.one;
        rectTexto.offsetMin = rectTexto.offsetMax = Vector2.zero;
        textoProvisorio = objetoTexto.GetComponent<Text>();
        textoProvisorio.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textoProvisorio.fontSize = 44;
        textoProvisorio.alignment = TextAnchor.MiddleCenter;
        textoProvisorio.color = Color.white;
        textoProvisorio.raycastTarget = false;
    }
}
