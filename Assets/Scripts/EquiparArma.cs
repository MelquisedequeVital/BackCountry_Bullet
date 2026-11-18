using UnityEngine;
using UnityEngine.InputSystem;

public class EquiparArma : MonoBehaviour
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

    [Header("Alinhamento da Pose (Correção Mixamo)")]
    [Tooltip("Corrige a arma apontando para a esquerda/direita")]
    [Range(-90f, 90f)]
    public float ajusteHorizontal = 40f;

    [Tooltip("Corrige o cano da arma apontando muito para cima ou para baixo")]
    [Range(-60f, 60f)]
    public float ajusteVertical = 15f;

    // Variável pública oculta para outros scripts lerem se o personagem está armado
    [HideInInspector] public bool estaArmado;

    private Animator animator;
    private Transform ossoPeito;
    private int indiceCamada;
    private float pesoAtual;

    void Start()
    {
        ConfigurarReferencias();
        DefinirEstadoInicial();
    }

    void Update()
    {
        VerificarComandoTeclado();
        AtualizarPoseAnimacao();
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

    // 3. Detecta o clique na tecla configurada (Q)
    private void VerificarComandoTeclado()
    {
        if (Keyboard.current == null) return;

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

    // 5. Mostra ou esconde o modelo 3D da arma na mão
    private void AtualizarVisibilidadeArma()
    {
        if (objetoArma != null)
        {
            objetoArma.SetActive(estaArmado);
        }
    }

    // 6. Faz a transição suave entre a pose da arma (1) e os braços soltos (0)
    private void AtualizarPoseAnimacao()
    {
        if (indiceCamada == -1) return;

        float pesoAlvo = estaArmado ? 1f : 0f;
        pesoAtual = Mathf.MoveTowards(pesoAtual, pesoAlvo, velocidadeTransicao * Time.deltaTime);
        animator.SetLayerWeight(indiceCamada, pesoAtual);
    }

    // 7. Gira o tronco proporcionalmente ao peso da camada para apontar a arma reto para frente
    private void AlinharTroncoComArma()
    {
        if (ossoPeito == null || pesoAtual <= 0.001f) return;

        ossoPeito.Rotate(transform.up, ajusteHorizontal * pesoAtual, Space.World);
        ossoPeito.Rotate(transform.right, ajusteVertical * pesoAtual, Space.World);
    }
}
