using UnityEngine;

/// <summary>
/// Recuo por código: a cada tiro, dá um tranco para cima no tronco (os braços e a arma
/// vão junto) e depois volta suavemente para a mira. Roda por cima da animação de tiro,
/// deixando o disparo mais forte.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RecuoArma : MonoBehaviour, IReacaoAoTiro
{
    [Header("Força do Recuo")]
    [Tooltip("Quantos graus a arma sobe a cada tiro. Se subir para o lado errado, use um valor negativo.")]
    [Range(-30f, 30f)]
    public float forcaVertical = 8f;

    [Tooltip("Desvio aleatório para os lados a cada tiro (0 = sempre reto)")]
    [Range(0f, 10f)]
    public float variacaoHorizontal = 1.5f;

    [Tooltip("Limite de recuo acumulado quando atira várias vezes seguidas")]
    [Min(0f)]
    public float recuoMaximo = 20f;

    [Header("Tempo do Recuo")]
    [Tooltip("Rapidez do tranco para cima (maior = mais seco)")]
    [Min(0.1f)]
    public float velocidadeSubida = 35f;

    [Tooltip("Rapidez com que a arma volta para a mira (maior = volta mais rápido)")]
    [Min(0.1f)]
    public float velocidadeRetorno = 8f;

    [Header("Avançado")]
    [Tooltip("Osso que recebe o recuo. A Spine (coluna) move peito, braços e arma juntos.")]
    public HumanBodyBones ossoDoRecuo = HumanBodyBones.Spine;

    // x = desvio lateral, y = subida (em graus)
    private Vector2 recuoAlvo;
    private Vector2 recuoAtual;
    private Transform osso;

    void Awake()
    {
        osso = GetComponent<Animator>().GetBoneTransform(ossoDoRecuo);

        if (osso == null)
            Debug.LogWarning($"RecuoArma: osso '{ossoDoRecuo}' não encontrado. O personagem precisa ser Humanoid.", this);
    }

    public void AoAtirar()
    {
        AdicionarImpulso();
    }

    // Roda depois do Animator, para somar o recuo à pose da animação
    void LateUpdate()
    {
        AtualizarRecuo(Time.deltaTime);
        AplicarRecuoNoOsso();
    }

    private void AdicionarImpulso()
    {
        float desvioLateral = Random.Range(-variacaoHorizontal, variacaoHorizontal);
        recuoAlvo += new Vector2(desvioLateral, forcaVertical);
        recuoAlvo = Vector2.ClampMagnitude(recuoAlvo, recuoMaximo);
    }

    // O alvo volta para zero aos poucos, e o recuo atual persegue o alvo rapidamente
    private void AtualizarRecuo(float deltaTime)
    {
        recuoAlvo = Vector2.Lerp(recuoAlvo, Vector2.zero, FatorSuavizacao(velocidadeRetorno, deltaTime));
        recuoAtual = Vector2.Lerp(recuoAtual, recuoAlvo, FatorSuavizacao(velocidadeSubida, deltaTime));
    }

    private void AplicarRecuoNoOsso()
    {
        if (osso == null || recuoAtual.sqrMagnitude < 0.0001f) return;

        // Girar no eixo "direita" do personagem com ângulo negativo inclina o tronco para cima
        osso.Rotate(transform.right, -recuoAtual.y, Space.World);
        osso.Rotate(transform.up, recuoAtual.x, Space.World);
    }

    // Suavização que funciona igual em qualquer FPS
    private static float FatorSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}
