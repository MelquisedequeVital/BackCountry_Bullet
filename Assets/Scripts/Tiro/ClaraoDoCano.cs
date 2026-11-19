using UnityEngine;

/// <summary>
/// Mostra o clarão (muzzle flash) na ponta do cano a cada tiro.
/// Cria uma cópia do efeito uma única vez, presa na ponta do cano. A cada disparo,
/// solta as partículas do efeito na hora (Emit) e acende a luz por um instante.
/// Funciona inclusive com efeitos feitos para tiro contínuo (em loop), como os do WarFX,
/// porque o loop original do efeito fica desligado.
/// </summary>
public class ClaraoDoCano : MonoBehaviour, IReacaoAoTiro
{
    [Header("Efeito")]
    [Tooltip("Prefab do clarão (ex.: WarFX > _Effects > MuzzleFlashes > 4Planes > WFX_MF 4P RIFLE1)")]
    public GameObject prefabDoClarao;

    [Tooltip("Objeto vazio na ponta do cano da arma. A seta azul (Z) dele deve apontar para fora do cano.")]
    public Transform pontaDoCano;

    [Header("Ajustes")]
    [Tooltip("Tamanho do clarão (1 = tamanho original do efeito). Aplicado ao iniciar o jogo.")]
    [Range(0.1f, 3f)]
    public float escala = 1f;

    [Tooltip("Quantas partículas cada parte do efeito solta por tiro")]
    [Range(1, 5)]
    public int particulasPorTiro = 1;

    [Header("Luz")]
    [Tooltip("Acende a luz do efeito a cada tiro (ilumina a arma e o chão por um instante)")]
    public bool usarLuz = true;

    [Tooltip("Quanto tempo (em segundos) a luz fica acesa a cada tiro")]
    [Range(0.02f, 0.3f)]
    public float duracaoDaLuz = 0.05f;

    private ParticleSystem[] particulas;
    private Light[] luzes;
    private float apagarLuzEm;

    void Awake()
    {
        if (!ConfiguracaoValida()) return;

        GameObject clarao = CriarClarao();
        DesligarComportamentoOriginal(clarao);
    }

    public void AoAtirar()
    {
        if (particulas == null) return;

        SoltarParticulas();

        if (usarLuz)
            AcenderLuzes();
    }

    void Update()
    {
        if (luzes != null && Time.time >= apagarLuzEm)
            LigarLuzes(false);
    }

    // ---------- Configuração ----------

    private bool ConfiguracaoValida()
    {
        if (prefabDoClarao == null)
        {
            Debug.LogWarning("ClaraoDoCano: arraste um prefab no campo 'Prefab Do Clarao'.", this);
            return false;
        }

        if (pontaDoCano == null)
        {
            Debug.LogWarning("ClaraoDoCano: arraste o objeto da ponta do cano no campo 'Ponta Do Cano'.", this);
            return false;
        }

        return true;
    }

    // Cria o clarão como filho da ponta do cano, para ele acompanhar a arma
    private GameObject CriarClarao()
    {
        GameObject clarao = Instantiate(prefabDoClarao, pontaDoCano);
        clarao.transform.localPosition = Vector3.zero;
        clarao.transform.localRotation = Quaternion.identity;
        clarao.transform.localScale = Vector3.one * escala;

        particulas = clarao.GetComponentsInChildren<ParticleSystem>(true);
        luzes = clarao.GetComponentsInChildren<Light>(true);
        return clarao;
    }

    // Desliga o loop automático do efeito e os scripts dele (ex.: luz piscando sem parar).
    // A partir daqui, quem controla quando o clarão aparece é só este componente.
    private void DesligarComportamentoOriginal(GameObject clarao)
    {
        foreach (MonoBehaviour script in clarao.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled = false;

        foreach (ParticleSystem particula in particulas)
            DesligarEmissaoAutomatica(particula);

        LigarLuzes(false);
    }

    // Sem isso, toda vez que a arma é guardada e sacada de novo (Q), o Unity religa o efeito
    // sozinho (Play On Awake) e ele fica em loop para sempre.
    private static void DesligarEmissaoAutomatica(ParticleSystem particula)
    {
        particula.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule principal = particula.main;
        principal.playOnAwake = false;
        principal.loop = false;

        // O Emit() do tiro continua funcionando mesmo com a emissão automática desligada
        ParticleSystem.EmissionModule emissao = particula.emission;
        emissao.enabled = false;
    }

    // ---------- Disparo ----------

    // Emit solta as partículas na mesma hora, sem esperar o ritmo de emissão do efeito
    private void SoltarParticulas()
    {
        foreach (ParticleSystem particula in particulas)
            particula.Emit(particulasPorTiro);
    }

    private void AcenderLuzes()
    {
        LigarLuzes(true);
        apagarLuzEm = Time.time + duracaoDaLuz;
    }

    private void LigarLuzes(bool ligar)
    {
        foreach (Light luz in luzes)
            luz.enabled = ligar;
    }
}
