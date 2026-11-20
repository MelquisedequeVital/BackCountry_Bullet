using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mostra as conversas na tela e controla o fluxo delas:
///  - Conversa: caixa embaixo com o nome e o texto aparecendo letra por letra. E / Espaço / clique passa a fala.
///    Enquanto está aberta, o jogador fica parado (não anda, não atira, não esquiva, não saca a arma)
///    e a câmera vai para a diagonal mostrando o jogador e o NPC.
///  - Dica: caixa amarela no alto. Aparece quando a conversa termina e fica um tempo na tela, para o jogador fazer o que ela pede.
///  - Legenda: fala solta que não trava o jogador (ex.: o NPC chamando de longe).
///  - Aviso "[E] Falar com ...": aparece perto de um NPC com conversa disponível.
/// Não precisa colocar na cena: é criada sozinha na primeira vez que um NPC ou zona de diálogo precisar dela.
/// Se quiser mudar as configurações (nome do jogador, câmera, velocidade do texto), coloque este componente
/// num objeto da cena e ajuste lá: ele passa a ser usado no lugar do criado sozinho.
/// </summary>
public class CaixaDeDialogo : MonoBehaviour
{
    [Header("Texto")]
    [Tooltip("Velocidade em que o texto aparece")]
    [Min(1f)] public float letrasPorSegundo = 45f;
    [Tooltip("Nome que aparece nas falas do jogador")]
    public string nomeDoJogador = "Severino";
    public Color corDoNomeDoNpc = new Color(1f, 0.8f, 0.35f);
    public Color corDoNomeDoJogador = new Color(0.55f, 0.85f, 1f);

    [Header("Câmera da conversa")]
    [Tooltip("Quanto a câmera fica atrás do jogador (m)")] public float recuoDaCamera = 1.6f;
    [Tooltip("Quanto ela vai para o lado: é o que deixa a câmera na diagonal (m)")] public float ladoDaCamera = 1.1f;
    [Tooltip("Altura da câmera (m)")] public float alturaDaCamera = 1.75f;
    [Tooltip("Altura do ponto para onde a câmera olha, entre o jogador e o NPC (m)")] public float alturaDoOlhar = 1.45f;
    [Tooltip("Duração da transição entre a câmera normal e a da conversa (s)")] public float duracaoDaTransicao = 0.8f;

    [Header("Dicas")]
    [Tooltip("Por quanto tempo a dica continua na tela depois que a conversa acaba (s)")]
    [Min(1f)] public float tempoDaDicaDepoisDaConversa = 15f;

    [Header("Legenda (fala que não trava o jogador)")]
    [Min(0.5f)] public float tempoMinimoDaLegenda = 2.5f;
    [Min(0f)] public float segundosPorLetraDaLegenda = 0.05f;

    // ---------- Acesso ----------

    private static CaixaDeDialogo instancia;

    /// <summary>A caixa da cena (criada na hora, se ainda não existir).</summary>
    public static CaixaDeDialogo Instancia
    {
        get
        {
            if (instancia == null) instancia = FindAnyObjectByType<CaixaDeDialogo>();
            if (instancia == null) instancia = new GameObject("Caixa de Dialogo").AddComponent<CaixaDeDialogo>();
            return instancia;
        }
    }

    /// <summary>Tem uma conversa aberta (inclui o quadro em que ela fechou, para o clique que fechou não virar um tiro).</summary>
    public static bool EstaAberta =>
        instancia != null && (instancia.conversaAtual != null || instancia.quadroDoFechamento == Time.frameCount);

    /// <summary>Essa conversa já foi mostrada nesta cena?</summary>
    public static bool JaTocou(Dialogo dialogo) => instancia != null && instancia.jaTocados.Contains(dialogo);

    /// <summary>O jogador (o objeto que tem o controle de movimento). Pode ser nulo se não houver jogador.</summary>
    public Transform Jogador { get; private set; }

    /// <summary>O jogador está livre para começar uma conversa (não está caído, esquivando etc.).</summary>
    public bool JogadorPodeConversar =>
        Jogador != null && conversaAtual == null && (controleMovimento == null || !controleMovimento.MovimentoBloqueado);

    // ---------- Estado ----------

    private IControleMovimento controleMovimento;
    private CameraDeConversa cameraDeConversa;
    private readonly HashSet<Dialogo> jaTocados = new HashSet<Dialogo>();

