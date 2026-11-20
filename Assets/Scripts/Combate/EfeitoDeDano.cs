using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efeito visual quando o jogador leva dano:
/// 1. As bordas da tela ficam vermelhas por um instante e somem aos poucos (vinheta);
/// 2. A câmera dá uma tremida rápida e volta para onde estava;
/// 3. Com a vida baixa, as bordas ficam pulsando em vermelho, como um aviso.
/// Tudo é montado sozinho quando o jogo começa. Coloque no mesmo objeto que tem a Vida do jogador.
/// </summary>
public class EfeitoDeDano : MonoBehaviour
{
    [Header("Bordas vermelhas")]
    public Color cor = new Color(0.8f, 0f, 0f);

    [Tooltip("Quão forte o vermelho aparece no golpe (0 a 1)")]
    [Range(0f, 1f)]
    public float intensidade = 0.6f;

    [Tooltip("Tempo (segundos) para o vermelho sumir depois do golpe")]
    [Min(0.05f)]
    public float duracao = 0.6f;

    [Tooltip("Tamanho da área limpa no centro da tela (0 = tudo vermelho, 1 = só os cantos)")]
    [Range(0f, 0.9f)]
    public float centroLimpo = 0.45f;

    [Header("Tremida da câmera")]
    [Tooltip("Força da tremida em graus (0 = sem tremida)")]
    [Range(0f, 5f)]
    public float forcaDaTremida = 1.2f;

    [Tooltip("Duração da tremida (segundos)")]
    [Range(0.05f, 0.5f)]
    public float duracaoDaTremida = 0.15f;

    [Header("Aviso de vida baixa")]
    [Tooltip("Abaixo desta porcentagem de vida as bordas pulsam (0 = desligado)")]
    [Range(0f, 1f)]
    public float vidaBaixa = 0.3f;

    [Range(0f, 1f)]
    public float intensidadeDoPulso = 0.3f;

    [Tooltip("Pulsos por segundo")]
    [Min(0.1f)]
    public float velocidadeDoPulso = 1.2f;

    private const int TamanhoDaTextura = 128;

    private Vida vida;
    private IControleMira controleMira;
    private GameObject canvas;
    private Image vinheta;

    private float forcaDoGolpe;       // 1 logo após o golpe, cai até 0
    private float tempoDeTremida;     // quanto falta da tremida
    private Vector2 tremidaAplicada;  // quanto a câmera já foi deslocada (para devolver depois)

    void Awake()
    {
        vida = GetComponentInParent<Vida>();
        controleMira = GetComponentInParent<IControleMira>();

        if (vida == null)
            Debug.LogWarning("EfeitoDeDano: nenhum componente Vida encontrado.", this);
    }

    void Start()
    {
        MontarVinheta();
    }

    void OnEnable()
    {
        if (vida != null) vida.AoReceberDano += AoLevarDano;
    }

    void OnDisable()
    {
        if (vida != null) vida.AoReceberDano -= AoLevarDano;
        DevolverCamera();
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas);
    }

    void Update()
    {
        AtualizarVinheta(Time.deltaTime);
        AtualizarTremida(Time.deltaTime);
    }

    private void AoLevarDano(float quantidade, Vector3 ponto, Vector3 direcao)
    {
        forcaDoGolpe = 1f;
        tempoDeTremida = duracaoDaTremida;
    }

    // =====================================================================
    // Bordas vermelhas
    // =====================================================================

    private void AtualizarVinheta(float deltaTime)
    {
        if (vinheta == null) return;

        forcaDoGolpe = Mathf.MoveTowards(forcaDoGolpe, 0f, deltaTime / duracao);
        float alfaDoGolpe = forcaDoGolpe * forcaDoGolpe * intensidade; // some rápido no fim
        float alfa = Mathf.Max(alfaDoGolpe, CalcularPulsoDeVidaBaixa());

        Color c = cor;
        c.a = alfa;
        vinheta.color = c;
        vinheta.enabled = alfa > 0.001f;
    }

    private float CalcularPulsoDeVidaBaixa()
    {
        if (vida == null || !vida.EstaVivo || vidaBaixa <= 0f || vida.Porcentagem > vidaBaixa)
            return 0f;

        float onda = (Mathf.Sin(Time.time * velocidadeDoPulso * Mathf.PI * 2f) + 1f) * 0.5f;
        return Mathf.Lerp(intensidadeDoPulso * 0.4f, intensidadeDoPulso, onda);
    }

    private void MontarVinheta()
    {
        canvas = new GameObject("Efeito de Dano", typeof(Canvas));
        Canvas c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 5; // abaixo da barra de vida (10), para ela continuar visível

        var objeto = new GameObject("Vinheta", typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(canvas.transform, false);

        RectTransform rect = (RectTransform)objeto.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        vinheta = objeto.GetComponent<Image>();
        vinheta.sprite = CriarSpriteDeVinheta();
        vinheta.raycastTarget = false;
        vinheta.enabled = false;
    }

    // Textura branca: transparente no centro e opaca nas bordas (a cor vem da Image)
    private Sprite CriarSpriteDeVinheta() => CriarSpriteDeVinheta(centroLimpo);

    /// <summary>
    /// Cria a imagem da vinheta: transparente no centro e opaca nas bordas (a cor vem da Image).
    /// Pública para outros efeitos de tela reaproveitarem (ex.: o feedback de esquiva).
    /// </summary>
    public static Sprite CriarSpriteDeVinheta(float centroLimpo)
    {
        var textura = new Texture2D(TamanhoDaTextura, TamanhoDaTextura, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[TamanhoDaTextura * TamanhoDaTextura];
        float metade = (TamanhoDaTextura - 1) * 0.5f;

        for (int y = 0; y < TamanhoDaTextura; y++)
        {
            for (int x = 0; x < TamanhoDaTextura; x++)
            {
                // Distância do centro: 0 no meio, ~1 nas bordas e ~1,41 nos cantos
                float dx = (x - metade) / metade;
                float dy = (y - metade) / metade;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);

                float alfa = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(centroLimpo, 1.2f, distancia));
                pixels[y * TamanhoDaTextura + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
            }
        }

        textura.SetPixels32(pixels);
        textura.Apply();
        return Sprite.Create(textura, new Rect(0, 0, TamanhoDaTextura, TamanhoDaTextura), new Vector2(0.5f, 0.5f));
    }

    // =====================================================================
    // Tremida da câmera
    // =====================================================================

    // A cada quadro a câmera vai para um ponto aleatório que diminui com o tempo;
    // no fim, o deslocamento total é devolvido, e a mira volta exatamente para onde estava.
    private void AtualizarTremida(float deltaTime)
    {
        if (controleMira == null || tempoDeTremida <= 0f) return;

        tempoDeTremida -= deltaTime;

        if (tempoDeTremida <= 0f)
        {
            DevolverCamera();
            return;
        }

        float forca = forcaDaTremida * (tempoDeTremida / duracaoDaTremida);
        Vector2 alvo = Random.insideUnitCircle * forca;
        Vector2 passo = alvo - tremidaAplicada;

        controleMira.GirarMira(passo.y, passo.x);
        tremidaAplicada = alvo;
    }

    private void DevolverCamera()
    {
        if (controleMira != null && tremidaAplicada != Vector2.zero)
            controleMira.GirarMira(-tremidaAplicada.y, -tremidaAplicada.x);

        tremidaAplicada = Vector2.zero;
        tempoDeTremida = 0f;
    }
}
