using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feedback de "esquiva perfeita": quando um golpe é evitado pela invencibilidade do mergulho,
/// as bordas da tela piscam em azul, o jogo fica em câmera lenta por um instante e (opcional) toca um som.
/// Assim o jogador SABE que desviou, em vez de ficar na dúvida.
/// Coloque no mesmo objeto que tem a Vida do jogador.
/// </summary>
public class FeedbackDeEsquiva : MonoBehaviour
{
    [Header("Bordas da tela")]
    public Color cor = new Color(0.3f, 0.85f, 1f);
    [Range(0f, 1f)] public float intensidade = 0.55f;
    [Tooltip("Tempo para o brilho sumir (segundos reais)")]
    [Min(0.05f)] public float duracaoDoBrilho = 0.45f;

    [Header("Câmera lenta")]
    [Tooltip("Velocidade do jogo durante o efeito (1 = normal, 0.3 = bem lento, 1 = desliga)")]
    [Range(0.05f, 1f)] public float velocidadeDoJogo = 0.35f;
    [Tooltip("Duração da câmera lenta (segundos reais)")]
    [Range(0f, 0.5f)] public float duracaoDaCameraLenta = 0.15f;

    [Header("Som (opcional)")]
    public AudioClip somDeEsquiva;
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("Repetição")]
    [Tooltip("Tempo mínimo entre dois feedbacks (um projétil atravessando avisa vários quadros seguidos)")]
    [Min(0f)] public float intervaloMinimo = 0.6f;

    private Vida vida;
    private GameObject canvas;
    private Image vinheta;
    private AudioSource fonte;

    private float forca;              // 1 no momento da esquiva, cai até 0
    private float fimDaCameraLenta = -1f;
    private float proximoPermitido;
    private bool emCameraLenta;

    void Awake()
    {
        vida = GetComponentInParent<Vida>();
        if (vida == null)
            Debug.LogWarning("FeedbackDeEsquiva: nenhum componente Vida encontrado.", this);
    }

    void Start() => MontarVinheta();

    void OnEnable()
    {
        if (vida != null) vida.AoEvitarDano += AoDesviar;
    }

    void OnDisable()
    {
        if (vida != null) vida.AoEvitarDano -= AoDesviar;
        SairDaCameraLenta();
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas);
    }

    // Usa o tempo "real" (unscaled), porque o próprio efeito deixa o jogo mais lento
    void Update()
    {
        float deltaReal = Time.unscaledDeltaTime;

        forca = Mathf.MoveTowards(forca, 0f, deltaReal / duracaoDoBrilho);
        if (vinheta != null)
        {
            Color c = cor;
            c.a = forca * intensidade;
            vinheta.color = c;
            vinheta.enabled = c.a > 0.001f;
        }

        if (emCameraLenta && Time.unscaledTime >= fimDaCameraLenta)
            SairDaCameraLenta();
    }

    private void AoDesviar()
    {
        if (Time.unscaledTime < proximoPermitido) return;

        // Só é "esquiva perfeita" se estiver esquivando (a invencibilidade de estar caído, por exemplo, não conta)
        if (!TryGetComponent(out IEstadoEsquiva esquiva) || !esquiva.EstaEsquivando) return;
        proximoPermitido = Time.unscaledTime + intervaloMinimo;

        forca = 1f;
        TocarSom();

        if (velocidadeDoJogo < 1f && duracaoDaCameraLenta > 0f)
        {
            Time.timeScale = velocidadeDoJogo;
            fimDaCameraLenta = Time.unscaledTime + duracaoDaCameraLenta;
            emCameraLenta = true;
        }
    }

    private void SairDaCameraLenta()
    {
        if (!emCameraLenta) return;
        Time.timeScale = 1f;
        emCameraLenta = false;
    }

    private void TocarSom()
    {
        if (somDeEsquiva == null) return;
        if (fonte == null)
        {
            fonte = gameObject.AddComponent<AudioSource>();
            fonte.playOnAwake = false;
            fonte.spatialBlend = 0f;
        }
        fonte.PlayOneShot(somDeEsquiva, volume);
    }

    private void MontarVinheta()
    {
        canvas = new GameObject("Feedback de Esquiva", typeof(Canvas));
        Canvas c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 6; // acima do efeito de dano, abaixo da barra de vida

        var objeto = new GameObject("Vinheta", typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(canvas.transform, false);
        RectTransform rect = (RectTransform)objeto.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        vinheta = objeto.GetComponent<Image>();
        vinheta.sprite = EfeitoDeDano.CriarSpriteDeVinheta(0.5f);
        vinheta.raycastTarget = false;
        vinheta.enabled = false;
    }
}
