using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Cobertura alvo da mata (0..1) em cada ponto do mapa: mato fechado por padrão, com clareiras naturais,
    /// faixa de solo exposto na beira da estrada, clareiras na vila e no datacenter, leito do riacho,
    /// lajedos, inselbergs e faces de granito sem vegetação. Usado pela pintura do solo, pelo scatter e pelos detalhes.
    /// Determinístico: mesmas seeds = mesmo campo.
    /// </summary>
    public class VegetationField
    {
        // Parâmetros de forma (m)
        public const float RoadBare = 4f;          // faixa de solo exposto a partir da borda da estrada
        public const float RoadOpen = 12f;         // até aqui, mato aberto (touceiras e cobertura)
        public const float RoadFull = 25f;         // a partir daqui, mata fechada
        public const float AreaBare = 6f;          // clareira em volta da vila/datacenter
        public const float AreaFull = 30f;
        // Corredor da trilha principal (MVP): vegetação só até aqui a partir da borda da estrada
        public const float TrailFull = 30f;        // cobertura cheia até esta distância
        public const float TrailEnd = 45f;         // some por completo aqui

        public readonly Terrain terrain;
        public readonly TerrainData td;
        public readonly Vector3 origin, size;
        public readonly TerrainReliefSettings relief;
        public readonly TerrainPaintSettings paint;
        public readonly TerrainReliefGenerator.Features features;
        public readonly Vector2 center;
        readonly float[,] roadDist, riverDist;
        readonly int res;
        readonly float cell;
        readonly Vector3 texelOrigin;
        readonly Vector2 paintOff, fieldOff;
        readonly float roadHalf;

        public static VegetationField Build(TerrainPaintSettings paint, int res = 1024)
        {
            var terrain = TerrainReliefGenerator.FindTerrain();
            var layoutRoot = TerrainReliefGenerator.FindLayout();
            if (terrain == null || layoutRoot == null || paint == null || paint.relief == null || paint.relief.layout == null)
            {
                Debug.LogError("[Mata] Precisa de Terrain, _Layout e dos assets de pintura/relevo.");
                return null;
            }
            return new VegetationField(paint, terrain, layoutRoot, res);
        }

        VegetationField(TerrainPaintSettings paint, Terrain terrain, Transform layoutRoot, int res)
        {
            this.paint = paint;
            relief = paint.relief;
            this.terrain = terrain;
            td = terrain.terrainData;
            origin = terrain.transform.position;
            size = td.size;
            this.res = res;
            cell = size.x / res;
            texelOrigin = origin + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            features = TerrainReliefGenerator.ComputeFeatures(relief, terrain, layoutRoot);
            center = features.center;
            roadHalf = relief.layout.roadWidth * 0.5f;
            roadDist = features.road.Count > 1
                ? TerrainReliefGenerator.DistanceField(features.road, roadHalf + Mathf.Max(RoadFull, TrailEnd) + 5f, res, cell, texelOrigin, out _)
                : null;
            riverDist = TerrainReliefGenerator.DistanceField(features.river, relief.riverBedHalfWidth + relief.riverBankWidth + 4f, res, cell, texelOrigin, out _);
            paintOff = TerrainPainter.NoiseOffset(paint);
            var rng = new System.Random(paint.seed * 13 + 5);
            fieldOff = new Vector2((float)rng.NextDouble() * 7000f, (float)rng.NextDouble() * 7000f);
        }

        (int x, int y) Cell(Vector2 p) =>
            (Mathf.Clamp(Mathf.RoundToInt((p.x - texelOrigin.x) / cell), 0, res - 1),
             Mathf.Clamp(Mathf.RoundToInt((p.y - texelOrigin.z) / cell), 0, res - 1));

        /// <summary>Distância à borda da estrada (m); grande se longe.</summary>
        public float RoadEdge(Vector2 p)
        {
            if (roadDist == null) return 9999f;
            var (x, y) = Cell(p);
            float d = roadDist[y, x];
            return d == float.MaxValue ? 9999f : d - roadHalf;
        }

        public float RiverDistance(Vector2 p)
        {
            var (x, y) = Cell(p);
            float d = riverDist[y, x];
            return d == float.MaxValue ? 9999f : d;
        }

        public float ReservedDistance(Vector2 p)
        {
            float d = 9999f;
            foreach (var a in features.areas) d = Mathf.Min(d, TerrainReliefGenerator.RectDistance(p, a.c, a.half));
            return d;
        }

        public bool InPlayable(Vector2 p, float margin = 0f) =>
            Mathf.Abs(p.x - center.x) < relief.playableHalfSize + margin && Mathf.Abs(p.y - center.y) < relief.playableHalfSize + margin;

        public float LajedoMask(Vector2 p) => TerrainReliefGenerator.LajedoMask(features, p);

        /// <summary>Corredor da trilha principal: 1 até TrailFull da borda da estrada, 0 a partir de TrailEnd.</summary>
        public float Trail(Vector2 p) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TrailFull, TrailEnd, RoadEdge(p)));

        /// <summary>Cobertura alvo da mata (0 = solo exposto, 1 = mato fechado).</summary>
        public float Cover(Vector2 p)
        {
            float u = (p.x - origin.x) / size.x, v = (p.y - origin.z) / size.z;
            if (u < 0f || v < 0f || u > 1f || v > 1f) return 0f;
            float slope = td.GetSteepness(u, v);
            float hRel = td.GetInterpolatedHeight(u, v) + origin.y - relief.baseHeight;
            float serra = TerrainReliefGenerator.SerraMask(relief, p, center);

            float c = 1f;
            // Clareiras naturais e roçados (manchas bege no mosaico); menos nas serras
            float open = TerrainPainter.Fbm((p + fieldOff) / 110f, 3);
            c *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.575f, 0.64f, open)) * Mathf.Lerp(1f, 0.3f, serra);
            // Textura interna: massas emendadas com falhas menores entre elas
            float body = TerrainPainter.Fbm((p - fieldOff) / 26f, 3);
            c *= Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.58f, body)));

            // Estrada: faixa de solo exposto, depois mato aberto, depois mata fechada
            float rd = RoadEdge(p);
            float roadF = rd < RoadBare ? 0f
                : 0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadBare, RoadOpen, rd))
                  + 0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadOpen, RoadFull, rd));
            c *= roadF;
            // Vila e datacenter: clareiras
            float ad = ReservedDistance(p);
            c *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AreaBare, AreaFull, ad));
            // Leito seco: só o fundo fica limpo (a margem tem mato)
            if (RiverDistance(p) < relief.riverBedHalfWidth + 1f && serra < 0.5f) c = 0f;
            // Lajedos (topo de pedra), inselbergs e faces de granito
            c *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, LajedoMask(p)));
            c *= 1f - TerrainReliefGenerator.InselbergRockMask(features, p);
            c *= 1f - TerrainPainter.RockAmount(paint, p, slope, hRel, paintOff, out _);
            c *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(42f, 54f, slope));
            // Vegetação até perto do topo das serras
            c *= Mathf.Lerp(1f, 0.35f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(100f, 135f, hRel)));
            return Mathf.Clamp01(c);
        }
    }
}
