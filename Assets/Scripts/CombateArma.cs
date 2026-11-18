using UnityEngine;
using UnityEngine.InputSystem;

public class CombateArma : MonoBehaviour
{
    [Header("Referências de Mira")]
    [Tooltip("Arraste aqui o objeto Mirinha de dentro do Canvas")]
    public GameObject mirinhaUI;

    [Tooltip("Arraste aqui a PlayerAimCamera da Hierarchy")]
    public GameObject cameraCombate;

    [Header("Ajustes de Movimento e Giro")]
    [Tooltip("Velocidade com que o personagem acompanha a rotação do mouse")]
    public float velocidadeGiro = 18f;

    [Tooltip("Suavização da troca entre andar para frente, trás e lados")]
    public float suavizacaoAnimacao = 10f;

    // Referências internas
    private EquiparArma scriptEquipar;
    private Animator animator;
    private Transform cameraPrincipal;
    private Vector2 movimentoAtual;
    private bool modoCombateAtivo;

    void Start()
    {
        ConfigurarReferencias();
        AtualizarInterfaceECamera(false);
    }

    void Update()
    {
        VerificarEstadoEquipado();
        AtualizarAnimacaoDirecional();
    }

    // Roda depois do ThirdPersonController para impedir o boneco de virar de costas
    void LateUpdate()
    {
        RotacionarComMouse();
    }

    // 1. Pega os componentes necessários no personagem e na cena
    private void ConfigurarReferencias()
    {
        scriptEquipar = GetComponent<EquiparArma>();
        animator = GetComponent<Animator>();

        if (Camera.main != null)
        {
            cameraPrincipal = Camera.main.transform;
        }
    }

    // 2. Liga ou desliga a mirinha e a câmera assim que a arma é equipada ou guardada
    private void VerificarEstadoEquipado()
    {
        bool armadoAgora = (scriptEquipar != null && scriptEquipar.estaArmado);

        if (armadoAgora != modoCombateAtivo)
        {
            modoCombateAtivo = armadoAgora;
            AtualizarInterfaceECamera(modoCombateAtivo);
        }
    }

    // 3. Ativa ou desativa a mirinha na tela e a câmera sobre o ombro
    private void AtualizarInterfaceECamera(bool ativar)
    {
        if (mirinhaUI != null)
            mirinhaUI.SetActive(ativar);

        if (cameraCombate != null)
            cameraCombate.SetActive(ativar);
    }

    // 4. Lê o teclado (WASD / Setas) e envia os valores suavizados para a Blend Tree
    private void AtualizarAnimacaoDirecional()
    {
        if (animator == null) return;

        Vector2 inputAlvo = modoCombateAtivo ? LerTeclasMovimento() : Vector2.zero;
        movimentoAtual = Vector2.Lerp(movimentoAtual, inputAlvo, suavizacaoAnimacao * Time.deltaTime);

        animator.SetFloat("MoveX", movimentoAtual.x);
        animator.SetFloat("MoveY", movimentoAtual.y);
    }

    // 5. Converte as teclas pressionadas em coordenadas X (lados) e Y (frente/trás)
    private Vector2 LerTeclasMovimento()
    {
        Vector2 direcao = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direcao.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direcao.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direcao.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direcao.x -= 1f;
        }

        return Vector2.ClampMagnitude(direcao, 1f);
    }

    // 6. Mantém o personagem sempre apontado para onde a câmera/mouse está olhando
    private void RotacionarComMouse()
    {
        if (!modoCombateAtivo || cameraPrincipal == null) return;

        Vector3 frenteCamera = cameraPrincipal.forward;
        frenteCamera.y = 0f;

        if (frenteCamera.sqrMagnitude > 0.001f)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(frenteCamera);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeGiro * Time.deltaTime);
        }
    }
}
