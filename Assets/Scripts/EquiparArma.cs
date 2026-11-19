using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public class EquiparArma : MonoBehaviour, IEstadoArma
{
    [Header("Configurações Gerais")]
    [Tooltip("Arraste aqui o objeto da arma que está na mão do personagem")]
    public GameObject objetoArma;

    [Tooltip("Nome exato da camada da arma no Animator")]
    public string nomeCamadaArmas = "Armas";

    [Tooltip("Tecla para sacar ou guardar a arma")]
    public Key teclaEquipar = Key.Q;

    [Tooltip("Velocidade com que os braços levantam ou abaixam")]
    public float velocidadeTransicao = 8f;

    [Tooltip("Marque se quiser começar o jogo já com a arma na mão")]
    public bool comecarArmado = false;

    [Tooltip("Quanto da pose de mira continua durante a esquiva armada. 1 = arma parada na mira, 0 = só a animação de esquiva.")]
    [Range(0f, 1f)]
    public float poseDeMiraNaEsquiva = 0.85f;

    [Header("Movimento Armado")]
    [Tooltip("Velocidade de andar/correr com a arma na mão (1 = normal, 0.5 = metade). " +
             "Armado o personagem é mais lento: para escapar de ataques fortes é preciso guardar a arma.")]
    [Range(0.2f, 1f)]
    public float velocidadeArmado = 0.5f;

    [Header("Alinhamento da Pose (Correção Mixamo)")]
    [Tooltip("Corrige a arma apontando para a esquerda/direita")]
    [Range(-90f, 90f)]
    public float ajusteHorizontal = 40f;

    [Tooltip("Corrige o cano da arma apontando muito para cima ou para baixo")]
    [Range(-60f, 60f)]
    public float ajusteVertical = 15f;

    [Tooltip("Quanto do Ajuste Horizontal continua durante a esquiva armada. Menor = menos torção do tronco. " +
             "1 = correção inteira, 0 = sem correção.")]
    [Range(0f, 1f)]
    public float ajusteHorizontalNaEsquiva = 0.3f;

    // Variável pública oculta para outros scripts lerem se o personagem está armado
    [HideInInspector] public bool estaArmado;

    // Implementação da interface IEstadoArma (usada pelo sistema de tiro)
    public bool EstaArmado => estaArmado;

    private Animator animator;
    private IEstadoEsquiva estadoEsquiva;
    private IControleMovimento controleMovimento;
    private float fatorArmado; // 0 = desarmado, 1 = armado (muda aos poucos)
    private Transform ossoPeito;
    private int indiceCamada;
    private float pesoAtual;
    private float fatorAjusteHorizontal = 1f;

    void Start()
    {
        ConfigurarReferencias();
        DefinirEstadoInicial();
    }

    void Update()
    {
        VerificarComandoTeclado();
        AtualizarVisibilidadeArma();
        AtualizarPoseAnimacao();
        AtualizarVelocidadeDeMovimento();
    }

    // Roda após o Animator calcular a pose do frame para alinhar o tronco
    void LateUpdate()
    {
        AlinharTroncoComArma();
    }

    // 1. Busca o Animator, a camada de armas e o osso do peito
    private void ConfigurarReferencias()
    {
        animator = GetComponent<Animator>();
        estadoEsquiva = GetComponent<IEstadoEsquiva>();
        controleMovimento = GetComponent<IControleMovimento>();
        indiceCamada = animator.GetLayerIndex(nomeCamadaArmas);
        ossoPeito = animator.GetBoneTransform(HumanBodyBones.Chest);

        if (indiceCamada == -1)
        {
            Debug.LogWarning($"Camada '{nomeCamadaArmas}' não encontrada no Animator!");
        }
    }

    // 2. Define o estado inicial da arma e da pose ao iniciar o jogo
    private void DefinirEstadoInicial()
    {
        estaArmado = comecarArmado;
        pesoAtual = estaArmado ? 1f : 0f;

        AtualizarVisibilidadeArma();

        if (indiceCamada != -1)
        {
            animator.SetLayerWeight(indiceCamada, pesoAtual);
        }
    }

    // 3. Detecta o clique na tecla configurada (Q). Não troca de arma no meio da esquiva.
    private void VerificarComandoTeclado()
    {
        if (Keyboard.current == null || EstaEsquivando()) return;

        if (Keyboard.current[teclaEquipar].wasPressedThisFrame)
        {
            AlternarEstadoArma();
        }
    }

    // 4. Alterna entre armado e desarmado
    private void AlternarEstadoArma()
    {
        estaArmado = !estaArmado;
        AtualizarVisibilidadeArma();
    }

    // 5. Mostra ou esconde o modelo 3D da arma na mão (também some durante o mergulho)
    private void AtualizarVisibilidadeArma()
    {
        if (objetoArma == null) return;

        bool visivel = estaArmado && !EstaMergulhando();
        if (objetoArma.activeSelf != visivel)
        {
            objetoArma.SetActive(visivel);
        }
    }

    // 6. Faz a transição suave entre a pose da arma (1) e os braços soltos (0).
    //    No mergulho a pose sai na hora (o corpo inteiro faz o mergulho).
    //    Na esquiva armada curta, a pose continua quase toda (ajustável em poseDeMiraNaEsquiva).
    private void AtualizarPoseAnimacao()
    {
        if (indiceCamada == -1) return;

        if (EstaMergulhando())
            pesoAtual = 0f;

        float pesoAlvo = CalcularPesoAlvo();
        pesoAtual = Mathf.MoveTowards(pesoAtual, pesoAlvo, velocidadeTransicao * Time.deltaTime);
        animator.SetLayerWeight(indiceCamada, pesoAtual);

        AtualizarFatorAjusteHorizontal();
    }

    // Na esquiva o quadril já gira com a animação; somar a correção inteira torce demais o tronco.
    // Por isso a correção horizontal diminui aos poucos durante a esquiva e volta no final.
    private void AtualizarFatorAjusteHorizontal()
    {
        float fatorAlvo = EstaEsquivando() ? ajusteHorizontalNaEsquiva : 1f;
        fatorAjusteHorizontal = Mathf.MoveTowards(fatorAjusteHorizontal, fatorAlvo, velocidadeTransicao * Time.deltaTime);
    }

    // 7. Gira o tronco proporcionalmente ao peso da camada para apontar a arma reto para frente
    private void AlinharTroncoComArma()
    {
        if (ossoPeito == null || pesoAtual <= 0.001f) return;

        ossoPeito.Rotate(transform.up, ajusteHorizontal * fatorAjusteHorizontal * pesoAtual, Space.World);
        ossoPeito.Rotate(transform.right, ajusteVertical * pesoAtual, Space.World);
    }

    // 8. Armado o personagem anda mais devagar e não corre (a troca é suave, no mesmo ritmo dos braços)
    private void AtualizarVelocidadeDeMovimento()
    {
        if (controleMovimento == null) return;

        controleMovimento.CorridaPermitida = !estaArmado;

        fatorArmado = Mathf.MoveTowards(fatorArmado, estaArmado ? 1f : 0f, velocidadeTransicao * Time.deltaTime);
        controleMovimento.MultiplicadorDeVelocidade = Mathf.Lerp(1f, velocidadeArmado, fatorArmado);
    }

    private float CalcularPesoAlvo()
    {
        if (!estaArmado || EstaMergulhando()) return 0f;
        return EstaEsquivando() ? poseDeMiraNaEsquiva : 1f;
    }

    private bool EstaEsquivando()
    {
        return estadoEsquiva != null && estadoEsquiva.EstaEsquivando;
    }

    private bool EstaMergulhando()
    {
        return estadoEsquiva != null && estadoEsquiva.TipoAtual == TipoDeEsquiva.Mergulho;
    }
}
