using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Parâmetros do relevo da caatinga. Gere e regenere pelo botão no Inspector.</summary>
    [CreateAssetMenu(menuName = "Backcountry Bullet/Terrain Relief Settings", fileName = "CaatingaRelief")]
    public class TerrainReliefSettings : ScriptableObject
    {
        public CaatingaLayoutSettings layout;
        public int seed = 1977;

        [Header("Planície")]
        [Tooltip("Altura do chão plano das áreas reservadas (m).")]
        public float baseHeight = 20f;
        [Tooltip("Meia-largura da área jogável a partir do centro do terreno (m).")]
        public float playableHalfSize = 200f;
        public float plainAmplitude = 3.5f;
        public float plainWavelength = 160f;
        [Range(1, 6)] public int plainOctaves = 3;

        [Header("Serras (bordas) — chapadas e serras arredondadas do sertão")]
        [Tooltip("Distância do centro (superelipse) onde as serras começam a subir.")]
        public float serraStart = 250f;
        [Tooltip("Distância do centro onde as serras atingem a altura total.")]
        public float serraFull = 440f;
        public float serraMinHeight = 55f;
        public float serraMaxExtraHeight = 95f;
        [Tooltip("Tamanho das massas de serra (m). Maior = blocos mais longos.")]
        public float serraWavelength = 320f;
        [Tooltip("Tamanho das regiões que alternam entre chapada (topo plano com paredão) e serra de cristas arredondadas.")]
        public float serraRegionWavelength = 450f;
        [Range(0f, 1f), Tooltip("0 = só serras arredondadas, 1 = só chapadas.")]
        public float chapadaBias = 0.5f;
        [Tooltip("Ondulação do contorno do pé da serra (m).")]
        public float serraFootWobble = 90f;

        [Header("Serras em arco (ferradura vista de cima)")]
        public int arcRidges = 3;
        [Tooltip("Distância do centro do mapa ao centro de cada arco (m).")]
        public float arcCenterDistance = 315f;
        public Vector2 arcRadius = new Vector2(55f, 85f);
        public Vector2 arcSpanDegrees = new Vector2(190f, 250f);
        public Vector2 arcWidth = new Vector2(26f, 38f);
        public Vector2 arcHeight = new Vector2(45f, 80f);

        [Header("Arcos de pedra vazados (malhas)")]
        [Tooltip("Arcos soltos na área jogável, como marco visual.")]
        public int rockArchesInside = 1;
        [Tooltip("Arcos no fundo das serras em ferradura (no máximo um por ferradura).")]
        public int rockArchesInArcRidges = 2;
        public Vector2 rockArchScale = new Vector2(0.9f, 1.15f);
        public Material rockArchMaterial;

        [Header("Inselbergs")]
        public int inselbergsInside = 2;
        public int inselbergsOutside = 3;
        public Vector2 inselbergRadius = new Vector2(30f, 55f);
        public Vector2 inselbergHeight = new Vector2(35f, 75f);
        [Tooltip("Folga mínima entre o inselberg e áreas reservadas/estrada (m).")]
        public float inselbergClearance = 35f;

        [Header("Lajedos (granito plano no nível do chão)")]
        public int lajedoCount = 9;
        public Vector2 lajedoRadius = new Vector2(7f, 16f);
        [Tooltip("Quanto o lajedo sobe acima do chão (m).")]
        public Vector2 lajedoHeight = new Vector2(0.3f, 0.9f);

        [Header("Leito de riacho seco")]
        public Vector2 riverStart = new Vector2(40f, 900f);
        public Vector2 riverEnd = new Vector2(960f, 60f);
        public float riverMeanderAmplitude = 30f;
        public float riverMeanderWavelength = 260f;
        public float riverBedHalfWidth = 6f;
        public float riverBankWidth = 9f;
        public float riverDepth = 2.5f;

        [Header("Estrada")]
        [Tooltip("Faixa de transição lateral da estrada achatada (m).")]
        public float roadBlend = 10f;
        [Tooltip("Janela de suavização da altura ao longo da estrada (m).")]
        public float roadSmoothWindow = 30f;
    }
}
