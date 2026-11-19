using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Pinta o splatmap do Terrain por inclinação, altura, riacho, estrada e áreas reservadas.</summary>
    public static class TerrainPainter
    {
        // Ocre = terra vermelha, Rachado = solo com capim seco (nomes antigos mantidos como índices)
        const int Ocre = 0, Rachado = 1, Cascalho = 2, Areia = 3, Rocha = 4, Batida = 5, Mata = 6, Count = 7;

        public static void Paint(TerrainPaintSettings s)
        {
            var terrain = TerrainReliefGenerator.FindTerrain();
            var layoutRoot = TerrainReliefGenerator.FindLayout();
            if (terrain == null || layoutRoot == null || s.relief == null || s.relief.layout == null)
            {
                Debug.LogError("[Solo] Precisa de Terrain, _Layout e do asset de relevo (com CaatingaLayout).");
                return;
            }
            if (s.layers == null || s.layers.Length != Count || System.Array.Exists(s.layers, l => l == null))
            {
                s.layers = SoilTextureLibrary.EnsureLayers(false);
                EditorUtility.SetDirty(s);
            }

            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Pintar solo da caatinga");
            td.terrainLayers = s.layers;

            int res = td.alphamapResolution;
            Vector3 size = td.size;
            Vector3 origin = terrain.transform.position;
            float cell = size.x / res;
            Vector3 texelOrigin = origin + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            var layout = s.relief.layout;
            float baseH = s.relief.baseHeight;
            var rng = new System.Random(s.seed);
            Vector2 off = new Vector2((float)rng.NextDouble() * 5000f, (float)rng.NextDouble() * 5000f);

            // Distâncias
            var road = RoadPath.Sample(RoadPath.GetControlPoints(layoutRoot.Find(CaatingaLayoutSettings.EstradaName)), layout.roadSampleStep);
            float roadHalf = layout.roadWidth * 0.5f;
            var roadDist = road.Count > 1
                ? TerrainReliefGenerator.DistanceField(road, roadHalf + s.roadShoulder + 3f, res, cell, texelOrigin, out _)
                : null;
            var river = TerrainReliefGenerator.GetRiverPath(s.relief);
            float bankEnd = s.relief.riverBedHalfWidth + s.relief.riverBankWidth;
            var riverDist = TerrainReliefGenerator.DistanceField(river, bankEnd + 2f, res, cell, texelOrigin, out _);
            var vila = layoutRoot.Find(CaatingaLayoutSettings.VilaName);
            var dc = layoutRoot.Find(CaatingaLayoutSettings.DatacenterName);
            Vector2 center = new Vector2(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
            var features = TerrainReliefGenerator.ComputeFeatures(s.relief, terrain, layoutRoot);
            var field = VegetationField.Build(s);

            var maps = new float[res, res, Count];
            var w = new float[Count];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                Vector2 p = new Vector2(texelOrigin.x + x * cell, texelOrigin.z + y * cell);
                float slope = td.GetSteepness(u, v);
                float hRel = td.GetInterpolatedHeight(u, v) + origin.y - baseH;

                // Base: terra vermelha com manchas de capim seco e de chão pedregoso
                System.Array.Clear(w, 0, Count);
                w[Ocre] = 1f;
                float grassy = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.soilGrassMaxSlope * 0.7f, s.soilGrassMaxSlope, slope));
                float grassN = Fbm((p + off) / s.soilGrassPatchScale, 3);
                float grassT = PatchThreshold(s.soilGrassPatchAmount);
                w[Rachado] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grassT, grassT + 0.08f, grassN)) * grassy * 1.4f;
                float gravN = Fbm((p - off) / s.gravelPatchScale, 3);
                float gravT = PatchThreshold(s.gravelPatchAmount);
                float gravel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gravT, gravT + 0.05f, gravN)) * 0.9f
                             + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.gravelSlope.x, s.gravelSlope.y, slope)) * 1.2f;
                w[Cascalho] = gravel;

                // Rocha: encostas íngremes e partes altas, só em manchas (faces de granito expostas)
                float rock = RockAmount(s, p, slope, hRel, off, out float rockPatch);
                Overwrite(w, Rocha, rock);
                // encosta sem granito: chão pedregoso bege
                Overwrite(w, Cascalho, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.rockSlopeStart * 0.8f, s.rockSlopeFull, slope)) * (1f - rockPatch) * 0.7f);
                // inselbergs: pedra nua; lajedos: granito plano com borda de cascalho
                Overwrite(w, Rocha, TerrainReliefGenerator.InselbergRockMask(features, p));
                float laj = TerrainReliefGenerator.LajedoMask(features, p);
                if (laj > 0f)
                {
                    Overwrite(w, Cascalho, Mathf.Clamp01(laj * 3f) * 0.6f);
                    Overwrite(w, Rocha, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, laj)));
                }

                // Chão de mata (folhiço e copa vista de longe) onde a cobertura é alta: dá o mosaico verde-oliva à distância
                if (field != null)
                {
                    float cover = field.Cover(p);
                    Overwrite(w, Mata, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, cover)) * 0.95f);
                }

                // Leito seco: areia no fundo, cascalho na margem
                float rd = riverDist[y, x];
                if (rd != float.MaxValue)
                {
                    float sandEnd = s.relief.riverBedHalfWidth + s.relief.riverBankWidth * s.sandBankFraction;
                    float sand = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sandEnd - 1.5f, sandEnd + 1.5f, rd + (Fbm(p / 8f, 2) - 0.5f) * 3f));
                    float bank = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sandEnd, bankEnd + 1f, rd));
                    float riverFade = 1f - TerrainReliefGenerator.SerraMask(s.relief, p, center);
                    Overwrite(w, Cascalho, bank * 0.6f * riverFade);
                    Overwrite(w, Areia, sand * riverFade);
                }

                // Áreas reservadas: terra batida misturada
                float edgeJitter = (Fbm(p / 15f + off, 2) - 0.5f) * 2f * s.areaEdgeNoise;
                if (vila) Overwrite(w, Batida, AreaWeight(p, vila.position, layout.vilaSize, s.areaEdgeBlend, edgeJitter) * PackedDirt(p, off, s.vilaPackedDirt));
                if (dc) Overwrite(w, Batida, AreaWeight(p, dc.position, layout.datacenterSize, s.areaEdgeBlend, edgeJitter) * PackedDirt(p, off, s.datacenterPackedDirt));

                // Estrada: terra batida com acostamento de cascalho
                if (roadDist != null && roadDist[y, x] != float.MaxValue)
                {
                    float d = roadDist[y, x] + (Fbm(p / 4f - off, 2) - 0.5f) * 2f * s.roadEdgeNoise;
                    float shoulder = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roadHalf, roadHalf + s.roadShoulder, d));
                    Overwrite(w, Cascalho, shoulder * 0.55f);
                    float batida = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roadHalf - 0.6f, roadHalf + 0.6f, d));
                    Overwrite(w, Batida, batida);
                }

                float sum = 0f;
                for (int i = 0; i < Count; i++) sum += w[i];
                for (int i = 0; i < Count; i++) maps[y, x, i] = w[i] / sum;
            }

            td.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(td);
            Debug.Log($"[Solo] Splatmap {res}x{res} pintado com {Count} camadas.");
        }

        /// <summary>Capim seco esparso como detalhe do Terrain: manchas de ruído, fora de estrada, áreas, leito, rocha e encostas.</summary>
        public static void PaintGrass(TerrainPaintSettings s)
        {
            var terrain = TerrainReliefGenerator.FindTerrain();
            var layoutRoot = TerrainReliefGenerator.FindLayout();
            if (terrain == null || layoutRoot == null || s.relief == null || s.grassTexture == null)
            {
                Debug.LogError("[Capim] Precisa de Terrain, _Layout, relevo e da textura de capim no asset de pintura.");
                return;
            }
            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Pintar capim seco");
            td.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    usePrototypeMesh = false,
                    prototypeTexture = s.grassTexture,
                    renderMode = DetailRenderMode.Grass,
                    minWidth = s.grassWidth.x, maxWidth = s.grassWidth.y,
                    minHeight = s.grassHeight.x, maxHeight = s.grassHeight.y,
                    healthyColor = s.grassHealthy, dryColor = s.grassDry,
                    noiseSpread = 0.4f,
                    useDensityScaling = true,
                }
            };
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            int res = td.detailResolution;
            var layout = s.relief.layout;
            Vector3 origin = terrain.transform.position, size = td.size;
            float cell = size.x / res;
            Vector3 texelOrigin = origin + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            var road = RoadPath.Sample(RoadPath.GetControlPoints(layoutRoot.Find(CaatingaLayoutSettings.EstradaName)), layout.roadSampleStep);
            float roadHalf = layout.roadWidth * 0.5f;
            var roadDist = TerrainReliefGenerator.DistanceField(road, roadHalf + 2f, res, cell, texelOrigin, out _);
            var riverDist = TerrainReliefGenerator.DistanceField(TerrainReliefGenerator.GetRiverPath(s.relief), s.relief.riverBedHalfWidth + 1f, res, cell, texelOrigin, out _);
            var vila = layoutRoot.Find(CaatingaLayoutSettings.VilaName);
            var dc = layoutRoot.Find(CaatingaLayoutSettings.DatacenterName);
            var splat = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
            int aRes = td.alphamapWidth;
            var rng = new System.Random(s.seed + 7);
            Vector2 off = new Vector2((float)rng.NextDouble() * 3000f, (float)rng.NextDouble() * 3000f);

            var map = new int[res, res];
            long total = 0;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                Vector2 p = new Vector2(texelOrigin.x + x * cell, texelOrigin.z + y * cell);
                if (roadDist[y, x] != float.MaxValue || riverDist[y, x] != float.MaxValue) continue;
                if (td.GetSteepness(u, v) > s.grassMaxSlope) continue;
                if (vila && TerrainReliefGenerator.RectDistance(p, new Vector2(vila.position.x, vila.position.z), layout.vilaSize * 0.5f) < 2f) continue;
                if (dc && TerrainReliefGenerator.RectDistance(p, new Vector2(dc.position.x, dc.position.z), layout.datacenterSize * 0.5f) < 2f) continue;
                int ax = Mathf.Min(aRes - 1, (int)(u * aRes)), ay = Mathf.Min(aRes - 1, (int)(v * aRes));
                float soil = splat[ay, ax, Ocre] + splat[ay, ax, Rachado] * 0.5f + splat[ay, ax, Cascalho] * 0.6f;
                if (soil < 0.3f) continue;
                float n = Fbm((p + off) / s.grassPatchScale, 3);
                float t = PatchThreshold(s.grassAmount);
                float w = Mathf.InverseLerp(t, t + 0.12f, n) * soil;
                if (w <= 0f) continue;
                int count = Mathf.FloorToInt(w * s.grassMaxPerCell + (float)rng.NextDouble());
                map[y, x] = Mathf.Clamp(count, 0, s.grassMaxPerCell);
                total += map[y, x];
            }
            td.SetDetailLayer(0, 0, 0, map);
            terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 120f);
            EditorUtility.SetDirty(td);
            Debug.Log($"[Capim] {total} tufos de capim seco.");
        }

        /// <summary>Deslocamento de ruído da pintura (mesma seed = mesmo valor). Usado também pelo <see cref="VegetationField"/>.</summary>
        public static Vector2 NoiseOffset(TerrainPaintSettings s)
        {
            var rng = new System.Random(s.seed);
            return new Vector2((float)rng.NextDouble() * 5000f, (float)rng.NextDouble() * 5000f);
        }

        /// <summary>0..1 de granito exposto: encostas íngremes e altas, recortadas em manchas. rockPatch = máscara das manchas.</summary>
        public static float RockAmount(TerrainPaintSettings s, Vector2 p, float slope, float hRel, Vector2 off, out float rockPatch)
        {
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.rockSlopeStart, s.rockSlopeFull, slope));
            rock = Mathf.Max(rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.rockHighAltitude, s.rockHighAltitude + 25f, hRel))
                                   * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 20f, slope)));
            float rockT = PatchThreshold(s.rockPatchAmount);
            rockPatch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rockT - 0.04f, rockT + 0.06f, Fbm(p / s.rockPatchScale - off, 3)));
            rock *= rockPatch * Mathf.Lerp(0.85f, 1.15f, Fbm(p / 25f + off, 2));
            return Mathf.Clamp01(rock);
        }

        /// <summary>Mistura os pesos em direção a uma camada só (prioridade de pintura).</summary>
        static void Overwrite(float[] w, int layer, float amount)
        {
            if (amount <= 0f) return;
            float sum = 0f;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            for (int i = 0; i < w.Length; i++)
            {
                float norm = sum > 0f ? w[i] / sum : 0f;
                w[i] = Mathf.Lerp(norm, i == layer ? 1f : 0f, amount);
            }
        }

        static float AreaWeight(Vector2 p, Vector3 c, Vector2 sizeXZ, float blend, float jitter)
        {
            // distância com sinal aproximada: negativa dentro, para a borda irregular poder entrar na área
            Vector2 local = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.z)) - sizeXZ * 0.5f;
            float d = local.x > 0f || local.y > 0f
                ? new Vector2(Mathf.Max(local.x, 0f), Mathf.Max(local.y, 0f)).magnitude
                : Mathf.Max(local.x, local.y);
            d += jitter;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blend * 0.5f, blend, d));
        }

        static float PackedDirt(Vector2 p, Vector2 off, float amount)
        {
            float n = Fbm(p / 12f + off * 0.3f, 3);
            return Mathf.Clamp01(amount + (n - 0.5f) * 0.6f);
        }

        /// <summary>fBm de Perlin fica quase todo entre 0,35 e 0,65: mapeia "quantidade" 0..1 para esse intervalo.</summary>
        public static float PatchThreshold(float amount) => Mathf.Lerp(0.64f, 0.36f, amount);

        public static float Fbm(Vector2 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++) { sum += Mathf.PerlinNoise(p.x, p.y) * amp; norm += amp; p *= 2.03f; amp *= 0.5f; }
            return sum / norm;
        }
    }
}
