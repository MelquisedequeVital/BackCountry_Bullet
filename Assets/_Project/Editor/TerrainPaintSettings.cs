using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Regras de pintura do solo. Ordem dos layers = <see cref="SoilTextureLibrary.Soil"/>.</summary>
    [CreateAssetMenu(menuName = "Backcountry Bullet/Terrain Paint Settings", fileName = "CaatingaPaint")]
    public class TerrainPaintSettings : ScriptableObject
    {
        public TerrainReliefSettings relief;
        public int seed = 404;

        [Header("TerrainLayers (terra vermelha, capim seco, cascalho, areia, rocha, terra batida, chão de mata)")]
        public TerrainLayer[] layers = new TerrainLayer[7];

        [Header("Rocha nas encostas (granito exposto em manchas)")]
        [Tooltip("Inclinação (graus) onde a rocha começa a aparecer.")]
        public float rockSlopeStart = 30f;
        [Tooltip("Inclinação (graus) onde é só rocha.")]
        public float rockSlopeFull = 42f;
        [Tooltip("Acima desta altura relativa à planície (m), rocha aparece já em inclinações médias.")]
        public float rockHighAltitude = 70f;
        [Range(0f, 1f), Tooltip("Fração das encostas íngremes que mostra granito (o resto fica com solo e vegetação).")]
        public float rockPatchAmount = 0.45f;
        public float rockPatchScale = 45f;

        [Header("Cascalho")]
        public Vector2 gravelSlope = new Vector2(12f, 24f);
        [Range(0f, 1f)] public float gravelPatchAmount = 0.35f;
        public float gravelPatchScale = 40f;
        [Tooltip("Faixa de cascalho na beira da estrada (m).")]
        public float roadShoulder = 2.5f;

        [Header("Solo com capim seco e gravetos")]
        [Range(0f, 1f)] public float soilGrassPatchAmount = 0.45f;
        public float soilGrassPatchScale = 35f;
        [Tooltip("Capim no solo até esta inclinação (graus).")]
        public float soilGrassMaxSlope = 28f;

        [Header("Leito seco")]
        [Tooltip("Fração da margem que ainda recebe areia (0 = só o fundo).")]
        [Range(0f, 1f)] public float sandBankFraction = 0.25f;

        [Header("Áreas reservadas")]
        [Range(0f, 1f)] public float vilaPackedDirt = 0.65f;
        [Range(0f, 1f)] public float datacenterPackedDirt = 0.9f;
        public float areaEdgeBlend = 6f;
        [Tooltip("Irregularidade da borda das áreas reservadas (m).")]
        public float areaEdgeNoise = 5f;

        [Header("Estrada")]
        [Tooltip("Irregularidade da borda da terra batida (m).")]
        public float roadEdgeNoise = 0.6f;

        [Header("Capim seco (detalhe do Terrain)")]
        public Texture2D grassTexture;
        [Tooltip("Máximo de tufos por célula de detalhe (~1 m²).")]
        [Range(0, 8)] public int grassMaxPerCell = 2;
        [Range(0f, 1f), Tooltip("Fração do chão coberta por manchas de capim.")]
        public float grassAmount = 0.28f;
        public float grassPatchScale = 25f;
        public float grassMaxSlope = 22f;
        public Vector2 grassWidth = new Vector2(0.5f, 0.9f);
        public Vector2 grassHeight = new Vector2(0.35f, 0.7f);
        public Color grassHealthy = new Color(1f, 0.96f, 0.88f);
        public Color grassDry = new Color(0.9f, 0.8f, 0.66f);
    }
}
