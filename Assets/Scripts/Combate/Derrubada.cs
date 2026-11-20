using StarterAssets;
using UnityEngine;

/// <summary>Algo que pode ser derrubado por um golpe forte (ex.: a investida dos inimigos).</summary>
public interface IDerrubavel
{
    bool EstaDerrubado { get; }

    /// <param name="direcao">Para onde o golpe empurra (o jogador cai para esse lado, de costas para quem bateu).</param>
    void Derrubar(Vector3 direcao);
}

/// <summary>
/// Derrubada do jogador: empurrão para trás → animação de queda → no chão → animação de levantar → volta ao controle.
/// Enquanto está derrubado: não anda, não pula, não atira, não esquiva, a arma some da mão e fica invencível
/// (para não apanhar caído). As animações são tocadas por nome (CrossFade): basta criar os estados no Animator.
/// Coloque no objeto do jogador (o mesmo da Vida e do ThirdPersonController).
/// </summary>
[RequireComponent(typeof(Animator), typeof(CharacterController))]
public class Derrubada : MonoBehaviour, IDerrubavel, IFonteDeInvencibilidade, IPermissaoDeTiro
{
    private enum Fase { EmPe, Caindo, Levantando }

    [Header("Empurrão (acompanha a animação de queda)")]
    [Tooltip("Quantos metros o jogador é arremessado para trás no total")]
    [Min(0f)] public float distanciaDoEmpurrao = 7f;
    [Tooltip("Quanto do empurrão já aconteceu (0 a 1) em cada ponto da animação de queda. " +
             "Baseado na 'Falling Back Death': quase tudo enquanto o corpo voa (0.15 a 0.36), o resto escorregando no chão.")]
    public AnimationCurve curvaDoEmpurrao = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.15f, 0f), new Keyframe(0.22f, 0.35f), new Keyframe(0.29f, 0.65f),
        new Keyframe(0.36f, 0.85f), new Keyframe(0.44f, 0.96f), new Keyframe(0.51f, 1f), new Keyframe(1f, 1f));

    [Header("Queda")]
    [Tooltip("Nome do estado da queda no Animator (Base Layer)")]
    public string estadoDaQueda = "Derrubado";
    [Tooltip("De que ponto (0 a 1) a queda começa. A 'Falling Back Death' tropeça no lugar até ~0.18: " +
             "começar em 0.15 faz o corpo voar para trás logo no impacto.")]
    [Range(0f, 0.5f)] public float inicioDaQueda = 0.15f;
    [Tooltip("Velocidade da animação de queda (1 = original)")]
    [Range(0.2f, 3f)] public float velocidadeDaQueda = 1f;
    [Tooltip("Em que ponto da queda (0 a 1) começa a levantar. O corpo chega ao chão em ~0.36 e para de " +
             "escorregar em ~0.51; em 0.95 ele fica cerca de 1,5 s deitado.")]
    [Range(0f, 1f)] public float pontoParaLevantar = 0.95f;

    [Header("Levantar")]
    [Tooltip("Nome do estado de levantar no Animator. Se não existir, ele volta direto para o andar.")]
    public string estadoDoLevantar = "Levantando";
    [Tooltip("De que ponto (0 a 1) a animação de levantar começa (pula o começo parado, se houver). " +
             "No 'Kip Up' os primeiros ~15% são o personagem parado no chão.")]
    [Range(0f, 0.9f)] public float inicioDoLevantar = 0.1f;
    [Tooltip("Velocidade da animação de levantar (1 = original)")]
    [Range(0.2f, 4f)] public float velocidadeDoLevantar = 1.5f;
    [Tooltip("Em que ponto do levantar (0 a 1) o jogador volta a ter o controle")]
    [Range(0.3f, 1f)] public float fimDoLevantar = 0.9f;

    [Header("Geral")]
    [Tooltip("Estado para onde volta ao terminar (o andar do Starter Assets)")]
    public string estadoDeLocomocao = "Idle Walk Run Blend";
    [Tooltip("Fica invencível enquanto está derrubado (recomendado: não apanha caído)")]
    public bool invencivelEnquantoCaido = true;

    public bool EstaDerrubado => fase != Fase.EmPe;
    public bool EstaInvencivel => invencivelEnquantoCaido && EstaDerrubado;
    public bool PodeAtirar => !EstaDerrubado;

    private Animator animator;
    private CharacterController characterController;
    private IControleMovimento controleMovimento;
    private Esquiva esquiva;
    private Vida vida;

    private Fase fase = Fase.EmPe;
    private Vector3 direcaoDoEmpurrao;
    private float tempoNaFase;
    private float fracaoInicialDoEmpurrao; // quanto da curva já "passou" por começar a queda adiantada
    private float fracaoJaEmpurrada;      // 0 a 1 do empurrão total já aplicado
    private int hashQueda, hashLevantar, hashLocomocao;
    private bool levantarComecou;

    void Awake()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        controleMovimento = GetComponent<IControleMovimento>();
        esquiva = GetComponent<Esquiva>();
        vida = GetComponent<Vida>();

        hashQueda = Animator.StringToHash(estadoDaQueda);
        hashLevantar = Animator.StringToHash(estadoDoLevantar);
        hashLocomocao = Animator.StringToHash(estadoDeLocomocao);

        if (!animator.HasState(0, hashQueda))
            Debug.LogWarning($"Derrubada: estado '{estadoDaQueda}' não encontrado na Base Layer do Animator.", this);
        if (!animator.HasState(0, hashLevantar))
            Debug.LogWarning($"Derrubada: estado '{estadoDoLevantar}' não encontrado na Base Layer do Animator; " +
                             "o personagem vai voltar direto para o andar, sem animação de levantar.", this);
    }

    // ---------- Derrubar ----------

    public void Derrubar(Vector3 direcao)
    {
        if (EstaDerrubado || (vida != null && !vida.EstaVivo)) return;

        direcao.y = 0f;
        direcaoDoEmpurrao = direcao.sqrMagnitude > 0.001f ? direcao.normalized : -transform.forward;

        // Cai de costas: vira de frente para quem bateu (o empurrão vai para trás dele)
        transform.rotation = Quaternion.LookRotation(-direcaoDoEmpurrao);

        esquiva?.Cancelar(); // um golpe no meio da esquiva armada interrompe a esquiva
        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = true;

        animator.speed = velocidadeDaQueda;
        animator.CrossFade(hashQueda, 0.08f, 0, inicioDaQueda); // já começa no "voo" para trás
        fracaoInicialDoEmpurrao = curvaDoEmpurrao.Evaluate(inicioDaQueda);
        fracaoJaEmpurrada = 0f;

        fase = Fase.Caindo;
        tempoNaFase = 0f;
    }

    void Update()
    {
        if (fase == Fase.EmPe) return;
        tempoNaFase += Time.deltaTime;

        if (fase == Fase.Caindo) AtualizarQueda();
        else AtualizarLevantar();
    }

    // ---------- Fases ----------

    private void AtualizarQueda()
    {
        float progresso = Progresso(hashQueda, out bool noEstado);
        if (noEstado) Empurrar(progresso);

        // Morreu com o golpe: fica no chão (não levanta)
        if (vida != null && !vida.EstaVivo) return;

        bool podeLevantar = (noEstado && progresso >= pontoParaLevantar) || tempoNaFase > 6f;
        if (podeLevantar) ComecarALevantar();
    }

    // O corpo é empurrado junto com a animação: voa para trás quando o boneco voa e escorrega quando ele escorrega
    private void Empurrar(float progresso)
    {
        float restante = Mathf.Max(1f - fracaoInicialDoEmpurrao, 0.0001f);
        float desejado = Mathf.Clamp01((curvaDoEmpurrao.Evaluate(progresso) - fracaoInicialDoEmpurrao) / restante);
        float passo = desejado - fracaoJaEmpurrada;
        if (passo <= 0f) return;

        fracaoJaEmpurrada = desejado;
        characterController.Move(direcaoDoEmpurrao * (passo * distanciaDoEmpurrao));
    }

    private void ComecarALevantar()
    {
        fase = Fase.Levantando;
        tempoNaFase = 0f;
        levantarComecou = false;

        if (!animator.HasState(0, hashLevantar))
        {
            Terminar(); // sem animação de levantar: volta direto para o andar
            return;
        }

        animator.speed = velocidadeDoLevantar;
        animator.CrossFade(hashLevantar, 0.25f, 0, inicioDoLevantar);
    }

    private void AtualizarLevantar()
    {
        float progresso = Progresso(hashLevantar, out bool noEstado);
        if (noEstado) levantarComecou = true;

        bool terminou = (levantarComecou && (!noEstado || progresso >= fimDoLevantar)) || tempoNaFase > 8f;
        if (terminou) Terminar();
    }

    private void Terminar()
    {
        fase = Fase.EmPe;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(hashLocomocao, 0.25f, 0);
        if (controleMovimento != null) controleMovimento.MovimentoBloqueado = false;
    }

    private float Progresso(int hash, out bool noEstado)
    {
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);
        noEstado = info.shortNameHash == hash;
        return noEstado ? Mathf.Clamp01(info.normalizedTime) : 0f;
    }
}
