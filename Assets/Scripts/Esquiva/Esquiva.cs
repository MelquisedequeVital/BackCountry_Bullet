using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Qual esquiva está acontecendo agora.</summary>
public enum TipoDeEsquiva
{
    Nenhuma,
    Mergulho,   // desarmado: mergulho para frente
    Armada      // armado: esquiva mantendo a pose de mira (frente, trás, esquerda, direita)
}

/// <summary>
/// Informa se o personagem está no meio de uma esquiva, e qual.
/// Outros sistemas consultam isto para se comportar durante a esquiva
/// (ex.: não atirar, esconder a arma). No futuro, também serve para dar invencibilidade.
/// </summary>
public interface IEstadoEsquiva
{
    bool EstaEsquivando { get; }
    TipoDeEsquiva TipoAtual { get; }
}

/// <summary>
/// Esquiva no botão direito do mouse, na direção em que o jogador está andando.
/// - Desarmado (mergulho): vira o personagem para a direção e mergulha para frente.
/// - Armado: continua olhando para a mira e escolhe a animação pela direção
///   (frente, trás, esquerda ou direita).
/// Durante a esquiva, este script move o personagem e o ThirdPersonController fica bloqueado.
/// </summary>
[RequireComponent(typeof(Animator), typeof(CharacterController))]
public class Esquiva : MonoBehaviour, IEstadoEsquiva, IFonteDeInvencibilidade
{
    [Header("Invencibilidade (só no mergulho desarmado)")]
    [Tooltip("Em que ponto do mergulho (0 a 1) o personagem fica invencível")]
    [Range(0f, 1f)]
    public float inicioDaInvencibilidade = 0.05f;

    [Tooltip("Em que ponto do mergulho (0 a 1) a invencibilidade acaba")]
    [Range(0f, 1f)]
    public float fimDaInvencibilidade = 0.6f;

    [Header("Distância")]
    [Tooltip("Quantos metros o personagem percorre no mergulho")]
    [Min(0f)]
    public float distanciaDesarmado = 3.6f;

    [Tooltip("Quantos metros o personagem percorre na esquiva armada curta")]
    [Min(0f)]
    public float distanciaArmado = 2.5f;

    [Header("Rapidez")]
    [Tooltip("Acelera a animação do mergulho (1 = velocidade original, 2 = duas vezes mais rápido)")]
    [Range(0.5f, 3f)]
    public float velocidadeDesarmado = 1.8f;

    [Tooltip("Acelera as esquivas armadas curtas (1 = velocidade original, 2 = duas vezes mais rápido)")]
    [Range(0.5f, 3f)]
    public float velocidadeArmado = 1.4f;

