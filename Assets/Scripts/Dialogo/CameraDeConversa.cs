using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Câmera das conversas: sai de trás do jogador e vai para uma diagonal por cima do ombro dele,
/// mostrando o jogador de costas/lado e o NPC de frente. Usa uma CinemachineCamera criada por código
/// com prioridade alta, então o Cinemachine faz a transição suave da câmera normal para ela e de volta.
/// É uma classe comum (não é componente): quem usa é a CaixaDeDialogo.
/// </summary>
public class CameraDeConversa
{
    private const string NomeDaCamera = "Camera de Conversa";

    // Enquadramento (em metros), relativo ao jogador e à linha jogador → NPC
    private readonly float recuo;          // quanto a câmera fica atrás do jogador
    private readonly float lado;           // quanto ela vai para o lado (é isso que dá a diagonal)
    private readonly float altura;         // altura da câmera
    private readonly float alturaDoOlhar;  // altura do ponto para onde ela olha (meio dos dois, na altura do peito/rosto)

    private CinemachineCamera camera;

    public CameraDeConversa(float recuo, float lado, float altura, float alturaDoOlhar, float duracaoDaTransicao)
    {
        this.recuo = recuo;
        this.lado = lado;
        this.altura = altura;
        this.alturaDoOlhar = alturaDoOlhar;

        ConfigurarTransicao(duracaoDaTransicao);
    }

    public void Ativar(Transform jogador, Transform npc)
    {
        if (jogador == null || npc == null) return;
        CriarSePreciso();

        Posicionar(jogador.position, npc.position);
        camera.gameObject.SetActive(true);
    }

    public void Desativar()
    {
        if (camera != null) camera.gameObject.SetActive(false);
    }

    // ---------- Enquadramento ----------

    private void Posicionar(Vector3 jogador, Vector3 npc)
    {
        Vector3 paraONpc = npc - jogador;
        paraONpc.y = 0f;
        if (paraONpc.sqrMagnitude < 0.01f) paraONpc = Vector3.forward;
        paraONpc.Normalize();

        Vector3 direita = Vector3.Cross(Vector3.up, paraONpc);
        Vector3 alvoDoOlhar = (jogador + npc) * 0.5f + Vector3.up * alturaDoOlhar;

        // Escolhe o ombro do lado onde a câmera já está: a câmera gira menos e não "atravessa" o personagem
        Vector3 opcaoDireita = jogador - paraONpc * recuo + direita * lado + Vector3.up * altura;
        Vector3 opcaoEsquerda = jogador - paraONpc * recuo - direita * lado + Vector3.up * altura;
        Vector3 posicao = opcaoDireita;
        Camera principal = Camera.main;
        if (principal != null &&
            (opcaoEsquerda - principal.transform.position).sqrMagnitude < (opcaoDireita - principal.transform.position).sqrMagnitude)
            posicao = opcaoEsquerda;

        posicao = AfastarDeParedes(alvoDoOlhar, posicao);

        camera.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(alvoDoOlhar - posicao));
        if (principal != null) camera.Lens.FieldOfView = principal.fieldOfView;
    }

    // Se tiver parede entre o ponto de interesse e a câmera, traz a câmera para a frente da parede
    private static Vector3 AfastarDeParedes(Vector3 alvo, Vector3 posicao)
    {
        Vector3 direcao = posicao - alvo;
        float distancia = direcao.magnitude;
        if (Physics.SphereCast(alvo, 0.2f, direcao / distancia, out RaycastHit hit, distancia,
                               ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<CharacterController>() == null) // o próprio jogador não conta
        {
            return alvo + direcao / distancia * Mathf.Max(hit.distance - 0.1f, 0.5f);
        }
        return posicao;
    }

    // ---------- Montagem ----------

    private void CriarSePreciso()
    {
        if (camera != null) return;

        var objeto = new GameObject(NomeDaCamera);
        objeto.SetActive(false);
        camera = objeto.AddComponent<CinemachineCamera>();
        camera.Priority = 100; // maior que a câmera normal: enquanto estiver ativa, é ela que manda
    }

    // Transição própria (sem mexer na transição padrão do jogo): da câmera normal para a de conversa e de volta
    private static void ConfigurarTransicao(float duracao)
    {
        Camera principal = Camera.main;
        if (principal == null || !principal.TryGetComponent(out CinemachineBrain cerebro)) return;
        if (cerebro.CustomBlends != null) return; // o projeto já tem transições personalizadas: respeita

        var transicao = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, duracao);
        var configuracao = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
        configuracao.CustomBlends = new[]
        {
            new CinemachineBlenderSettings.CustomBlend { From = "**ANY CAMERA**", To = NomeDaCamera, Blend = transicao },
            new CinemachineBlenderSettings.CustomBlend { From = NomeDaCamera, To = "**ANY CAMERA**", Blend = transicao },
        };
        cerebro.CustomBlends = configuracao;
    }
}