    private Dialogo conversaAtual;
    private List<Dialogo.Fala> falas;
    private int indice;
    private NPC npcAtual;
    private Action aoTerminar;
    private string textoCompleto;
    private float letrasMostradas;
    private bool linhaCompleta;
    private int quadroDaAbertura, quadroDoFechamento = -1;

    private readonly List<string> dicas = new List<string>();           // as que estão na tela
    private readonly List<string> dicasDaConversa = new List<string>(); // guardadas até a conversa acabar
    private float fimDaDica = float.MaxValue;

    private readonly Queue<(string nome, bool ehOJogador, string texto)> legendas = new Queue<(string, bool, string)>();
    private float fimDaLegenda;

    private NPC npcDoAviso;

    // ---------- Tela ----------

    private GameObject caixa, painelDaDica;
    private Text textoDoNome, textoDaFala, textoDoIndicador, textoDaDica, textoDaLegenda, textoDoAviso;

    // ---------- Ciclo de vida ----------

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;

        EncontrarJogador();
        cameraDeConversa = new CameraDeConversa(recuoDaCamera, ladoDaCamera, alturaDaCamera, alturaDoOlhar, duracaoDaTransicao);
        MontarTela();
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    void Update()
    {
        if (conversaAtual != null)
        {
            AtualizarConversa();
            return;
        }

        AtualizarLegenda();
        AtualizarAvisoDoNpc();
        if (dicas.Count > 0 && Time.time >= fimDaDica) EsconderDica();
    }

    // ---------- Conversa ----------

    /// <param name="quemFala">O NPC da conversa: dá o nome das falas dele e a câmera enquadra ele.
    /// Pode ser nulo: aí a câmera não muda (e falas "NPC" ficam sem nome).</param>
    /// <param name="aoTerminar">Chamado quando a última fala passar.</param>
    public void Iniciar(Dialogo dialogo, NPC quemFala, Action aoTerminar)
    {
        if (dialogo == null || conversaAtual != null) return;

        conversaAtual = dialogo;
        falas = dialogo.FalasParaTocar();
        indice = -1;
        npcAtual = quemFala;
        this.aoTerminar = aoTerminar;
        quadroDaAbertura = Time.frameCount; // o E que abriu a conversa não pode pular a primeira fala
        jaTocados.Add(dialogo);

        legendas.Clear();
        textoDaLegenda.enabled = false;
        textoDoAviso.enabled = false;
        EsconderDica();            // dicas da conversa anterior saem
        dicasDaConversa.Clear();   // as desta conversa só aparecem quando ela terminar

        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = true;
        if (quemFala != null) cameraDeConversa.Ativar(Jogador, quemFala.transform);

        caixa.SetActive(true);
        ProximaFala();
    }

    private void AtualizarConversa()
    {
        VirarUmParaOOutro();

        if (!linhaCompleta)
        {
            letrasMostradas += letrasPorSegundo * Time.unscaledDeltaTime;
            int quantas = Mathf.Min(Mathf.FloorToInt(letrasMostradas), textoCompleto.Length);
            textoDaFala.text = textoCompleto.Substring(0, quantas);
            linhaCompleta = quantas >= textoCompleto.Length;
        }

        // O "[E]" pisca quando a fala terminou de aparecer
        textoDoIndicador.enabled = linhaCompleta && Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f;

        if (Time.frameCount == quadroDaAbertura || !ApertouParaAvancar()) return;

        if (!linhaCompleta) MostrarFalaInteira();
        else ProximaFala();
    }

    private void ProximaFala()
    {
        indice++;

        // Dicas não aparecem no meio da conversa: ficam guardadas e vão para a caixa amarela quando ela terminar
        while (indice < falas.Count && falas[indice].tipo == Dialogo.TipoDeFala.Dica)
        {
            dicasDaConversa.Add(falas[indice].texto);
            indice++;
        }

        if (indice >= falas.Count)
        {
            Terminar();
            return;
        }

        Dialogo.Fala fala = falas[indice];
        textoDoNome.text = NomeDe(fala, npcAtual).ToUpperInvariant();
        textoDoNome.color = fala.quem == Dialogo.Quem.Jogador ? corDoNomeDoJogador : corDoNomeDoNpc;
        textoCompleto = fala.texto ?? "";
        letrasMostradas = 0f;
        linhaCompleta = textoCompleto.Length == 0;
        textoDaFala.text = "";
    }

