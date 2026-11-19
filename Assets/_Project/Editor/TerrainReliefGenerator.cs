using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Gera o heightmap da caatinga de forma determinística a partir de <see cref="TerrainReliefSettings"/>.</summary>
    public static class TerrainReliefGenerator
    {
        public const string SceneryRootName = "Cenario";
        public const string ArchesRootName = "Arcos_Pedra";

        public struct Inselberg
        {
            public Vector2 center;
            public float radius, height, angle, stretch;
        }

        /// <summary>Lajedo: afloramento de granito plano e largo, pouco acima do chão.</summary>
        public struct Lajedo
        {
            public Vector2 center;
            public float radius, height, angle, stretch;
        }

        /// <summary>Feições determinísticas do relevo (mesma seed = mesma lista), usadas também na pintura e no scatter.</summary>
        public class Features
        {
            public Vector2 center, noiseOff, serraOff;
            public List<Vector3> road, river;
            public List<(Vector2 c, Vector2 half)> areas = new List<(Vector2, Vector2)>();
            public List<ArcRidge> arcs;
            public List<Vector2> archInside;
            public List<Inselberg> inselbergs;
            public List<Lajedo> lajedos;
            public System.Random rng;
        }

        public static Features ComputeFeatures(TerrainReliefSettings s, Terrain terrain, Transform layoutRoot)
        {
            Vector3 size = terrain.terrainData.size, origin = terrain.transform.position;
            var f = new Features { rng = new System.Random(s.seed) };
            var rng = f.rng;
            f.noiseOff = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
            f.serraOff = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
            f.center = new Vector2(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
            var vila = layoutRoot.Find(CaatingaLayoutSettings.VilaName);
            var dc = layoutRoot.Find(CaatingaLayoutSettings.DatacenterName);
            f.road = RoadPath.Sample(RoadPath.GetControlPoints(layoutRoot.Find(CaatingaLayoutSettings.EstradaName)), s.layout.roadSampleStep);
            f.river = GetRiverPath(s);
            if (vila) f.areas.Add((XZ(vila.position), s.layout.vilaSize * 0.5f));
            if (dc) f.areas.Add((XZ(dc.position), s.layout.datacenterSize * 0.5f));
            f.arcs = PlaceArcRidges(s, rng, f.center, f.areas, f.road);
            f.archInside = PickInsideArchSpots(s, rng, f.center, f.areas, f.road, f.river, f.arcs);
            f.inselbergs = PlaceInselbergs(s, rng, f.center, f.areas, f.road, f.river, f.arcs, f.archInside);
            f.lajedos = PlaceLajedos(s, new System.Random(s.seed * 7 + 3), f);
            return f;
        }

        /// <summary>0..1: quanto o ponto está sobre o topo de um lajedo.</summary>
        public static float LajedoMask(Features f, Vector2 p)
        {
            float m = 0f;
            foreach (var l in f.lajedos) m = Mathf.Max(m, LajedoShape(l, p).top);
            return m;
        }

        /// <summary>0..1: quanto o ponto está na rocha nua de um inselberg (domo, sem o pé).</summary>
        public static float InselbergRockMask(Features f, Vector2 p)
        {
            float m = 0f;
            foreach (var ib in f.inselbergs) m = Mathf.Max(m, Mathf.InverseLerp(0.12f, 0.3f, InselbergHeight(ib, p) / ib.height));
            return m;
        }

        /// <summary>Serra em ferradura: anel parcial em volta de <c>center</c>, aberto para o centro do mapa.</summary>
        public struct ArcRidge
        {
            public Vector2 center;
            public float radius, width, height, backAngle, halfSpan, phase;
        }

        public static Terrain FindTerrain() => Object.FindAnyObjectByType<Terrain>();

        public static Transform FindLayout()
        {
            var go = GameObject.Find(CaatingaLayoutSettings.LayoutRootName);
            return go ? go.transform : null;
        }

        public static void Generate(TerrainReliefSettings s)
        {
            var terrain = FindTerrain();
            var layoutRoot = FindLayout();
            if (terrain == null || layoutRoot == null || s.layout == null)
            {
                Debug.LogError("[Relevo] Precisa de um Terrain, do objeto _Layout na cena e do CaatingaLayout no asset de relevo.");
                return;
            }

            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Gerar relevo da caatinga");
            int res = td.heightmapResolution;
            Vector3 size = td.size;
            Vector3 origin = terrain.transform.position;
            float cell = size.x / (res - 1);
            var f = ComputeFeatures(s, terrain, layoutRoot);
            var rng = f.rng;
            Vector2 noiseOff = f.noiseOff, serraOff = f.serraOff, center = f.center;
            var roadSamples = f.road; var river = f.river; var areas = f.areas;
            var arcs = f.arcs; var archInside = f.archInside; var inselbergs = f.inselbergs;

            // 1) Relevo natural
            var h = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector2 p = new Vector2(origin.x + x * cell, origin.z + y * cell);
                h[y, x] = NaturalHeight(s, p, center, noiseOff, serraOff, inselbergs, arcs);
                foreach (var l in f.lajedos) h[y, x] += LajedoShape(l, p).height;
            }

            // 2) Leito do riacho (some sob as serras)
            var riverDist = DistanceField(river, s.riverBedHalfWidth + s.riverBankWidth + 2f, res, cell, origin, out _);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float d = riverDist[y, x];
                if (d == float.MaxValue) continue;
                Vector2 p = new Vector2(origin.x + x * cell, origin.z + y * cell);
                float carve = s.riverDepth * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.riverBedHalfWidth, s.riverBedHalfWidth + s.riverBankWidth, d)));
                h[y, x] -= carve * (1f - SerraMask(s, p, center));
            }

            // 3) Estrada: plana na seção transversal, suavizada ao longo do caminho
            float roadHalf = s.layout.roadWidth * 0.5f;
            if (roadSamples.Count > 1)
            {
                var roadTarget = RoadTargets(s, roadSamples, h, res, cell, origin);
                var roadDist = DistanceField(roadSamples, roadHalf + 1f + s.roadBlend, res, cell, origin, out var roadIdx);
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = roadDist[y, x];
                    if (d == float.MaxValue) continue;
                    float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roadHalf + 1f, roadHalf + 1f + s.roadBlend, d));
                    h[y, x] = Mathf.Lerp(h[y, x], roadTarget[roadIdx[y, x]], w);
                }
            }

            // 4) Áreas reservadas planas na altura base
            float blend = s.layout.flattenBlend;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector2 p = new Vector2(origin.x + x * cell, origin.z + y * cell);
                foreach (var a in areas)
                {
                    float d = RectDistance(p, a.c, a.half);
                    if (d >= blend) continue;
                    float w = 1f - Mathf.SmoothStep(0f, 1f, d / blend);
                    h[y, x] = Mathf.Lerp(h[y, x], s.baseHeight, w);
                }
            }

            // Grava (normalizado)
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                min = Mathf.Min(min, h[y, x]); max = Mathf.Max(max, h[y, x]);
                h[y, x] = Mathf.Clamp01((h[y, x] - origin.y) / size.y);
            }
            td.SetHeights(0, 0, h);
            terrain.Flush();
            EditorUtility.SetDirty(td);

            SnapLayoutToTerrain(layoutRoot, terrain);
            int archCount = PlaceRockArches(s, rng, terrain, center, roadSamples, arcs, archInside);
            Debug.Log($"[Relevo] seed {s.seed}: altura {min:F1}–{max:F1} m, {arcs.Count} serras em arco, {inselbergs.Count} inselbergs, {f.lajedos.Count} lajedos, {archCount} arcos de pedra, riacho {RoadPath.Length(river):F0} m.");
        }

        // ---------------------------------------------------------------- relevo

        static float NaturalHeight(TerrainReliefSettings s, Vector2 p, Vector2 center, Vector2 noiseOff, Vector2 serraOff,
            List<Inselberg> inselbergs, List<ArcRidge> arcs)
        {
            // Ondulação suave
            float plain = (Fbm((p + noiseOff) / s.plainWavelength, s.plainOctaves) - 0.5f) * 2f * s.plainAmplitude;

            float mask = SerraMask(s, p, center);
            float serra = 0f;
            if (mask > 0f)
            {
                Vector2 warp = new Vector2(
                    Mathf.PerlinNoise((p.x + serraOff.x) / 260f, (p.y + 71f) / 260f) - 0.5f,
                    Mathf.PerlinNoise((p.x + 133f) / 260f, (p.y + serraOff.y) / 260f) - 0.5f) * 160f;
                Vector2 q = p + warp + serraOff;
                float mass = Fbm(q / s.serraWavelength, 2);                        // massas de serra
                float e = mask * Mathf.Lerp(0.55f, 1.05f, mass);
                e += (Mathf.PerlinNoise(q.x / 55f, q.y / 55f) - 0.5f) * 0.09f * mask; // paredão recortado
                float chapada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.62f,
                    Mathf.PerlinNoise((p.x + serraOff.y) / s.serraRegionWavelength, (p.y + serraOff.x) / s.serraRegionWavelength) + (s.chapadaBias - 0.5f)));
                float bumps = BillowFbm(q / 85f, 3);
                float peak = s.serraMinHeight + s.serraMaxExtraHeight *
                             Mathf.PerlinNoise((p.x + serraOff.y) / 380f, (p.y - serraOff.x) / 380f);
                serra = peak * SerraProfile(Mathf.Clamp01(e), chapada, bumps);
            }

            float arc = 0f;
            foreach (var r in arcs) arc = Mathf.Max(arc, ArcRidgeHeight(r, p));

            float insel = 0f;
            foreach (var ib in inselbergs) insel = Mathf.Max(insel, InselbergHeight(ib, p));

            return s.baseHeight + plain * (1f - mask) + Mathf.Max(serra, arc) + insel;
        }

        /// <summary>
        /// Perfil de serra do sertão. Chapada: pedimento suave, paredão íngreme e topo plano.
        /// Serra arredondada: subida contínua com cristas em domo (granito).
        /// </summary>
        static float SerraProfile(float e, float chapada, float bumps)
        {
            float rounded = Mathf.Pow(Mathf.SmoothStep(0f, 1f, e), 1.3f) * Mathf.Lerp(0.75f, 1.1f, bumps);
            float mesa = 0.2f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.45f, e))
                       + 0.65f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.52f, e))
                       + 0.15f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, e)) * Mathf.Lerp(0.6f, 1f, bumps);
            return Mathf.Lerp(rounded, mesa, chapada);
        }

        static float ArcRidgeHeight(ArcRidge r, Vector2 p)
        {
            // Anfiteatro (boqueirão) recortado numa chapada: paredão em ferradura voltado para a planície,
            // topo da chapada continuando para trás até se fundir com as serras da borda.
            Vector2 v = p - r.center;
            float dist = v.magnitude;
            float w = r.width;
            if (dist > r.radius * 1.25f + w * 2.5f + 160f || dist < r.radius * 0.7f - w * 1.3f) return 0f;
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float ad = Mathf.Abs(Mathf.DeltaAngle(ang, r.backAngle));
            float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r.halfSpan - 60f, r.halfSpan + 15f, ad));
            if (taper <= 0f) return 0f;
            // raio irregular: reentrâncias e esporões no paredão
            float rn = r.radius * (1f + 0.22f * (BillowFbm(new Vector2(ang / 28f + r.phase, r.phase * 0.5f), 3) - 0.5f));
            float x = dist - rn;                                  // < 0: dentro do anfiteatro
            float backFade = 1f - 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(w * 2.5f, w * 2.5f + 160f, x));
            if (x < -w * 1.3f) return 0f;
            float talus = 0.16f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-w * 1.3f, -w * 0.35f, x));
            float cliff = 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-w * 0.35f, w * 0.12f, x));
            float rise = 0.14f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(w * 0.12f, w * 1.8f, x));
            float along = Mathf.PerlinNoise(ang / 45f + r.phase, r.phase);
            float bumps = BillowFbm(p / 40f + new Vector2(r.phase * 10f, 0f), 3);
            float hgt = r.height * Mathf.Lerp(0.8f, 1.2f, along) * (talus + cliff + rise * Mathf.Lerp(0.5f, 1.3f, bumps)) * backFade;
            return hgt * Mathf.Pow(taper, 0.6f);
        }

        static float InselbergHeight(Inselberg ib, Vector2 p)
        {
            Vector2 d = p - ib.center;
            if (d.sqrMagnitude > ib.radius * ib.radius * 4f) return 0f;
            float ca = Mathf.Cos(ib.angle), sa = Mathf.Sin(ib.angle);
            Vector2 local = new Vector2(d.x * ca - d.y * sa, (d.x * sa + d.y * ca) / ib.stretch);
            float ang = Mathf.Atan2(local.y, local.x);
            float wobble = 1f + 0.18f * (Mathf.PerlinNoise(ib.center.x * 0.01f + Mathf.Cos(ang) * 1.3f, ib.center.y * 0.01f + Mathf.Sin(ang) * 1.3f) - 0.5f) * 2f;
            Vector2 skewed = local + new Vector2(ib.radius * 0.18f, 0f) * (1f - Mathf.Clamp01(local.magnitude / ib.radius));
            float r = skewed.magnitude / (ib.radius * wobble);
            float dome = r < 1f ? Mathf.Pow(1f - r * r, 0.7f) : 0f;
            float foot = 0.14f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1.8f, r)));
            float cracks = RidgedFbm(new Vector2(p.x + ib.center.y, p.y + ib.center.x) / 22f, 3);
            float rough = 1f - 0.16f * cracks - 0.06f * Mathf.PerlinNoise(p.x * 0.15f, p.y * 0.15f);
            return ib.height * Mathf.Max(dome * rough, foot);
        }

        /// <summary>Altura acrescentada pelo lajedo e máscara do topo (rocha exposta).</summary>
        static (float height, float top) LajedoShape(Lajedo l, Vector2 p)
        {
            Vector2 d = p - l.center;
            if (d.sqrMagnitude > l.radius * l.radius * 2.5f) return (0f, 0f);
            float ca = Mathf.Cos(l.angle), sa = Mathf.Sin(l.angle);
            Vector2 local = new Vector2(d.x * ca - d.y * sa, (d.x * sa + d.y * ca) / l.stretch);
            float ang = Mathf.Atan2(local.y, local.x);
            float edge = 1f + 0.28f * (Mathf.PerlinNoise(l.center.x * 0.013f + Mathf.Cos(ang) * 1.7f, l.center.y * 0.013f + Mathf.Sin(ang) * 1.7f) - 0.5f) * 2f;
            float r = local.magnitude / (l.radius * edge);
            float top = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1.02f, r));
            float bulge = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(1f - r * r)) * (1f + 0.15f * (Mathf.PerlinNoise(p.x * 0.2f, p.y * 0.2f) - 0.5f));
            return (l.height * top * bulge, top);
        }

        static List<Lajedo> PlaceLajedos(TerrainReliefSettings s, System.Random rng, Features f)
        {
            var list = new List<Lajedo>();
            for (int tries = 0; list.Count < s.lajedoCount && tries < 2000; tries++)
            {
                Vector2 c = f.center + new Vector2(Mathf.Lerp(-1f, 1f, (float)rng.NextDouble()), Mathf.Lerp(-1f, 1f, (float)rng.NextDouble())) * s.playableHalfSize * 1.1f;
                var l = new Lajedo
                {
                    center = c,
                    radius = Mathf.Lerp(s.lajedoRadius.x, s.lajedoRadius.y, (float)rng.NextDouble()),
                    height = Mathf.Lerp(s.lajedoHeight.x, s.lajedoHeight.y, (float)rng.NextDouble()),
                    angle = (float)rng.NextDouble() * Mathf.PI,
                    stretch = Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble())
                };
                float need = l.radius * 1.3f;
                bool ok = SerraMask(s, c, f.center) < 0.05f;
                foreach (var a in f.areas) if (ok && RectDistance(c, a.c, a.half) < need + 15f) ok = false;
                if (ok && f.road.Count > 1 && RoadPath.DistanceXZ(f.road, new Vector3(c.x, 0, c.y)) < need + s.layout.roadWidth * 0.5f + 8f) ok = false;
                if (ok && RoadPath.DistanceXZ(f.river, new Vector3(c.x, 0, c.y)) < need + s.riverBedHalfWidth + s.riverBankWidth) ok = false;
                foreach (var ib in f.inselbergs) if (ok && Vector2.Distance(ib.center, c) < ib.radius * 1.8f + need) ok = false;
                foreach (var o in list) if (ok && Vector2.Distance(o.center, c) < (o.radius + l.radius) * 1.6f + 25f) ok = false;
                if (ok) list.Add(l);
            }
            return list;
        }

        public static float SerraMask(TerrainReliefSettings s, Vector2 p, Vector2 center)
        {
            // Superelipse (p=4): quadrado de cantos arredondados, com pé irregular
            float dx = Mathf.Abs(p.x - center.x), dz = Mathf.Abs(p.y - center.y);
            float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dz, 4f), 0.25f);
            d += (Mathf.PerlinNoise(p.x / 130f + s.seed * 0.37f, p.y / 130f) - 0.5f) * s.serraFootWobble;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.serraStart, s.serraFull, d));
        }

        // ---------------------------------------------------------------- posicionamento

        static List<ArcRidge> PlaceArcRidges(TerrainReliefSettings s, System.Random rng, Vector2 center,
            List<(Vector2 c, Vector2 half)> areas, List<Vector3> road)
        {
            var list = new List<ArcRidge>();
            float start = (float)rng.NextDouble() * 360f;
            for (int k = 0; k < s.arcRidges; k++)
            {
                float baseAng = start + k * 360f / Mathf.Max(1, s.arcRidges) + Mathf.Lerp(-20f, 20f, (float)rng.NextDouble());
                var r = new ArcRidge
                {
                    radius = Mathf.Lerp(s.arcRadius.x, s.arcRadius.y, (float)rng.NextDouble()),
                    width = Mathf.Lerp(s.arcWidth.x, s.arcWidth.y, (float)rng.NextDouble()),
                    height = Mathf.Lerp(s.arcHeight.x, s.arcHeight.y, (float)rng.NextDouble()),
                    halfSpan = Mathf.Lerp(s.arcSpanDegrees.x, s.arcSpanDegrees.y, (float)rng.NextDouble()) * 0.5f,
                    phase = (float)rng.NextDouble() * 100f
                };
                for (int tryi = 0; tryi < 24; tryi++)
                {
                    float a = (baseAng + tryi * 15f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float kk = Mathf.Pow(Mathf.Pow(Mathf.Abs(dir.x), 4f) + Mathf.Pow(Mathf.Abs(dir.y), 4f), 0.25f);
                    r.center = center + dir * s.arcCenterDistance / kk;
                    Vector2 toCenter = center - r.center;
                    r.backAngle = Mathf.Atan2(-toCenter.y, -toCenter.x) * Mathf.Rad2Deg; // costas viradas para fora
                    if (ArcIsClear(s, r, areas, road, list)) { list.Add(r); break; }
                }
            }
            return list;
        }

        static bool ArcIsClear(TerrainReliefSettings s, ArcRidge r, List<(Vector2 c, Vector2 half)> areas, List<Vector3> road, List<ArcRidge> others)
        {
            for (float ad = -r.halfSpan; ad <= r.halfSpan; ad += 5f)
            {
                float a = (r.backAngle + ad) * Mathf.Deg2Rad;
                Vector2 pt = r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.radius;
                float need = r.width + s.layout.flattenBlend + 10f;
                foreach (var ar in areas) if (RectDistance(pt, ar.c, ar.half) < need) return false;
                if (road.Count > 1 && RoadPath.DistanceXZ(road, new Vector3(pt.x, 0, pt.y)) < need) return false;
            }
            foreach (var o in others) if (Vector2.Distance(o.center, r.center) < o.radius + r.radius + o.width + r.width) return false;
            return true;
        }

        static List<Vector2> PickInsideArchSpots(TerrainReliefSettings s, System.Random rng, Vector2 center,
            List<(Vector2 c, Vector2 half)> areas, List<Vector3> road, List<Vector3> river, List<ArcRidge> arcs)
        {
            var spots = new List<Vector2>();
            for (int tries = 0; spots.Count < s.rockArchesInside && tries < 500; tries++)
            {
                Vector2 c = center + new Vector2(Mathf.Lerp(-1f, 1f, (float)rng.NextDouble()), Mathf.Lerp(-1f, 1f, (float)rng.NextDouble())) * s.playableHalfSize * 0.85f;
                bool ok = true;
                foreach (var a in areas) if (RectDistance(c, a.c, a.half) < 45f) ok = false;
                if (ok && road.Count > 1 && RoadPath.DistanceXZ(road, new Vector3(c.x, 0, c.y)) < 35f) ok = false;
                if (ok && road.Count > 1 && RoadPath.DistanceXZ(road, new Vector3(c.x, 0, c.y)) > 110f) ok = false; // visível da estrada
                if (ok && RoadPath.DistanceXZ(river, new Vector3(c.x, 0, c.y)) < 30f) ok = false;
                foreach (var o in spots) if (ok && Vector2.Distance(o, c) < 120f) ok = false;
                if (ok) spots.Add(c);
            }
            return spots;
        }

        static List<Inselberg> PlaceInselbergs(TerrainReliefSettings s, System.Random rng, Vector2 center,
            List<(Vector2 c, Vector2 half)> areas, List<Vector3> road, List<Vector3> river, List<ArcRidge> arcs, List<Vector2> archSpots)
        {
            var list = new List<Inselberg>();
            void Place(int count, float minR, float maxR)
            {
                for (int placed = 0, tries = 0; placed < count && tries < 500; tries++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                    Vector2 dirv = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float k = Mathf.Pow(Mathf.Pow(Mathf.Abs(dirv.x), 4f) + Mathf.Pow(Mathf.Abs(dirv.y), 4f), 0.25f);
                    Vector2 c = center + dirv * dist / k;
                    var ib = new Inselberg
                    {
                        center = c,
                        radius = Mathf.Lerp(s.inselbergRadius.x, s.inselbergRadius.y, (float)rng.NextDouble()),
                        height = Mathf.Lerp(s.inselbergHeight.x, s.inselbergHeight.y, (float)rng.NextDouble()),
                        angle = (float)rng.NextDouble() * Mathf.PI,
                        stretch = Mathf.Lerp(0.65f, 1f, (float)rng.NextDouble())
                    };
                    float need = ib.radius * 1.7f;
                    bool ok = true;
                    foreach (var a in areas) if (RectDistance(c, a.c, a.half) < need + s.inselbergClearance) { ok = false; break; }
                    if (ok && road.Count > 1 && RoadPath.DistanceXZ(road, new Vector3(c.x, 0, c.y)) < need + s.inselbergClearance) ok = false;
                    if (ok && RoadPath.DistanceXZ(river, new Vector3(c.x, 0, c.y)) < need + s.riverBedHalfWidth + s.riverBankWidth) ok = false;
                    foreach (var o in list) if (ok && Vector2.Distance(o.center, c) < (o.radius + ib.radius) * 1.8f + 20f) ok = false;
                    foreach (var r in arcs) if (ok && Vector2.Distance(r.center, c) < r.radius + r.width + need) ok = false;
                    foreach (var sp in archSpots) if (ok && Vector2.Distance(sp, c) < need + 40f) ok = false;
                    if (!ok) continue;
                    list.Add(ib); placed++;
                }
            }
            Place(s.inselbergsInside, s.playableHalfSize * 0.35f, s.playableHalfSize * 0.9f);
            Place(s.inselbergsOutside, s.serraStart * 1.2f, s.serraStart * 1.5f); // ao fundo, saindo do pé das serras
            return list;
        }

        static int PlaceRockArches(TerrainReliefSettings s, System.Random rng, Terrain terrain, Vector2 center,
            List<Vector3> road, List<ArcRidge> arcs, List<Vector2> insideSpots)
        {
            var scenery = GameObject.Find(SceneryRootName);
            if (scenery == null) { scenery = new GameObject(SceneryRootName); Undo.RegisterCreatedObjectUndo(scenery, "Cenario"); }
            var root = scenery.transform.Find(ArchesRootName);
            if (root == null)
            {
                var go = new GameObject(ArchesRootName);
                Undo.RegisterCreatedObjectUndo(go, "Arcos");
                go.transform.SetParent(scenery.transform, false);
                root = go.transform;
            }
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            if (s.rockArchMaterial == null) { s.rockArchMaterial = RockArchBuilder.EnsureMaterial(); EditorUtility.SetDirty(s); }
            var prefabs = RockArchBuilder.EnsurePrefabs(s.rockArchMaterial, false);
            if (prefabs.Count == 0) return 0;

            int count = 0;
            void Spawn(Vector2 pos, Vector2 lookDir)
            {
                var prefab = prefabs[rng.Next(prefabs.Count)];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                Undo.RegisterCreatedObjectUndo(inst, "Arco de pedra");
                float scale = Mathf.Lerp(s.rockArchScale.x, s.rockArchScale.y, (float)rng.NextDouble());
                var rot = Quaternion.LookRotation(new Vector3(lookDir.x, 0f, lookDir.y).normalized, Vector3.up);
                // pé no ponto mais baixo sob as pernas
                var bounds = prefab.GetComponent<MeshFilter>().sharedMesh.bounds;
                float half = bounds.extents.x * scale;
                Vector3 c3 = new Vector3(pos.x, 0f, pos.y);
                Vector3 right = rot * Vector3.right;
                float y = Mathf.Min(terrain.SampleHeight(c3 - right * half), terrain.SampleHeight(c3 + right * half), terrain.SampleHeight(c3));
                inst.transform.SetPositionAndRotation(new Vector3(pos.x, y + terrain.transform.position.y - 1.5f * scale, pos.y), rot);
                inst.transform.localScale = Vector3.one * scale;
                inst.name = $"{prefab.name}_{count:00}";
                count++;
            }

            foreach (var sp in insideSpots)
            {
                Vector3 nearest = NearestPoint(road, new Vector3(sp.x, 0, sp.y));
                Spawn(sp, new Vector2(nearest.x - sp.x, nearest.z - sp.y)); // olhar da estrada atravessa o arco
            }
            for (int i = 0; i < Mathf.Min(s.rockArchesInArcRidges, arcs.Count); i++)
            {
                var r = arcs[i];
                float back = r.backAngle * Mathf.Deg2Rad;
                Vector2 pos = r.center + new Vector2(Mathf.Cos(back), Mathf.Sin(back)) * r.radius * 0.25f;
                Spawn(pos, center - pos);
            }
            return count;
        }

        // ---------------------------------------------------------------- utilitários

        /// <summary>Polilinha do leito seco (determinística pela seed). Usada também na pintura do solo.</summary>
        public static List<Vector3> GetRiverPath(TerrainReliefSettings s)
        {
            var pts = new List<Vector3>();
            var rng = new System.Random(s.seed * 31 + 7);
            float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            float off = (float)rng.NextDouble() * 1000f;
            Vector2 a = s.riverStart, b = s.riverEnd;
            Vector2 dir = (b - a).normalized, perp = new Vector2(-dir.y, dir.x);
            float len = Vector2.Distance(a, b);
            int n = Mathf.CeilToInt(len / 4f);
            for (int i = 0; i <= n; i++)
            {
                float along = i / (float)n * len;
                float meander = Mathf.Sin(along / s.riverMeanderWavelength * Mathf.PI * 2f + phase) * 0.65f
                              + (Mathf.PerlinNoise(along / (s.riverMeanderWavelength * 0.45f), off) - 0.5f) * 0.9f;
                Vector2 q = a + dir * along + perp * meander * s.riverMeanderAmplitude;
                pts.Add(new Vector3(q.x, 0f, q.y));
            }
            return pts;
        }

        static float[] RoadTargets(TerrainReliefSettings s, List<Vector3> samples, float[,] h, int res, float cell, Vector3 origin)
        {
            int n = samples.Count;
            var raw = new float[n];
            for (int i = 0; i < n; i++)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt((samples[i].x - origin.x) / cell), 0, res - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((samples[i].z - origin.z) / cell), 0, res - 1);
                raw[i] = h[y, x];
            }
            int win = Mathf.Max(1, Mathf.RoundToInt(s.roadSmoothWindow * 0.5f / Mathf.Max(0.1f, s.layout.roadSampleStep)));
            var target = new float[n];
            float total = RoadPath.Length(samples), acc = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) acc += Vector3.Distance(samples[i - 1], samples[i]);
                float sum = 0f; int cnt = 0;
                for (int j = Mathf.Max(0, i - win); j <= Mathf.Min(n - 1, i + win); j++) { sum += raw[j]; cnt++; }
                float endW = Mathf.SmoothStep(0f, 1f, Mathf.Min(acc, total - acc) / 25f);
                target[i] = Mathf.Lerp(s.baseHeight, sum / cnt, endW);
            }
            return target;
        }

        /// <summary>Distância (XZ) até a polilinha, só dentro do raio. Fora: float.MaxValue. nearest = índice do ponto mais próximo.</summary>
        public static float[,] DistanceField(List<Vector3> pts, float radius, int res, float cell, Vector3 origin, out int[,] nearest)
        {
            var dist = new float[res, res];
            nearest = new int[res, res];
            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) dist[y, x] = float.MaxValue;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = XZ(pts[i - 1]), b = XZ(pts[i]);
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - origin.x) / cell));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius - origin.x) / cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius - origin.z) / cell));
                int y1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + radius - origin.z) / cell));
                Vector2 ab = b - a; float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(origin.x + x * cell, origin.z + y * cell);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    if (d < radius && d < dist[y, x]) { dist[y, x] = d; nearest[y, x] = t < 0.5f ? i - 1 : i; }
                }
            }
            return dist;
        }

        static Vector3 NearestPoint(List<Vector3> pts, Vector3 p)
        {
            Vector3 best = pts.Count > 0 ? pts[0] : p; float bd = float.MaxValue;
            foreach (var q in pts) { float d = (new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude; if (d < bd) { bd = d; best = q; } }
            return best;
        }

        public static float RectDistance(Vector2 p, Vector2 c, Vector2 half)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - half.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(p.y - c.y) - half.y, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void SnapLayoutToTerrain(Transform root, Terrain terrain)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t == root) continue;
                Undo.RecordObject(t, "Ajustar marcadores ao terreno");
                var p = t.position;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                t.position = p;
            }
        }

        static float Fbm(Vector2 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++) { sum += Mathf.PerlinNoise(p.x, p.y) * amp; norm += amp; p *= 2.03f; amp *= 0.5f; }
            return sum / norm;
        }

        /// <summary>Ruído "billow": bolhas arredondadas, bom para cristas de granito.</summary>
        static float BillowFbm(Vector2 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++) { sum += Mathf.Abs(Mathf.PerlinNoise(p.x, p.y) * 2f - 1f) * amp; norm += amp; p *= 2.07f; amp *= 0.5f; }
            return Mathf.Clamp01(sum / norm * 1.5f);
        }

        static float RidgedFbm(Vector2 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(p.x, p.y) * 2f - 1f);
                n *= n;
                sum += n * amp * prev; norm += amp;
                prev = Mathf.Clamp01(n * 1.6f);
                p *= 2.1f; amp *= 0.5f;
            }
            return Mathf.Clamp01(sum / norm);
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
    }
}
