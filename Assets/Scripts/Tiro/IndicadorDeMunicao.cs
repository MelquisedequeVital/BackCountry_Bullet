using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra no HUD quantos tiros restam na carga (uma "balinha" por tiro) e, durante a recarga,
/// uma barra enchendo com o texto "RECARREGANDO".
/// Monta-se sozinho; por padrão fica logo abaixo da barra de vida do jogador.
/// Coloque no mesmo objeto que tem o componente CargaDaArma.
/// </summary>
public class IndicadorDeMunicao : MonoBehaviour
{
    [Header("Posição (pixels, numa tela 1920x1080)")]
    [Tooltip("Distância da borda esquerda e de cima. O padrão fica embaixo da barra de vida.")]
    public Vector2 margem = new Vector2(40f, 74f);

    [Tooltip("Tamanho de cada balinha")]
    public Vector2 tamanhoDaBala = new Vector2(14f, 26f);

    [Tooltip("Espaço entre as balinhas")]
    public float espacamento = 6f;

    [Header("Cores")]
    public Color corCheia = new Color(1f, 0.85f, 0.35f);
    public Color corVazia = new Color(1f, 1f, 1f, 0.15f);
    public Color corDaRecarga = new Color(1f, 0.85f, 0.35f, 0.9f);

    private IMunicao municao;
    private GameObject canvas;
    private Image[] balas;
    private GameObject painelDeRecarga;
    private RectTransform preenchimentoDaRecarga;

    void Awake()
    {
        municao = GetComponent<IMunicao>();
        if (municao == null)
        {
            Debug.LogWarning("IndicadorDeMunicao: nenhum componente de munição (ex.: CargaDaArma) neste objeto.", this);
            enabled = false;
        }
    }

    void Start()
    {
        Montar();
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas);
    }

    void LateUpdate()
    {
        if (canvas == null) return;

        bool recarregando = municao.Recarregando;
        painelDeRecarga.SetActive(recarregando);

        for (int i = 0; i < balas.Length; i++)
        {
            balas[i].enabled = !recarregando;
            balas[i].color = i < municao.TirosRestantes ? corCheia : corVazia;
        }

        if (recarregando)
            preenchimentoDaRecarga.anchorMax = new Vector2(municao.ProgressoDaRecarga, 1f);
    }

    // =====================================================================
    // Montagem
    // =====================================================================

    private void Montar()
    {
        canvas = new GameObject("HUD Munição", typeof(Canvas), typeof(CanvasScaler));
        Canvas c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;

        CanvasScaler escalador = canvas.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        MontarBalas();
        MontarPainelDeRecarga();
    }

    private void MontarBalas()
    {
        balas = new Image[municao.Capacidade];
        for (int i = 0; i < balas.Length; i++)
        {
            RectTransform bala = CriarImagem($"Bala {i + 1}", canvas.transform, corCheia);
            AncorarNoCanto(bala, margem + new Vector2(i * (tamanhoDaBala.x + espacamento), 0f), tamanhoDaBala);
            balas[i] = bala.GetComponent<Image>();
        }
    }

    // Barra do mesmo tamanho da fileira de balas, com o texto por cima
    private void MontarPainelDeRecarga()
    {
        float largura = municao.Capacidade * (tamanhoDaBala.x + espacamento) - espacamento;
        var tamanho = new Vector2(Mathf.Max(largura, 160f), tamanhoDaBala.y);

        RectTransform fundo = CriarImagem("Recarga", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
        AncorarNoCanto(fundo, margem, tamanho);
        painelDeRecarga = fundo.gameObject;

        preenchimentoDaRecarga = CriarImagem("Progresso", fundo, corDaRecarga);
        preenchimentoDaRecarga.anchorMin = Vector2.zero;
        preenchimentoDaRecarga.anchorMax = new Vector2(0f, 1f);
        preenchimentoDaRecarga.offsetMin = preenchimentoDaRecarga.offsetMax = Vector2.zero;

        var objetoTexto = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(fundo, false);
        RectTransform rectTexto = (RectTransform)objetoTexto.transform;
        rectTexto.anchorMin = Vector2.zero;
        rectTexto.anchorMax = Vector2.one;
        rectTexto.offsetMin = rectTexto.offsetMax = Vector2.zero;

        Text texto = objetoTexto.GetComponent<Text>();
        texto.text = "RECARREGANDO";
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 16;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = Color.white;
        texto.raycastTarget = false;

        painelDeRecarga.SetActive(false);
    }

    private static RectTransform CriarImagem(string nome, Transform pai, Color cor)
    {
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(pai, false);
        Image imagem = objeto.GetComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false;
        return (RectTransform)objeto.transform;
    }

    private static void AncorarNoCanto(RectTransform rect, Vector2 posicao, Vector2 tamanho)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(posicao.x, -posicao.y);
        rect.sizeDelta = tamanho;
    }
}