    [Header("Tempo")]
    [Tooltip("Velocidade ao longo da esquiva (0 = início, 1 = fim da animação). Rápido no começo e parado no final.")]
    public AnimationCurve curvaDeVelocidade = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.4f, 0.8f), new Keyframe(0.65f, 0f));

    [Tooltip("Em que ponto da animação (0 a 1) o jogador volta a ter o controle. Menor = esquiva mais ágil.")]
    [Range(0.3f, 1f)]
    public float fimDaEsquiva = 0.7f;

    [Header("Mergulho (desarmado)")]
    [Tooltip("Velocidade ao longo do mergulho. Copiada da própria animação: acelera, rola e só para perto do fim.")]
    public AnimationCurve curvaDoMergulho = new AnimationCurve(
        new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(0.45f, 0.85f),
        new Keyframe(0.6f, 0.6f), new Keyframe(0.8f, 0.4f), new Keyframe(0.92f, 0.1f), new Keyframe(1f, 0f));

    [Tooltip("Em que ponto do mergulho o jogador volta a ter o controle. " +
             "Em 0.85 o personagem já está quase de pé, então a volta para o andar fica suave.")]
    [Range(0.3f, 1f)]
    public float fimDoMergulho = 0.85f;

    [Tooltip("Tempo mínimo (em segundos) depois de uma esquiva para fazer outra")]
    [Min(0f)]
    public float tempoDeRecarga = 0.3f;

    [Header("Estados no Animator (Base Layer)")]
    public string estadoDesarmado = "EsquivaDesarmado";
    public string estadoArmadoFrente = "EsquivaFrente";
    public string estadoArmadoTras = "EsquivaTras";
    public string estadoArmadoEsquerda = "EsquivaEsquerda";
    public string estadoArmadoDireita = "EsquivaDireita";

    [Tooltip("Estado para onde o personagem volta ao terminar a esquiva")]
    public string estadoDeLocomocao = "Idle Walk Run Blend";

    [Tooltip("Tempo da mistura entre animações (segundos)")]
    [Range(0f, 0.5f)]
    public float tempoDeTransicao = 0.1f;

    public TipoDeEsquiva TipoAtual { get; private set; } = TipoDeEsquiva.Nenhuma;
    public bool EstaEsquivando => TipoAtual != TipoDeEsquiva.Nenhuma;

    /// <summary>
    /// Invencível só durante a janela do mergulho desarmado ("i-frames").
    /// A esquiva armada NÃO dá invencibilidade: ela só tira o personagem do lugar.
    /// </summary>
    public bool EstaInvencivel
    {
        get
        {
            if (TipoAtual != TipoDeEsquiva.Mergulho) return false;

            float progresso = tempoDecorrido / duracaoDaAnimacao;
            return progresso >= inicioDaInvencibilidade && progresso <= fimDaInvencibilidade;
        }
    }

    private const int CamadaBase = 0;
    private const float DuracaoReserva = 1f; // usada até o Animator informar a duração real

    private Animator animator;
    private CharacterController characterController;
    private IControleMovimento controleMovimento;
    private IEstadoArma estadoArma;
    private Transform cameraPrincipal;

    private Vector3 direcaoDaEsquiva;
    private float distanciaDaEsquiva;
    private int hashDoEstadoAtual;
    private float duracaoDaAnimacao;
    private bool duracaoConhecida;
    private float tempoDecorrido;
    private float proximaEsquivaPermitida;
    private float areaDaCurva;
    private float areaDaCurvaDoMergulho;
    private float velocidadeOriginalDoAnimator = 1f;

    void Awake()
    {
        BuscarDependencias();
        areaDaCurva = CalcularAreaDaCurva(curvaDeVelocidade);
        areaDaCurvaDoMergulho = CalcularAreaDaCurva(curvaDoMergulho);
        AvisarSeFaltarAlgo();
    }

    void Update()
    {
        if (EstaEsquivando)
            AtualizarEsquiva(Time.deltaTime);
        else if (JogadorQuerEsquivar() && PodeEsquivar())
            IniciarEsquiva();
    }

    // ---------- Configuração ----------

    private void BuscarDependencias()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        controleMovimento = GetComponent<IControleMovimento>();
        estadoArma = GetComponent<IEstadoArma>();

        if (Camera.main != null)
            cameraPrincipal = Camera.main.transform;
    }

    private void AvisarSeFaltarAlgo()
    {
        if (controleMovimento == null)
            Debug.LogWarning("Esquiva: nenhum controle de movimento (ex.: ThirdPersonController) encontrado neste objeto.", this);

        foreach (string estado in new[] { estadoDesarmado, estadoArmadoFrente, estadoArmadoTras, estadoArmadoEsquerda, estadoArmadoDireita, estadoDeLocomocao })
        {
            if (!animator.HasState(CamadaBase, Animator.StringToHash(estado)))
                Debug.LogWarning($"Esquiva: estado '{estado}' não encontrado na Base Layer do Animator.", this);
        }
    }

    // Área embaixo da curva, para a distância percorrida ser a configurada mesmo mudando a curva
    private static float CalcularAreaDaCurva(AnimationCurve curva)
    {
        const int amostras = 50;
        float soma = 0f;
        for (int i = 0; i < amostras; i++)
            soma += curva.Evaluate((i + 0.5f) / amostras);

        return Mathf.Max(soma / amostras, 0.01f);
    }

    // ---------- Regras ----------

    private bool JogadorQuerEsquivar()
    {
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !FluxoDoJogo.JogoParado;
    }

    private bool PodeEsquivar()
    {
        bool noChao = controleMovimento == null || controleMovimento.NoChao;
        bool livre = controleMovimento == null || !controleMovimento.MovimentoBloqueado; // ex.: caído (derrubada)
        return noChao && livre && Time.time >= proximaEsquivaPermitida;
    }

    /// <summary>
    /// Interrompe a esquiva na hora (ex.: o jogador foi derrubado no meio dela).
    /// Não mexe na animação nem no bloqueio de movimento: quem cancelou assume o controle.
    /// </summary>
    public void Cancelar()
    {
        if (!EstaEsquivando) return;
        TipoAtual = TipoDeEsquiva.Nenhuma;
        animator.speed = velocidadeOriginalDoAnimator;
        proximaEsquivaPermitida = Time.time + tempoDeRecarga;
    }

    private bool EstaArmado()
    {
        return estadoArma != null && estadoArma.EstaArmado;
    }

    // ---------- Início ----------

    private void IniciarEsquiva()
    {
        TipoDeEsquiva tipo = EscolherTipo();
        bool mergulho = tipo == TipoDeEsquiva.Mergulho;

        direcaoDaEsquiva = CalcularDirecao(tipo);
        distanciaDaEsquiva = mergulho ? distanciaDesarmado : distanciaArmado;

        // O mergulho só tem animação para frente, então o personagem vira para a direção da esquiva
        if (mergulho)
            transform.rotation = Quaternion.LookRotation(direcaoDaEsquiva);

        AcelerarAnimacoes(mergulho ? velocidadeDesarmado : velocidadeArmado);
        TocarAnimacao(mergulho ? estadoDesarmado : EscolherEstadoArmado(direcaoDaEsquiva));
        BloquearMovimento(true);

        tempoDecorrido = 0f;
        duracaoConhecida = false;
        duracaoDaAnimacao = DuracaoReserva / animator.speed;
        TipoAtual = tipo;
    }

    // Armado: esquivas armadas (frente, trás, esquerda, direita). Desarmado: mergulho.
    private TipoDeEsquiva EscolherTipo()
    {
        return EstaArmado() ? TipoDeEsquiva.Armada : TipoDeEsquiva.Mergulho;
    }

    // Deixa o Animator mais rápido só durante a esquiva (volta ao normal em TerminarEsquiva)
    private void AcelerarAnimacoes(float multiplicador)
    {
        velocidadeOriginalDoAnimator = animator.speed;
        animator.speed = velocidadeOriginalDoAnimator * multiplicador;
    }

    // Direção das teclas apertadas, girada para acompanhar a câmera (igual ao andar)
    private Vector3 CalcularDirecao(TipoDeEsquiva tipo)
    {
        Vector2 entrada = controleMovimento != null ? controleMovimento.EntradaMovimento : Vector2.zero;

        // Parado: a esquiva armada vai para trás, o mergulho vai para frente
        if (entrada.sqrMagnitude < 0.01f)
            return tipo == TipoDeEsquiva.Armada ? -transform.forward : transform.forward;

        float anguloDaCamera = cameraPrincipal != null ? cameraPrincipal.eulerAngles.y : transform.eulerAngles.y;
        Vector3 direcao = Quaternion.Euler(0f, anguloDaCamera, 0f) * new Vector3(entrada.x, 0f, entrada.y);
        return direcao.normalized;
    }

    // Compara a direção da esquiva com para onde o personagem está olhando
    private string EscolherEstadoArmado(Vector3 direcaoNoMundo)
    {
        Vector3 local = transform.InverseTransformDirection(direcaoNoMundo);

        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            return local.z >= 0f ? estadoArmadoFrente : estadoArmadoTras;

        return local.x >= 0f ? estadoArmadoDireita : estadoArmadoEsquerda;
    }

    private void TocarAnimacao(string estado)
    {
        hashDoEstadoAtual = Animator.StringToHash(estado);
        animator.CrossFadeInFixedTime(hashDoEstadoAtual, tempoDeTransicao, CamadaBase);
    }

    // ---------- Durante ----------

    private void AtualizarEsquiva(float deltaTime)
    {
        AtualizarDuracaoDaAnimacao();
        tempoDecorrido += deltaTime;

        float progresso = tempoDecorrido / duracaoDaAnimacao;
        MoverPersonagem(progresso, deltaTime);

        float fim = TipoAtual == TipoDeEsquiva.Mergulho ? fimDoMergulho : fimDaEsquiva;
        if (progresso >= fim)
            TerminarEsquiva();
    }

    // O Animator só informa a duração real do clipe depois que a animação começa.
    // A duração é dividida pela velocidade do Animator, já que ele está acelerado.
    private void AtualizarDuracaoDaAnimacao()
    {
        if (duracaoConhecida) return;

        AnimatorStateInfo info = animator.IsInTransition(CamadaBase)
            ? animator.GetNextAnimatorStateInfo(CamadaBase)
            : animator.GetCurrentAnimatorStateInfo(CamadaBase);

        if (info.shortNameHash == hashDoEstadoAtual && info.length > 0f)
        {
            duracaoDaAnimacao = info.length / animator.speed;
            duracaoConhecida = true;
        }
    }

    private void MoverPersonagem(float progresso, float deltaTime)
    {
        bool mergulho = TipoAtual == TipoDeEsquiva.Mergulho;
        AnimationCurve curva = mergulho ? curvaDoMergulho : curvaDeVelocidade;
        float area = mergulho ? areaDaCurvaDoMergulho : areaDaCurva;

        float velocidadeMedia = distanciaDaEsquiva / (duracaoDaAnimacao * area);
        float velocidade = velocidadeMedia * curva.Evaluate(progresso);

        characterController.Move(direcaoDaEsquiva * (velocidade * deltaTime));
    }

    // ---------- Fim ----------

    private void TerminarEsquiva()
    {
        TipoAtual = TipoDeEsquiva.Nenhuma;
        proximaEsquivaPermitida = Time.time + tempoDeRecarga;

        BloquearMovimento(false);
        animator.speed = velocidadeOriginalDoAnimator;

        if (animator.isActiveAndEnabled)
            animator.CrossFadeInFixedTime(estadoDeLocomocao, tempoDeTransicao * 2f, CamadaBase);
    }

    private void BloquearMovimento(bool bloquear)
    {
        if (controleMovimento != null)
            controleMovimento.MovimentoBloqueado = bloquear;
    }

    // Se o componente for desligado no meio da esquiva, devolve o controle ao jogador
    void OnDisable()
    {
        if (EstaEsquivando)
            TerminarEsquiva();
    }
}
