using StarterAssets;
using UnityEngine;

/// <summary>
/// Recuo da mira: a cada tiro, a câmera (e a mira no centro da tela) sobe um pouco.
/// Depois, parte dessa subida pode voltar sozinha, conforme "Porcentagem de Retorno".
/// O mouse continua funcionando normalmente durante o recuo.
/// </summary>
public class RecuoCamera : MonoBehaviour, IReacaoAoTiro
{
    [Header("Força do Recuo")]
    [Tooltip("Quantos graus a mira sobe a cada tiro")]
    [Range(0f, 15f)]
    public float forcaVertical = 2f;

    [Tooltip("Desvio aleatório para os lados a cada tiro (0 = sempre reto)")]
    [Range(0f, 5f)]
    public float variacaoHorizontal = 0.5f;

    [Header("Tempo do Recuo")]
    [Tooltip("Rapidez com que a mira sobe (maior = tranco mais seco)")]
    [Min(0.1f)]
    public float velocidadeSubida = 25f;

    [Tooltip("Rapidez com que a mira volta para baixo")]
    [Min(0.1f)]
    public float velocidadeRetorno = 6f;

    [Tooltip("Quanto da subida volta sozinho. 0 = a mira fica lá em cima (você corrige com o mouse). 1 = volta tudo.")]
    [Range(0f, 1f)]
    public float porcentagemRetorno = 0.7f;

    private IControleMira controleMira;

    // Quanto ainda falta subir/virar, e quanto ainda falta voltar (x = lados, y = cima)
    private Vector2 subidaPendente;
    private Vector2 retornoPendente;

    void Awake()
    {
        controleMira = GetComponent<IControleMira>();

        if (controleMira == null)
            Debug.LogWarning("RecuoCamera: nenhum controle de mira (ex.: ThirdPersonController) encontrado neste objeto.", this);
    }

    public void AoAtirar()
    {
        AdicionarImpulso();
    }

    // Roda no Update para o ThirdPersonController já usar o valor no LateUpdate do mesmo frame
    void Update()
    {
        if (controleMira == null) return;

        float deltaTime = Time.deltaTime;
        AplicarSubida(deltaTime);
        AplicarRetorno(deltaTime);
    }

    private void AdicionarImpulso()
    {
        float desvioLateral = Random.Range(-variacaoHorizontal, variacaoHorizontal);
        subidaPendente += new Vector2(desvioLateral, forcaVertical);
    }

    // Aplica um pedaço da subida a cada frame e guarda quanto precisará voltar depois
    private void AplicarSubida(float deltaTime)
    {
        Vector2 passo = subidaPendente * FatorSuavizacao(velocidadeSubida, deltaTime);
        subidaPendente -= passo;
        retornoPendente += passo * porcentagemRetorno;

        Girar(passo);
    }

    // Devolve aos poucos a parte da subida que deve voltar
    private void AplicarRetorno(float deltaTime)
    {
        Vector2 passo = retornoPendente * FatorSuavizacao(velocidadeRetorno, deltaTime);
        retornoPendente -= passo;

        Girar(-passo);
    }

    private void Girar(Vector2 graus)
    {
        if (graus.sqrMagnitude > 0f)
            controleMira.GirarMira(graus.y, graus.x);
    }

    // Suavização que funciona igual em qualquer FPS
    private static float FatorSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}
