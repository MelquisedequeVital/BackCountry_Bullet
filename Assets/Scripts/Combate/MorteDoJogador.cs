using StarterAssets;
using UnityEngine;

/// <summary>
/// O que acontece quando a vida do jogador chega a zero:
///  - Armado: toca a animação de morte com o rifle.
///  - Desarmado: toca a mesma queda de costas usada quando o boss arremessa o jogador.
///  - Se já estava caído (derrubado), só fica no chão.
/// Em todos os casos o jogador para de responder: não anda, não pula, não atira, não esquiva, não saca a arma,
/// e a mira some. As animações são tocadas por nome (CrossFade): basta ter os estados na Base Layer do Animator.
/// Também sabe trazer o jogador de volta (Reviver): é o que o FluxoDoJogo usa no "Tentar de novo".
/// Coloque no objeto do jogador (o mesmo da Vida e do ThirdPersonController).
/// </summary>
[RequireComponent(typeof(Vida), typeof(Animator))]
public class MorteDoJogador : MonoBehaviour
{
    [Header("Morte armado")]
    [Tooltip("Nome do estado de morte com o rifle no Animator (Base Layer)")]
    public string estadoMorteArmado = "MorteArmado";

    [Header("Morte desarmado")]
    [Tooltip("Nome do estado da queda de costas (o mesmo do arremesso do boss)")]
    public string estadoMorteDesarmado = "Derrubado";
    [Tooltip("De que ponto (0 a 1) a queda começa. 0 = com o tropeço do começo; " +
             "0.15 = já caindo, igual ao arremesso do boss")]
    [Range(0f, 0.5f)] public float inicioDaMorteDesarmado = 0f;

    [Header("Geral")]
    [Tooltip("Duração da transição para a animação de morte (fração da animação, 0.15 = 15%)")]
    [Range(0f, 0.5f)] public float transicao = 0.15f;
    [Tooltip("Estado para onde o jogador volta ao reviver (o andar do Starter Assets)")]
    public string estadoDeLocomocao = "Idle Walk Run Blend";

    private Vida vida;
    private Animator animator;
    private IControleMovimento controleMovimento;
    private IEstadoArma estadoArma;
    private Esquiva esquiva;
    private IDerrubavel derrubavel;
    private readonly System.Collections.Generic.List<Behaviour> desligados = new System.Collections.Generic.List<Behaviour>();

    void Awake()
    {
        vida = GetComponent<Vida>();
        animator = GetComponent<Animator>();
        controleMovimento = GetComponent<IControleMovimento>();
        estadoArma = GetComponent<IEstadoArma>();
        esquiva = GetComponent<Esquiva>();
        derrubavel = GetComponent<IDerrubavel>();

        AvisarSeFaltarEstado(estadoMorteArmado);
        AvisarSeFaltarEstado(estadoMorteDesarmado);
    }

    void OnEnable() => vida.AoMorrer += Morrer;
    void OnDisable() => vida.AoMorrer -= Morrer;

    private void Morrer()
    {
        bool estavaArmado = estadoArma != null && estadoArma.EstaArmado;
        bool jaEstavaCaido = derrubavel != null && derrubavel.EstaDerrubado;

        esquiva?.Cancelar(); // morreu no meio de uma esquiva armada: ela para ali
        DesligarControles();
        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = true;

        if (jaEstavaCaido) return; // a derrubada já deixa o corpo no chão

        // A animação de morte é do corpo inteiro: tira o peso das camadas de cima (ex.: braços segurando a arma)
        for (int camada = 1; camada < animator.layerCount; camada++)
            animator.SetLayerWeight(camada, 0f);
        animator.speed = 1f;

        if (estavaArmado) Tocar(estadoMorteArmado, 0f);
        else Tocar(estadoMorteDesarmado, inicioDaMorteDesarmado);
    }

    /// <summary>
    /// Traz o jogador de volta à vida neste lugar: vida cheia, controles de volta, animação normal.
    /// Quem decide quando e onde é o FluxoDoJogo (botão "Tentar de novo").
    /// </summary>
    public void Reviver(Vector3 posicao, Quaternion rotacao)
    {
        if (derrubavel is Derrubada derrubada) derrubada.Resetar();

        // O CharacterController desfaz teleportes enquanto está ligado: desliga, move e religa
        if (TryGetComponent(out CharacterController controlador)) controlador.enabled = false;
        transform.SetPositionAndRotation(posicao, rotacao);
        if (controlador != null) controlador.enabled = true;

        vida.Reviver();

        foreach (Behaviour componente in desligados)
        {
            if (componente != null) componente.enabled = true;
        }
        desligados.Clear();

        // A mira volta a aparecer se ele ainda estiver com a arma na mão
        if (TryGetComponent(out ControladorMira controladorMira) && controladorMira.imagemMira != null)
            controladorMira.imagemMira.SetActive(estadoArma != null && estadoArma.EstaArmado);

        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = false;
        animator.speed = 1f;
        int locomocao = Animator.StringToHash(estadoDeLocomocao);
        if (animator.HasState(0, locomocao)) animator.Play(locomocao, 0, 0f);
    }

    private void Tocar(string estado, float inicio)
    {
        int hash = Animator.StringToHash(estado);
        if (animator.HasState(0, hash))
            animator.CrossFade(hash, transicao, 0, inicio); // "inicio" é o ponto (0 a 1) da animação
    }

    // Desliga tudo que lê os comandos do jogador ou mexe no corpo por cima da animação
    private void DesligarControles()
    {
        Desligar<ArmaTiro>();
        Desligar<EquiparArma>();
        Desligar<GirarComMouse>();
        Desligar<ApontarArmaParaMira>();
        Desligar<Esquiva>();

        if (TryGetComponent(out ControladorMira controladorMira) && controladorMira.imagemMira != null)
            controladorMira.imagemMira.SetActive(false);
        Desligar<ControladorMira>();
    }

    private void Desligar<T>() where T : Behaviour
    {
        if (TryGetComponent(out T componente) && componente.enabled)
        {
            componente.enabled = false;
            desligados.Add(componente); // para religar se o jogador reviver
        }
    }

    private void AvisarSeFaltarEstado(string estado)
    {
        if (!animator.HasState(0, Animator.StringToHash(estado)))
            Debug.LogWarning($"MorteDoJogador: estado '{estado}' não encontrado na Base Layer do Animator.", this);
    }
}
