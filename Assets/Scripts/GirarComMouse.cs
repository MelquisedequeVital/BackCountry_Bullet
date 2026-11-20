using UnityEngine;
using StarterAssets;

public class GirarComMouse : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Arraste aqui a PlayerAimCamera da Hierarchy")]
    public GameObject cameraCombate;

    [Header("Ajustes")]
    [Tooltip("Velocidade com que o personagem gira para acompanhar o mouse")]
    public float velocidadeGiro = 15f;

    private Transform cameraPrincipal;
    private ThirdPersonController controller;
    private EquiparArma scriptEquipar;
    private IEstadoEsquiva estadoEsquiva;
    private bool estaArmado;

    void Start()
    {
        ConfigurarReferencias();
        AtualizarCameraCombate(false);
    }

    void Update()
    {
        VerificarEstadoArmado();
    }

    // Roda após o movimento para alinhar o corpo com a câmera enquanto estiver armado
    void LateUpdate()
    {
        GirarPersonagemComCamera();
    }

    // 1. Pega a câmera principal, o controlador e o script de equipar arma
    private void ConfigurarReferencias()
    {
        if (Camera.main != null)
        {
            cameraPrincipal = Camera.main.transform;
        }

        controller = GetComponent<ThirdPersonController>();
        scriptEquipar = GetComponent<EquiparArma>();
        estadoEsquiva = GetComponent<IEstadoEsquiva>();
    }

    // 2. Ativa a câmera aproximada e trava o giro automático assim que equipar a arma (Q)
    private void VerificarEstadoArmado()
    {
        bool armadoAgora = (scriptEquipar != null && scriptEquipar.estaArmado);

        if (armadoAgora != estaArmado)
        {
            estaArmado = armadoAgora;
            AtualizarCameraCombate(estaArmado);
        }

        if (controller != null)
        {
            // Armado = corpo acompanha o mouse. Desarmado = corpo anda livre.
            controller.girarComMovimento = !estaArmado;
        }
    }

    // 3. Liga ou desliga a câmera aproximada sobre o ombro
    private void AtualizarCameraCombate(bool ativar)
    {
        if (cameraCombate != null)
        {
            cameraCombate.SetActive(ativar);
        }
    }

    // 4. Mantém o personagem apontado para a câmera sempre que estiver com a arma equipada.
    //    Pausa durante o mergulho, que precisa olhar para a direção do pulo.
    private void GirarPersonagemComCamera()
    {
        if (!estaArmado || cameraPrincipal == null || EstaMergulhando()) return;

        Vector3 frenteCamera = cameraPrincipal.forward;
        frenteCamera.y = 0f;

        if (frenteCamera.sqrMagnitude > 0.001f)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(frenteCamera);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeGiro * Time.deltaTime);
        }
    }

    private bool EstaMergulhando()
    {
        bool mergulhando = estadoEsquiva != null && estadoEsquiva.TipoAtual == TipoDeEsquiva.Mergulho;
        bool derrubado = TryGetComponent(out IDerrubavel derrubavel) && derrubavel.EstaDerrubado;
        return mergulhando || derrubado; // caído (derrubada) conta igual ao mergulho
    }
}
