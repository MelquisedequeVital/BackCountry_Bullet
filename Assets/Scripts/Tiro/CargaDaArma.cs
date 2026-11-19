using UnityEngine;

/// <summary>
/// Qualquer componente que pode impedir o tiro por um tempo (ex.: arma recarregando).
/// O ArmaTiro pergunta a todos antes de disparar.
/// </summary>
public interface IPermissaoDeTiro
{
    bool PodeAtirar { get; }
}

/// <summary>Informações da carga da arma, para a interface (HUD) mostrar.</summary>
public interface IMunicao
{
    int TirosRestantes { get; }
    int Capacidade { get; }
    bool Recarregando { get; }

    /// <summary>De 0 (começou a recarregar) a 1 (pronto).</summary>
    float ProgressoDaRecarga { get; }
}

/// <summary>
/// Limite de tiros seguidos: depois de "Tiros Por Carga" disparos, a arma recarrega sozinha.
/// Também recarrega se o jogador ficar um tempo sem atirar com a carga incompleta.
/// A recarga só anda com a arma NA MÃO: guardando a arma ela pausa, e continua ao sacar de novo.
/// (Por enquanto sem munição total; é a base para munição e recarga na tecla R no futuro.)
/// </summary>
public class CargaDaArma : MonoBehaviour, IReacaoAoTiro, IPermissaoDeTiro, IMunicao
{
    [Header("Carga")]
    [Tooltip("Quantos tiros seguidos antes de precisar recarregar")]
    [Min(1)]
    public int tirosPorCarga = 5;

    [Tooltip("Tempo da recarga (segundos)")]
    [Min(0f)]
    public float tempoDeRecarga = 1.5f;

    [Tooltip("Recarrega sozinho depois de tantos segundos sem atirar, se a carga não estiver cheia (0 = nunca)")]
    [Min(0f)]
    public float recarregarSozinhoApos = 2f;

    [Header("Som (opcional)")]
    [Tooltip("Som tocado ao começar a recarga (ex.: sfx_wpn_ar_reload_01_magOut)")]
    public AudioClip somAoComecar;

    [Tooltip("Som tocado ao terminar a recarga (ex.: sfx_wpn_ar_reload_03_magIn)")]
    public AudioClip somAoTerminar;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    public int TirosRestantes { get; private set; }
    public int Capacidade => tirosPorCarga;
    public bool Recarregando { get; private set; }
    public float ProgressoDaRecarga =>
        Recarregando && tempoDeRecarga > 0f ? Mathf.Clamp01(tempoJaRecarregado / tempoDeRecarga) : 1f;

    public bool PodeAtirar => !Recarregando && TirosRestantes > 0;

    private IEstadoArma estadoArma;
    private float tempoJaRecarregado;   // só aumenta com a arma na mão
    private float tempoSemAtirar;       // só conta com a arma na mão
    private bool somDeInicioTocado;
    private AudioSource fonte;

    void Awake()
    {
        TirosRestantes = tirosPorCarga;
        estadoArma = GetComponent<IEstadoArma>();
    }

    void Update()
    {
        if (!EstaArmado()) return; // arma guardada: nada anda (a recarga fica pausada)

        if (Recarregando)
            AvancarRecarga(Time.deltaTime);
        else
            ContarTempoSemAtirar(Time.deltaTime);
    }

    public void AoAtirar()
    {
        TirosRestantes = Mathf.Max(0, TirosRestantes - 1);
        tempoSemAtirar = 0f;

        if (TirosRestantes == 0)
            IniciarRecarga();
    }

    // Sem o componente de estado da arma, considera sempre armado (a recarga funciona como antes)
    private bool EstaArmado()
    {
        return estadoArma == null || estadoArma.EstaArmado;
    }

    private void ContarTempoSemAtirar(float deltaTime)
    {
        if (recarregarSozinhoApos <= 0f || TirosRestantes >= tirosPorCarga) return;

        tempoSemAtirar += deltaTime;
        if (tempoSemAtirar >= recarregarSozinhoApos)
            IniciarRecarga();
    }

    private void IniciarRecarga()
    {
        Recarregando = true;
        tempoJaRecarregado = 0f;
        somDeInicioTocado = false;
    }

    private void AvancarRecarga(float deltaTime)
    {
        // O som de "tirar o pente" toca quando a recarga realmente começa (com a arma na mão)
        if (!somDeInicioTocado)
        {
            TocarSom(somAoComecar);
            somDeInicioTocado = true;
        }

        tempoJaRecarregado += deltaTime;
        if (tempoJaRecarregado >= tempoDeRecarga)
            TerminarRecarga();
    }

    private void TerminarRecarga()
    {
        Recarregando = false;
        TirosRestantes = tirosPorCarga;
        tempoSemAtirar = 0f;
        TocarSom(somAoTerminar);
    }

    private void TocarSom(AudioClip som)
    {
        if (som == null) return;

        if (fonte == null)
        {
            fonte = gameObject.AddComponent<AudioSource>();
            fonte.playOnAwake = false;
            fonte.spatialBlend = 0f;
        }
        fonte.PlayOneShot(som, volume);
    }
}
