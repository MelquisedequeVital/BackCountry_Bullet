using System.Collections.Generic;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Conjunto ordenado de regras de scatter. A ordem importa: categorias anteriores ocupam espaço primeiro.</summary>
    [CreateAssetMenu(menuName = "Backcountry Bullet/Scatter Set", fileName = "Scatter_Set")]
    public class ScatterSet : ScriptableObject
    {
        public TerrainReliefSettings relief;
        [Tooltip("Pintura do solo: necessária para o campo de cobertura da mata (VegetationField).")]
        public TerrainPaintSettings paint;
        public int seed = 2024;
        public List<ScatterRule> rules = new List<ScatterRule>();
    }
}
