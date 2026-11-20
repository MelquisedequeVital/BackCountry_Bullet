using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Tamanhos das áreas reservadas do cenário. As posições ficam nos marcadores de _Layout na cena;
    /// este asset guarda só as dimensões e as margens usadas pelos scripts de relevo, pintura e scatter.
    /// </summary>
    [CreateAssetMenu(menuName = "Backcountry Bullet/Caatinga Layout Settings", fileName = "CaatingaLayout")]
    public class CaatingaLayoutSettings : ScriptableObject
    {
        public const string LayoutRootName = "_Layout";
        public const string VilaName = "Area_Vila";
        public const string BarName = "Spot_Bar";
        public const string EstradaName = "Estrada";
        public const string PortaoName = "Spot_Portao";
        public const string DatacenterName = "Area_Datacenter";

        [Header("Áreas reservadas (m)")]
        public Vector2 vilaSize = new Vector2(80f, 80f);
        public Vector2 datacenterSize = new Vector2(120f, 120f);

        [Header("Estrada")]
        [Tooltip("Largura da terra batida (m).")]
        public float roadWidth = 6f;
        [Tooltip("Passo de amostragem da curva da estrada (m).")]
        public float roadSampleStep = 1f;

        [Header("Margens")]
        [Tooltip("Faixa extra em volta das áreas e da estrada onde o relevo faz a transição para o plano (m).")]
        public float flattenBlend = 15f;
        [Tooltip("Distância mínima de vegetação/rochas à borda das áreas e da estrada (m).")]
        public float scatterClearance = 4f;
    }
}
