using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Telas de pausa, morte e vitória (e as Configurações da pausa), separadas do prefab FluxoDoJogo.
/// Aparecem sozinhas nas cenas de jogo (as que têm o Player), carregadas de Resources/TelasDoJogo,
/// e se ligam nos eventos do FluxoDoJogo da cena quando o jogo começa, desligando as telas provisórias dele.
/// Os botões chamam os métodos daqui, que repassam para o FluxoDoJogo.
/// </summary>
public class TelasDoJogo : MonoBehaviour
{
    [Header("Telas")]
    public GameObject telaDePausa;
    public GameObject telaDeMorte;
    public GameObject telaDeVitoria;
    public MenuConfiguracoes configuracoes;

    private FluxoDoJogo fluxo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarNasCenasDeJogo()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        GarantirNaCena();
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo) => GarantirNaCena();

    // Mesmo critério do FluxoDoJogo: cena de jogo é a que tem o Player
    private static void GarantirNaCena()
    {
        if (FindAnyObjectByType<TelasDoJogo>() != null || GameObject.FindGameObjectWithTag("Player") == null) return;

        var prefab = Resources.Load<GameObject>("TelasDoJogo");
        if (prefab == null)
        {
            Debug.LogWarning("TelasDoJogo: prefab Resources/TelasDoJogo não encontrado; as telas provisórias continuam.");
            return;
        }
        Instantiate(prefab).name = "TelasDoJogo";
    }

    // O FluxoDoJogo pode ser criado depois desta tela, então tenta até achar
    void Update()
    {
        if (fluxo == null) Conectar();
    }

    void OnDestroy()
    {
        if (fluxo == null) return;
        fluxo.aoPausar.RemoveListener(MostrarPausa);
        fluxo.aoContinuar.RemoveListener(EsconderPausa);
        fluxo.aoMorrer.RemoveListener(MostrarMorte);
        fluxo.aoTentarDeNovo.RemoveListener(EsconderMorte);
        fluxo.aoVencer.RemoveListener(MostrarVitoria);
    }

    void Conectar()
    {
        fluxo = FluxoDoJogo.Instancia != null ? FluxoDoJogo.Instancia : FindAnyObjectByType<FluxoDoJogo>();
        if (fluxo == null) return;

        // O FluxoDoJogo criado sozinho (cena sem o prefab) vem com os eventos vazios
        fluxo.aoPausar ??= new UnityEvent();
        fluxo.aoContinuar ??= new UnityEvent();
        fluxo.aoMorrer ??= new UnityEvent();
        fluxo.aoTentarDeNovo ??= new UnityEvent();
        fluxo.aoVencer ??= new UnityEvent();

        fluxo.usarTelasProvisorias = false;
        fluxo.aoPausar.AddListener(MostrarPausa);
        fluxo.aoContinuar.AddListener(EsconderPausa);
        fluxo.aoMorrer.AddListener(MostrarMorte);
        fluxo.aoTentarDeNovo.AddListener(EsconderMorte);
        fluxo.aoVencer.AddListener(MostrarVitoria);
    }

    void MostrarPausa() => telaDePausa.SetActive(true);
    void MostrarMorte() => telaDeMorte.SetActive(true);
    void EsconderMorte() => telaDeMorte.SetActive(false);
    void MostrarVitoria() => telaDeVitoria.SetActive(true);

    // Esc com as Configurações abertas fecha as duas telas
    void EsconderPausa()
    {
        telaDePausa.SetActive(false);
        if (configuracoes != null && configuracoes.gameObject.activeSelf) configuracoes.Fechar();
    }

    // ---------- Botões ----------

    public void Continuar() { if (fluxo != null) fluxo.Continuar(); }
    public void TentarDeNovo() { if (fluxo != null) fluxo.TentarDeNovo(); }
    public void ReiniciarTudo() { if (fluxo != null) fluxo.ReiniciarTudo(); }
    public void IrParaOMenuPrincipal() { if (fluxo != null) fluxo.IrParaOMenuPrincipal(); }
    public void Sair() { if (fluxo != null) fluxo.Sair(); }
}