    private void MostrarFalaInteira()
    {
        textoDaFala.text = textoCompleto;
        linhaCompleta = true;
    }

    private void Terminar()
    {
        conversaAtual = null;
        quadroDoFechamento = Time.frameCount;
        caixa.SetActive(false);

        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = false;
        cameraDeConversa.Desativar();

        foreach (string dica in dicasDaConversa) AdicionarDica(dica);
        dicasDaConversa.Clear();
        fimDaDica = Time.time + tempoDaDicaDepoisDaConversa;

        Action callback = aoTerminar;
        aoTerminar = null;
        npcAtual = null;
        callback?.Invoke();
    }

    // O jogador olha para o NPC (e o NPC para o jogador, se ele puder virar)
    private void VirarUmParaOOutro()
    {
        if (npcAtual == null || Jogador == null) return;

        float passo = 540f * Time.deltaTime;
        Vector3 paraONpc = npcAtual.transform.position - Jogador.position;
        paraONpc.y = 0f;
        if (paraONpc.sqrMagnitude < 0.01f) return;

        Jogador.rotation = Quaternion.RotateTowards(Jogador.rotation, Quaternion.LookRotation(paraONpc), passo);
        if (npcAtual.virarParaOJogador)
            npcAtual.transform.rotation =
                Quaternion.RotateTowards(npcAtual.transform.rotation, Quaternion.LookRotation(-paraONpc), passo);
    }

    private static bool ApertouParaAvancar()
    {
        Keyboard teclado = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame ||
                                    teclado.enterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    // O nome de quem fala: o do NPC, o do jogador, ou o escrito na própria fala
    private string NomeDe(Dialogo.Fala fala, NPC npc)
    {
        switch (fala.quem)
        {
            case Dialogo.Quem.Jogador: return nomeDoJogador;
            case Dialogo.Quem.NPC: return npc != null ? npc.nome : "";
            default: return fala.personagem ?? "";
        }
    }

    // ---------- Aviso "[E] Falar com ..." ----------

    private void AtualizarAvisoDoNpc()
    {
        npcDoAviso = JogadorPodeConversar && legendas.Count == 0 ? NPC.MaisProximoDisponivel(Jogador.position) : null;
        textoDoAviso.enabled = npcDoAviso != null && !textoDaLegenda.enabled;
        if (npcDoAviso == null) return;

        textoDoAviso.text = $"[E] Falar com {npcDoAviso.nome}";
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            npcDoAviso.Conversar();
    }

    // ---------- Legenda ----------

    /// <summary>Mostra as falas como legenda, sem travar o jogador (dicas vão para a caixa amarela).</summary>
    /// <param name="quemFala">O NPC que dá o nome das falas "NPC" (pode ser nulo).</param>
    public void MostrarLegenda(Dialogo dialogo, NPC quemFala)
    {
        if (dialogo == null || conversaAtual != null) return;
        jaTocados.Add(dialogo);

        foreach (Dialogo.Fala fala in dialogo.FalasParaTocar())
        {
            if (fala.tipo == Dialogo.TipoDeFala.Dica)
            {
                AdicionarDica(fala.texto);
                fimDaDica = Time.time + tempoDaDicaDepoisDaConversa;
            }
            else legendas.Enqueue((NomeDe(fala, quemFala), fala.quem == Dialogo.Quem.Jogador, fala.texto));
        }
        fimDaLegenda = 0f; // mostra a primeira já
    }

    private void AtualizarLegenda()
    {
        if (Time.time < fimDaLegenda) return;

        if (legendas.Count == 0)
        {
            textoDaLegenda.enabled = false;
            return;
        }

        (string nome, bool ehOJogador, string texto) = legendas.Dequeue();
        string cor = ColorUtility.ToHtmlStringRGB(ehOJogador ? corDoNomeDoJogador : corDoNomeDoNpc);
        textoDaLegenda.text = string.IsNullOrEmpty(nome)
            ? texto
            : $"<color=#{cor}><b>{nome.ToUpperInvariant()}:</b></color> {texto}";
        textoDaLegenda.enabled = true;
        fimDaLegenda = Time.time + Mathf.Max(tempoMinimoDaLegenda, texto.Length * segundosPorLetraDaLegenda);
    }

    // ---------- Dicas ----------

    /// <summary>Acrescenta uma dica na caixa amarela (aceita &lt;b&gt;negrito&lt;/b&gt;).</summary>
    public void AdicionarDica(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        if (dicas.Count >= 3) dicas.RemoveAt(0);
        dicas.Add(texto);

        textoDaDica.text = "<b>DICA</b>\n" + string.Join("\n", dicas);
        painelDaDica.SetActive(true);
    }

    public void EsconderDica()
    {
        dicas.Clear();
        painelDaDica.SetActive(false);
        fimDaDica = float.MaxValue;
    }

    // ---------- Montagem ----------

    private void EncontrarJogador()
    {
        GameObject comTag = GameObject.FindGameObjectWithTag("Player");
        if (comTag == null)
        {
            Debug.LogWarning("CaixaDeDialogo: nenhum objeto com a tag 'Player' encontrado.", this);
            return;
        }

        // A tag pode estar num objeto de dentro do modelo: o jogador de verdade é quem tem o controle de movimento
        controleMovimento = comTag.GetComponentInParent<IControleMovimento>();
        Jogador = controleMovimento is Component componente ? componente.transform : comTag.transform;
    }

    private void MontarTela()
    {
        var raiz = new GameObject("Tela de Dialogo", typeof(Canvas), typeof(CanvasScaler));
        raiz.transform.SetParent(transform, false);
        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // por cima do HUD
        CanvasScaler escalador = raiz.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        // Caixa da conversa (embaixo, no centro)
        RectTransform fundo = CriarImagem("Caixa", raiz.transform, new Color(0f, 0f, 0f, 0.78f));
        Ancorar(fundo, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 210f));
        caixa = fundo.gameObject;

        textoDoNome = CriarTexto("Nome", fundo, 30, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        Ancorar(textoDoNome.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(1140f, 40f));

        textoDaFala = CriarTexto("Fala", fundo, 30, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Ancorar(textoDaFala.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -60f), new Vector2(1140f, 135f));

        textoDoIndicador = CriarTexto("Indicador", fundo, 24, FontStyle.Bold, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.8f));
        Ancorar(textoDoIndicador.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 12f), new Vector2(120f, 32f));
        textoDoIndicador.text = "[E]";
        caixa.SetActive(false);

