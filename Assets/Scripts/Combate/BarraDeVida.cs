using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida que se monta sozinha quando o jogo começa (não precisa criar Canvas nem imagens).
/// - HUD: fixa no canto superior esquerdo da tela (para o jogador).
/// - Acima da cabeça: flutua sobre o personagem e sempre olha para a câmera (para os inimigos).
/// - Chefe no topo: barra larga no centro do topo da tela, com o nome embaixo (para o boss).
/// Mostra também um "rastro" claro que desce devagar depois de cada dano, para o jogador
/// perceber quanto perdeu.
/// Coloque no mesmo objeto que tem o componente Vida.
/// </summary>
public class BarraDeVida : MonoBehaviour
{
    public enum Posicao { HudCantoSuperiorEsquerdo, AcimaDaCabeca, ChefeNoTopo }

    [Tooltip("Onde a barra aparece")]
    public Posicao posicao = Posicao.AcimaDaCabeca;

    [Header("Cores")]
    public Color corDaVida = new Color(0.25f, 0.85f, 0.35f);
    [Tooltip("Cor do rastro que mostra o dano recém-recebido")]
    public Color corDoRastro = new Color(1f, 0.85f, 0.3f);
    public Color corDoFundo = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("Muda a cor da vida para vermelho quando estiver abaixo desta porcentagem (0 = nunca)")]
    [Range(0f, 1f)]
    public float vidaBaixa = 0.3f;
    public Color corDaVidaBaixa = new Color(0.9f, 0.2f, 0.2f);

    [Header("HUD (canto da tela)")]
    [Tooltip("Tamanho da barra em pixels (numa tela 1920x1080)")]
    public Vector2 tamanhoNaTela = new Vector2(320f, 24f);
    [Tooltip("Distância da barra até as bordas esquerda e de cima, em pixels")]
    public Vector2 margem = new Vector2(40f, 40f);

    [Header("Chefe no topo")]
    [Tooltip("Nome mostrado embaixo da barra do chefe")]
    public string nomeNaBarra = "Super Mecha";
    [Tooltip("Tamanho da barra do chefe em pixels (numa tela 1920x1080)")]
    public Vector2 tamanhoDaBarraDoChefe = new Vector2(900f, 22f);
    [Tooltip("Distância da barra até o topo da tela, em pixels")]
    public float distanciaDoTopo = 40f;
    [Min(8)]
    public int tamanhoDoNome = 26;

    [Header("Acima da cabeça")]
    [Tooltip("Tamanho da barra em metros")]
    public Vector2 tamanhoNoMundo = new Vector2(1f, 0.1f);
    [Tooltip("Distância extra acima do topo do personagem, em metros")]
    public float alturaAcimaDaCabeca = 0.3f;
    [Tooltip("Esconde a barra enquanto a vida estiver cheia")]
    public bool esconderComVidaCheia = false;

    [Header("Animação")]
    [Tooltip("Quanto tempo o rastro espera antes de começar a descer (segundos)")]
    [Min(0f)]
    public float atrasoDoRastro = 0.4f;
    [Tooltip("Velocidade com que o rastro desce (fração da barra por segundo)")]
    [Min(0.01f)]
    public float velocidadeDoRastro = 0.6f;

    private const float Borda = 3f; // espaço (em pixels) entre o fundo e o preenchimento

    private Vida vida;
    private GameObject raiz;
    private RectTransform preenchimento;
    private RectTransform rastro;
    private Image imagemDaVida;
    private Text textoDoNome;
    private Transform cameraPrincipal;

    private bool EstaNaTela => posicao != Posicao.AcimaDaCabeca;

    private float valorDoRastro = 1f;
    private float ultimaPorcentagem = 1f;
    private float rastroDesceEm;

    void Awake()
    {
        vida = GetComponentInParent<Vida>(); // procura neste objeto e, se não achar, nos "pais"
        if (vida == null)
        {
            Debug.LogWarning("BarraDeVida: nenhum componente Vida encontrado neste objeto.", this);
            enabled = false;
        }
    }

    void Start()
    {
        if (Camera.main != null)
            cameraPrincipal = Camera.main.transform;

        switch (posicao)
        {
            case Posicao.HudCantoSuperiorEsquerdo: MontarNaTela(); break;
            case Posicao.ChefeNoTopo: MontarChefeNoTopo(); break;
            default: MontarAcimaDaCabeca(); break;
        }
    }

    /// <summary>Troca o nome mostrado na barra do chefe (também pode ser mudado no Inspector).</summary>
    public void DefinirNome(string novoNome)
    {
        nomeNaBarra = novoNome;
        if (textoDoNome != null) textoDoNome.text = novoNome;
    }

    /// <summary>Mostra ou esconde a barra (ex.: só mostrar a do chefe quando a luta começar).</summary>
    public void Mostrar(bool mostrar)
    {
        if (raiz != null) raiz.SetActive(mostrar);
    }

    void LateUpdate()
    {
        if (raiz == null) return;

        float porcentagem = Mathf.Clamp01(vida.Porcentagem);
        AtualizarPreenchimento(porcentagem);
        AtualizarRastro(porcentagem);
        AtualizarVisibilidade(porcentagem);

        if (posicao == Posicao.AcimaDaCabeca)
            OlharParaCamera();
    }

    void OnDestroy()
    {
        // As barras da tela não são filhas do personagem, então precisam ser apagadas junto com ele
        if (raiz != null && EstaNaTela)
            Destroy(raiz);
    }

    // =====================================================================
    // Montagem
    // =====================================================================

    private void MontarNaTela()
    {
        CriarCanvasDaTela($"HUD Vida ({name})");

        // Fundo ancorado no canto superior esquerdo, afastado das bordas pela margem
        RectTransform fundo = CriarImagem("Fundo", raiz.transform, corDoFundo);
        fundo.anchorMin = fundo.anchorMax = fundo.pivot = new Vector2(0f, 1f);
        fundo.anchoredPosition = new Vector2(margem.x, -margem.y);
        fundo.sizeDelta = tamanhoNaTela;

        CriarBarras(fundo);
    }

    // Barra larga centralizada no topo, com o nome do chefe logo embaixo
    private void MontarChefeNoTopo()
    {
        CriarCanvasDaTela($"HUD Chefe ({name})");

        RectTransform fundo = CriarImagem("Fundo", raiz.transform, corDoFundo);
        fundo.anchorMin = fundo.anchorMax = fundo.pivot = new Vector2(0.5f, 1f);
        fundo.anchoredPosition = new Vector2(0f, -distanciaDoTopo);
        fundo.sizeDelta = tamanhoDaBarraDoChefe;

        CriarBarras(fundo);

        var objetoTexto = new GameObject("Nome", typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(raiz.transform, false);
        RectTransform rectTexto = (RectTransform)objetoTexto.transform;
        rectTexto.anchorMin = rectTexto.anchorMax = rectTexto.pivot = new Vector2(0.5f, 1f);
        rectTexto.anchoredPosition = new Vector2(0f, -(distanciaDoTopo + tamanhoDaBarraDoChefe.y + 6f));
        rectTexto.sizeDelta = new Vector2(tamanhoDaBarraDoChefe.x, tamanhoDoNome * 1.6f);

        textoDoNome = objetoTexto.GetComponent<Text>();
        textoDoNome.text = nomeNaBarra;
        textoDoNome.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textoDoNome.fontSize = tamanhoDoNome;
        textoDoNome.fontStyle = FontStyle.Bold;
        textoDoNome.alignment = TextAnchor.UpperCenter;
        textoDoNome.color = Color.white;
        textoDoNome.raycastTarget = false;

        Outline contorno = objetoTexto.AddComponent<Outline>(); // contorno preto: legível em qualquer fundo
        contorno.effectColor = new Color(0f, 0f, 0f, 0.8f);
        contorno.effectDistance = new Vector2(2f, -2f);
    }

    private void CriarCanvasDaTela(string nomeDoCanvas)
    {
        raiz = new GameObject(nomeDoCanvas, typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler escalador = raiz.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
    }

    private void MontarAcimaDaCabeca()
    {
        raiz = new GameObject("Barra de Vida", typeof(Canvas));
        raiz.transform.SetParent(transform, false);
        raiz.transform.localPosition = new Vector3(0f, CalcularAlturaDaCabeca() + alturaAcimaDaCabeca, 0f);

        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // O Canvas trabalha em "pixels"; 100 pixels = 1 metro
        const float pixelsPorMetro = 100f;
        RectTransform rectCanvas = raiz.GetComponent<RectTransform>();
        rectCanvas.sizeDelta = tamanhoNoMundo * pixelsPorMetro;
        raiz.transform.localScale = Vector3.one / pixelsPorMetro;

        RectTransform fundo = CriarImagem("Fundo", raiz.transform, corDoFundo);
        Esticar(fundo, 0f);

        CriarBarras(fundo);
    }

    // Rastro embaixo e vida por cima, ambos crescendo da esquerda para a direita
    private void CriarBarras(RectTransform fundo)
    {
        rastro = CriarImagem("Rastro", fundo, corDoRastro);
        Esticar(rastro, Borda);

        preenchimento = CriarImagem("Vida", fundo, corDaVida);
        Esticar(preenchimento, Borda);
        imagemDaVida = preenchimento.GetComponent<Image>();
    }

    // Topo do personagem: usa o tamanho do modelo 3D (ou do colisor, se não houver modelo)
    private float CalcularAlturaDaCabeca()
    {
        Bounds? limites = null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (limites == null) limites = r.bounds;
            else { Bounds b = limites.Value; b.Encapsulate(r.bounds); limites = b; }
        }

        if (limites == null && TryGetComponent(out Collider colisor))
            limites = colisor.bounds;

        return limites.HasValue ? limites.Value.max.y - transform.position.y : 2f;
    }

    private static RectTransform CriarImagem(string nome, Transform pai, Color cor)
    {
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(Image));
        objeto.transform.SetParent(pai, false);

        Image imagem = objeto.GetComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false; // a barra não bloqueia cliques
        return (RectTransform)objeto.transform;
    }

    // Faz o retângulo ocupar todo o pai, com um espaço nas bordas
    private static void Esticar(RectTransform rect, float espaco)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(espaco, espaco);
        rect.offsetMax = new Vector2(-espaco, -espaco);
    }

    // =====================================================================
    // Atualização
    // =====================================================================

    // A largura da barra é controlada pela âncora direita (0 = vazia, 1 = cheia)
    private void AtualizarPreenchimento(float porcentagem)
    {
        preenchimento.anchorMax = new Vector2(porcentagem, 1f);
        imagemDaVida.color = (vidaBaixa > 0f && porcentagem <= vidaBaixa) ? corDaVidaBaixa : corDaVida;
    }

    // O rastro fica parado um instante depois do dano e então desce até a vida atual
    private void AtualizarRastro(float porcentagem)
    {
        if (porcentagem < ultimaPorcentagem)
            rastroDesceEm = Time.time + atrasoDoRastro;

        if (porcentagem > valorDoRastro)
            valorDoRastro = porcentagem; // curou: o rastro acompanha na hora
        else if (Time.time >= rastroDesceEm)
            valorDoRastro = Mathf.MoveTowards(valorDoRastro, porcentagem, velocidadeDoRastro * Time.deltaTime);

        rastro.anchorMax = new Vector2(valorDoRastro, 1f);
        ultimaPorcentagem = porcentagem;
    }

    private void AtualizarVisibilidade(float porcentagem)
    {
        if (posicao != Posicao.AcimaDaCabeca) return;

        bool cheia = porcentagem >= 0.999f && valorDoRastro >= 0.999f;
        bool visivel = vida.EstaVivo && !(esconderComVidaCheia && cheia);

        if (raiz.activeSelf != visivel)
            raiz.SetActive(visivel);
    }

    // A barra fica sempre de frente para quem está olhando
    private void OlharParaCamera()
    {
        if (cameraPrincipal != null)
            raiz.transform.rotation = cameraPrincipal.rotation;
    }
}