        // Dica (caixa amarela no alto, cresce conforme o texto)
        RectTransform dica = CriarImagem("Dica", raiz.transform, new Color(1f, 0.82f, 0.25f, 0.92f));
        Ancorar(dica, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 0f));
        VerticalLayoutGroup layout = dica.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 14, 16);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        dica.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        textoDaDica = CriarTexto("Texto", dica, 26, FontStyle.Normal, TextAnchor.UpperCenter, new Color(0.12f, 0.09f, 0.04f));
        textoDaDica.GetComponent<Outline>().enabled = false;
        painelDaDica = dica.gameObject;
        painelDaDica.SetActive(false);

        // Legenda (fala solta, embaixo)
        textoDaLegenda = CriarTexto("Legenda", raiz.transform, 30, FontStyle.Normal, TextAnchor.LowerCenter, Color.white);
        Ancorar(textoDaLegenda.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1400f, 120f));
        textoDaLegenda.enabled = false;

        // Aviso "[E] Falar com ..."
        textoDoAviso = CriarTexto("Aviso", raiz.transform, 28, FontStyle.Bold, TextAnchor.LowerCenter, Color.white);
        Ancorar(textoDoAviso.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 50f));
        textoDoAviso.enabled = false;
    }

    private static RectTransform CriarImagem(string nome, Transform pai, Color cor)
    {
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(pai, false);
        Image imagem = objeto.GetComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false;
        return (RectTransform)objeto.transform;
    }

    private static Text CriarTexto(string nome, Transform pai, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor)
    {
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(Text));
        objeto.transform.SetParent(pai, false);
        Text texto = objeto.GetComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.supportRichText = true;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.raycastTarget = false;

        Outline contorno = objeto.AddComponent<Outline>(); // legível em qualquer fundo
        contorno.effectColor = new Color(0f, 0f, 0f, 0.8f);
        contorno.effectDistance = new Vector2(2f, -2f);
        return texto;
    }

    private static void Ancorar(RectTransform rect, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = ancora;
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
    }
}
