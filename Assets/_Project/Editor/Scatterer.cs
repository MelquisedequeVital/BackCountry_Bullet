using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Distribuição determinística de prefabs sobre o Terrain, seguindo <see cref="ScatterRule"/>.</summary>
    public static class Scatterer
    {
        public const string ScatterRootName = "Scatter";

        // ------------------------------------------------------------------ API

        public static void GenerateAll(ScatterSet set)
        {
            ClearAll(set);
            var ctx = Context.Build(set);
            if (ctx == null) return;
            foreach (var r in set.rules) if (r != null && r.enabled) Generate(set, r, clearFirst: false, ctx);
        }

        public static void ClearAll(ScatterSet set)
        {
            foreach (var r in set.rules) if (r != null) Clear(r);
            // Terrain Trees: limpa todas as instâncias e protótipos (só o scatter cria árvores no Terrain)
            var terrain = TerrainReliefGenerator.FindTerrain();
            if (terrain != null)
            {
                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
                terrain.terrainData.treePrototypes = new TreePrototype[0];
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }

        public static void Clear(ScatterRule rule)
        {
            var root = FindGroup(rule.GroupName, create: false);
            if (root != null)
            {
                for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            if (rule.output == ScatterRule.Output.TerrainTree)
            {
                // remove as instâncias dos protótipos desta regra (regras que dividem prefabs são limpas juntas)
                var terrain = TerrainReliefGenerator.FindTerrain();
                if (terrain == null) return;
                var td = terrain.terrainData;
                var protos = td.treePrototypes;
                var mine = new HashSet<int>();
                for (int i = 0; i < protos.Length; i++)
                    if (rule.prefabs.Exists(w => w.prefab != null && w.prefab == protos[i].prefab)) mine.Add(i);
                if (mine.Count == 0) return;
                var keep = new List<TreeInstance>();
                foreach (var ti in td.treeInstances) if (!mine.Contains(ti.prototypeIndex)) keep.Add(ti);
                td.SetTreeInstances(keep.ToArray(), false);
                EditorUtility.SetDirty(td);
            }
        }

        static int PrototypeIndex(TerrainData td, GameObject prefab)
        {
            var protos = td.treePrototypes;
            for (int i = 0; i < protos.Length; i++) if (protos[i].prefab == prefab) return i;
            var list = new List<TreePrototype>(protos) { new TreePrototype { prefab = prefab, bendFactor = 0f } };
            td.treePrototypes = list.ToArray();
            return list.Count - 1;
        }

        public static int Generate(ScatterSet set, ScatterRule rule, bool clearFirst = true) => Generate(set, rule, clearFirst, null);

        static int Generate(ScatterSet set, ScatterRule rule, bool clearFirst, Context ctx)
        {
            if (clearFirst) Clear(rule);
            if (ctx == null) ctx = Context.Build(set);
            if (ctx == null) return 0;
            bool asTree = rule.output == ScatterRule.Output.TerrainTree;
            if (asTree && rule.placeOnColliders) { Debug.LogWarning($"[Scatter] {rule.GroupName}: placeOnColliders não vale para TerrainTree."); asTree = false; }
            var newTrees = asTree ? new List<TreeInstance>() : null;
            var protoCache = new Dictionary<GameObject, int>();
            if (rule.prefabs.Count == 0 || rule.prefabs.TrueForAll(p => p.prefab == null))
            {
                Debug.LogWarning($"[Scatter] {rule.GroupName}: sem prefabs.");
                return 0;
            }

            // Ocupação: tudo que já está no Scatter (outras regras) e os arcos de pedra
            var hash = new SpatialHash(4f);
            var scatterRoot = FindScatterRoot(false);
            if (rule.placeOnColliders) Physics.SyncTransforms();
            else if (scatterRoot != null)
                foreach (Transform group in scatterRoot)
                    foreach (Transform t in group) hash.Add(t.position, Footprint(t.gameObject), 0);
            var arches = GameObject.Find($"{TerrainReliefGenerator.SceneryRootName}/{TerrainReliefGenerator.ArchesRootName}");
            if (arches != null)
                foreach (Transform t in arches.transform) hash.Add(t.position, Footprint(t.gameObject), 0);
            // Terrain Trees já existentes (outras regras)
            {
                var protos = ctx.td.treePrototypes;
                var protoRadius = new float[protos.Length];
                for (int i = 0; i < protos.Length; i++) protoRadius[i] = protos[i].prefab ? PrefabFootprint(protos[i].prefab).radius : 0.5f;
                foreach (var ti in ctx.td.treeInstances)
                    hash.Add(new Vector3(ctx.origin.x + ti.position.x * ctx.size.x, 0f, ctx.origin.z + ti.position.z * ctx.size.z),
                        protoRadius[ti.prototypeIndex] * ti.widthScale, 0);
            }

            // Âncoras para nearRule
            List<Vector3> anchors = null;
            if (rule.nearRule != null)
            {
                anchors = new List<Vector3>();
                var g = FindGroup(rule.nearRule.GroupName, false);
                if (g != null) foreach (Transform t in g) anchors.Add(t.position);
                if (anchors.Count == 0) { Debug.LogWarning($"[Scatter] {rule.GroupName}: a regra '{rule.nearRule.GroupName}' ainda não foi gerada."); return 0; }
            }

            var rng = new System.Random(set.seed ^ StableHash(rule.GroupName));
            var parent = FindGroup(rule.GroupName, create: true);
            var footprints = new Dictionary<GameObject, (float radius, float height, float bottom)>();
            float totalWeight = 0f;
            foreach (var wp in rule.prefabs) if (wp.prefab != null) totalWeight += wp.weight;

            float avgGroup = (rule.groupSize.x + rule.groupSize.y) * 0.5f;
            bool alongRoad = rule.roadBand.y > 0f && ctx.road.Count > 1;
            float roadLen = RoadPath.Length(ctx.road);
            // Faixa da estrada: densidade conta sobre a área da faixa (dos dois lados), não sobre o mapa
            float hectares = alongRoad
                ? roadLen * 2f * (rule.roadBand.y - rule.roadBand.x) / 10000f
                : ctx.size.x * ctx.size.z / 10000f;
            int trials = anchors != null
                ? Mathf.CeilToInt(anchors.Count * rule.densityPerHectare / avgGroup)
                : Mathf.CeilToInt(rule.densityPerHectare * hectares / avgGroup);
            if (alongRoad) trials *= 3; // a faixa é estreita: mais tentativas, o espaçamento limita o total
            int placed = 0;

            for (int t = 0; t < trials; t++)
            {
                Vector3 c;
                if (anchors != null)
                {
                    var a = anchors[rng.Next(anchors.Count)];
                    Vector2 d = RandomInDisc(rng) * rule.nearRuleDistance;
                    c = new Vector3(a.x + d.x, 0f, a.z + d.y);
                }
                else if (alongRoad)
                {
                    int i = rng.Next(1, ctx.road.Count);
                    Vector3 a = ctx.road[i - 1], b = ctx.road[i];
                    Vector3 dir = (b - a); dir.y = 0f; dir.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (rng.Next(2) == 0 ? -1f : 1f);
                    float dist = ctx.layout.roadWidth * 0.5f + Mathf.Lerp(rule.roadBand.x, rule.roadBand.y, (float)rng.NextDouble());
                    c = Vector3.Lerp(a, b, (float)rng.NextDouble()) + side * dist;
                    c.y = 0f;
                    if ((float)rng.NextDouble() > ctx.DensityFactor(rule, c)) continue;
                }
                else
                {
                    c = new Vector3(ctx.origin.x + (float)rng.NextDouble() * ctx.size.x, 0f, ctx.origin.z + (float)rng.NextDouble() * ctx.size.z);
                    if ((float)rng.NextDouble() > ctx.DensityFactor(rule, c)) continue;
                }

                int members = rng.Next(rule.groupSize.x, rule.groupSize.y + 1);
                for (int m = 0; m < members; m++)
                {
                    Vector2 off = m == 0 ? Vector2.zero : RandomInDisc(rng) * rule.groupRadius;
                    Vector3 p = new Vector3(c.x + off.x, 0f, c.z + off.y);
                    // sorteia prefab e escala antes de testar espaço (o raio depende deles)
                    var prefab = Pick(rule, rng, totalWeight);
                    float scale = Mathf.Lerp(rule.scaleRange.x, rule.scaleRange.y, (float)rng.NextDouble());
                    float yaw = rule.randomYaw ? (float)rng.NextDouble() * 360f : 0f;
                    float tiltX = ((float)rng.NextDouble() * 2f - 1f) * rule.randomTilt;
                    float tiltZ = ((float)rng.NextDouble() * 2f - 1f) * rule.randomTilt;
                    if (prefab == null) continue;
                    float? surfaceY = null;
                    if (rule.placeOnColliders)
                    {
                        // precisa cair em cima de um objeto do Scatter (não do terreno), com topo pouco inclinado
                        if (!Physics.Raycast(new Vector3(p.x, 2000f, p.z), Vector3.down, out var hit, 4000f)) continue;
                        var surfaceRoot = rule.nearRule != null ? FindGroup(rule.nearRule.GroupName, false) : scatterRoot;
                        if (hit.collider is TerrainCollider || surfaceRoot == null || !hit.collider.transform.IsChildOf(surfaceRoot)) continue;
                        if (Vector3.Angle(hit.normal, Vector3.up) > rule.slopeRange.y) continue;
                        surfaceY = hit.point.y;
                    }
                    else if (!ctx.IsValid(rule, p)) continue;

                    if (!footprints.TryGetValue(prefab, out var fp)) { fp = PrefabFootprint(prefab); footprints[prefab] = fp; }
                    float radius = fp.radius * scale;
                    if (!hash.IsFree(p, radius, rule.minDistance, rule.footprintFactor, 1)) continue;

                    if (asTree)
                    {
                        float y = ctx.Height(p) - ctx.origin.y - (fp.height * scale * rule.sinkFraction + fp.bottom * scale);
                        newTrees.Add(new TreeInstance
                        {
                            position = new Vector3((p.x - ctx.origin.x) / ctx.size.x, y / ctx.size.y, (p.z - ctx.origin.z) / ctx.size.z),
                            prototypeIndex = protoCache.TryGetValue(prefab, out var pi) ? pi : (protoCache[prefab] = PrototypeIndex(ctx.td, prefab)),
                            widthScale = scale * prefab.transform.localScale.x,
                            heightScale = scale * prefab.transform.localScale.y,
                            rotation = yaw * Mathf.Deg2Rad,
                            color = Color.white,
                            lightmapColor = Color.white,
                        });
                    }
                    else Spawn(prefab, parent, ctx, rule, p, scale, yaw, tiltX, tiltZ, fp, surfaceY);
                    hash.Add(p, radius, 1);
                    placed++;
                }
            }
            if (asTree && newTrees.Count > 0)
            {
                var all = new List<TreeInstance>(ctx.td.treeInstances);
                all.AddRange(newTrees);
                ctx.td.SetTreeInstances(all.ToArray(), false);
                ctx.terrain.Flush();
                EditorUtility.SetDirty(ctx.td);
            }
            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            Debug.Log($"[Scatter] {rule.GroupName}: {placed} objetos.");
            return placed;
        }

        // ------------------------------------------------------------------ colocação

        static void Spawn(GameObject prefab, Transform parent, Context ctx, ScatterRule rule, Vector3 p, float scale,
            float yaw, float tiltX, float tiltZ, (float radius, float height, float bottom) fp, float? surfaceY)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Vector3 normal = surfaceY.HasValue ? Vector3.up : ctx.Normal(p);
            Quaternion align = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), rule.alignToNormal);
            Quaternion rot = align * Quaternion.Euler(tiltX, 0f, tiltZ) * Quaternion.Euler(0f, yaw, 0f);
            float y = surfaceY ?? ctx.Height(p);
            Vector3 up = rot * Vector3.up;
            Vector3 pos = new Vector3(p.x, y, p.z) - up * (fp.height * scale * rule.sinkFraction + fp.bottom * scale);
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.transform.localScale = Vector3.Scale(prefab.transform.localScale, Vector3.one * scale);
        }

        static GameObject Pick(ScatterRule rule, System.Random rng, float totalWeight)
        {
            float r = (float)rng.NextDouble() * totalWeight;
            foreach (var wp in rule.prefabs)
            {
                if (wp.prefab == null) continue;
                r -= wp.weight;
                if (r <= 0f) return wp.prefab;
            }
            return rule.prefabs.FindLast(w => w.prefab != null)?.prefab;
        }

        /// <summary>Raio horizontal, altura e fundo (y mínimo relativo ao pivô) do prefab, em escala 1 da instância.</summary>
        static (float radius, float height, float bottom) PrefabFootprint(GameObject prefab)
        {
            var rends = prefab.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return (0.5f, 1f, 0f);
            // bounds no espaço do prefab (raiz na origem)
            var inv = prefab.transform.worldToLocalMatrix;
            Bounds b = default; bool init = false;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = Vector3.Scale(m.MultiplyPoint3x4(corner), prefab.transform.localScale);
                    if (!init) { b = new Bounds(w, Vector3.zero); init = true; } else b.Encapsulate(w);
                }
            }
            float radius = Mathf.Max(b.extents.x, b.extents.z);
            return (radius, b.size.y, b.min.y);
        }

        static float Footprint(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 0.5f;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        static Vector2 RandomInDisc(System.Random rng)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rng.NextDouble());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        static int StableHash(string s)
        {
            unchecked { int h = 23; foreach (char ch in s) h = h * 31 + ch; return h; }
        }

        // ------------------------------------------------------------------ hierarquia

        static Transform FindScatterRoot(bool create)
        {
            var scenery = GameObject.Find(TerrainReliefGenerator.SceneryRootName);
            if (scenery == null) { if (!create) return null; scenery = new GameObject(TerrainReliefGenerator.SceneryRootName); }
            var root = scenery.transform.Find(ScatterRootName);
            if (root == null && create) { root = new GameObject(ScatterRootName).transform; root.SetParent(scenery.transform, false); }
            return root;
        }

        public static Transform FindGroup(string name, bool create)
        {
            var root = FindScatterRoot(create);
            if (root == null) return null;
            var g = root.Find(name);
            if (g == null && create) { g = new GameObject(name).transform; g.SetParent(root, false); }
            return g;
        }

        // ------------------------------------------------------------------ contexto do terreno

        class Context
        {
            public Terrain terrain;
            public TerrainData td;
            public Vector3 origin, size;
            public Vector2 center;
            public TerrainReliefSettings relief;
            public CaatingaLayoutSettings layout;
            public List<Vector3> road, river;
            public List<(Vector2 c, Vector2 half)> areas = new List<(Vector2, Vector2)>();
            public int noiseSeed;
            public VegetationField field;

            public static Context Build(ScatterSet set)
            {
                var terrain = TerrainReliefGenerator.FindTerrain();
                var layoutRoot = TerrainReliefGenerator.FindLayout();
                if (terrain == null || layoutRoot == null || set.relief == null || set.relief.layout == null)
                {
                    Debug.LogError("[Scatter] Precisa de Terrain, _Layout e do asset de relevo no ScatterSet.");
                    return null;
                }
                var ctx = new Context
                {
                    terrain = terrain, td = terrain.terrainData, origin = terrain.transform.position, size = terrain.terrainData.size,
                    relief = set.relief, layout = set.relief.layout, noiseSeed = set.seed
                };
                ctx.center = new Vector2(ctx.origin.x + ctx.size.x * 0.5f, ctx.origin.z + ctx.size.z * 0.5f);
                ctx.road = RoadPath.Sample(RoadPath.GetControlPoints(layoutRoot.Find(CaatingaLayoutSettings.EstradaName)), 2f);
                ctx.river = TerrainReliefGenerator.GetRiverPath(set.relief);
                var vila = layoutRoot.Find(CaatingaLayoutSettings.VilaName);
                var dc = layoutRoot.Find(CaatingaLayoutSettings.DatacenterName);
                if (vila) ctx.areas.Add((new Vector2(vila.position.x, vila.position.z), ctx.layout.vilaSize * 0.5f));
                if (dc) ctx.areas.Add((new Vector2(dc.position.x, dc.position.z), ctx.layout.datacenterSize * 0.5f));
                if (set.paint != null) ctx.field = VegetationField.Build(set.paint);
                return ctx;
            }

            bool RegionOk(ScatterRule r, Vector2 p2)
            {
                float half = relief.playableHalfSize;
                bool inside = Mathf.Abs(p2.x - center.x) < half && Mathf.Abs(p2.y - center.y) < half;
                if (r.region == ScatterRule.Region.AreaJogavel) return inside;
                if (r.region == ScatterRule.Region.ForaDaAreaJogavel) return !inside;
                return true;
            }

            Vector2 UV(Vector3 p) => new Vector2((p.x - origin.x) / size.x, (p.z - origin.z) / size.z);
            public float Height(Vector3 p) => terrain.SampleHeight(p) + origin.y;
            public Vector3 Normal(Vector3 p) { var uv = UV(p); return td.GetInterpolatedNormal(uv.x, uv.y); }
            public float Slope(Vector3 p) { var uv = UV(p); return td.GetSteepness(uv.x, uv.y); }

            float ReservedDistance(Vector2 p)
            {
                float d = float.MaxValue;
                foreach (var a in areas) d = Mathf.Min(d, TerrainReliefGenerator.RectDistance(p, a.c, a.half));
                return d;
            }

            float RoadEdgeDistance(Vector3 p) => road.Count > 1 ? RoadPath.DistanceXZ(road, p) - layout.roadWidth * 0.5f : float.MaxValue;

            /// <summary>Probabilidade (0..1) de aceitar um centro de grupo neste ponto: região, manchas de ruído e proximidade de estrada/vila.</summary>
            public float DensityFactor(ScatterRule r, Vector3 p)
            {
                var uv = UV(p);
                if (uv.x < 0f || uv.y < 0f || uv.x > 1f || uv.y > 1f) return 0f;
                Vector2 p2 = new Vector2(p.x, p.z);
                float f = 1f;
                float mask = TerrainReliefGenerator.SerraMask(relief, p2, center);
                if (r.region == ScatterRule.Region.Planicie) f *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.4f, mask));
                else if (r.region == ScatterRule.Region.Serras) f *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.3f, mask));
                if (!RegionOk(r, p2)) return 0f;
                if (f <= 0f) return 0f;
                if (r.onlyNearTrail && field != null)
                {
                    f *= field.Trail(p2);
                    if (f <= 0f) return 0f;
                }
                if (r.vegetationField > 0f && field != null)
                {
                    float c = field.Cover(p2);
                    if (r.invertVegetationField) c = 1f - c;
                    f *= Mathf.Lerp(1f, c, r.vegetationField);
                    if (f <= 0f) return 0f;
                }
                if (r.patchScale > 0f && r.patchiness > 0f)
                {
                    float n = Mathf.PerlinNoise((p.x + noiseSeed * 0.37f + StableHash(r.GroupName) % 1000) / r.patchScale, (p.z - noiseSeed * 0.11f) / r.patchScale);
                    f *= Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, n)), r.patchiness);
                }
                if (r.roadBand.y <= 0f)
                {
                    float near = Mathf.Min(ReservedDistance(p2), RoadEdgeDistance(p));
                    f *= Mathf.Lerp(r.nearReservedDensity, 1f, Mathf.SmoothStep(0f, 1f, near / Mathf.Max(1f, r.densityFalloffDistance)));
                }
                return f;
            }

            /// <summary>Restrições rígidas de cada objeto.</summary>
            public bool IsValid(ScatterRule r, Vector3 p)
            {
                var uv = UV(p);
                if (uv.x < 0.002f || uv.y < 0.002f || uv.x > 0.998f || uv.y > 0.998f) return false;
                Vector2 p2 = new Vector2(p.x, p.z);
                float slope = td.GetSteepness(uv.x, uv.y);
                if (slope < r.slopeRange.x || slope > r.slopeRange.y) return false;
                float hRel = Height(p) - relief.baseHeight;
                if (hRel < r.heightRange.x || hRel > r.heightRange.y) return false;
                if (ReservedDistance(p2) < Mathf.Max(r.reservedClearance, layout.scatterClearance)) return false;
                float roadEdge = RoadEdgeDistance(p);
                if (roadEdge < Mathf.Max(r.roadClearance, layout.scatterClearance * 0.5f)) return false;
                if (r.roadBand.y > 0f && (roadEdge < r.roadBand.x || roadEdge > r.roadBand.y)) return false;
                if (r.avoidRiverBed && RoadPath.DistanceXZ(river, p) < relief.riverBedHalfWidth + 1.5f
                    && TerrainReliefGenerator.SerraMask(relief, p2, center) < 0.5f) return false;
                if (r.region == ScatterRule.Region.Planicie && TerrainReliefGenerator.SerraMask(relief, p2, center) > 0.45f) return false;
                if (r.region == ScatterRule.Region.Serras && TerrainReliefGenerator.SerraMask(relief, p2, center) < 0.02f) return false;
                if (!RegionOk(r, p2)) return false;
                if (field != null && (r.lajedoRange.x > 0f || r.lajedoRange.y < 1f))
                {
                    float l = field.LajedoMask(p2);
                    if (l < r.lajedoRange.x || l > r.lajedoRange.y) return false;
                }
                if (r.onlyNearTrail && field != null && field.Trail(p2) < 0.05f) return false;
                // regras da mata: nada onde a cobertura é zero (faixa da estrada, topo de lajedo, rocha, leito)
                if (field != null && r.vegetationField > 0f && !r.invertVegetationField && field.Cover(p2) < 0.04f) return false;
                if (field != null && r.roadBand.y <= 0f && field.RoadEdge(p2) < VegetationField.RoadBare) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ espaço ocupado

        class SpatialHash
        {
            readonly float cell;
            // tag: 0 = outros objetos (só o tamanho conta), 1 = desta regra (vale também minDistance)
            readonly Dictionary<(int, int), List<(Vector2 p, float r, int tag)>> cells = new Dictionary<(int, int), List<(Vector2, float, int)>>();
            float maxRadius;
            public SpatialHash(float cellSize) { cell = cellSize; }

            public void Add(Vector3 p, float radius, int tag)
            {
                var key = ((int)Mathf.Floor(p.x / cell), (int)Mathf.Floor(p.z / cell));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(Vector2, float, int)>();
                list.Add((new Vector2(p.x, p.z), radius, tag));
                maxRadius = Mathf.Max(maxRadius, radius);
            }

            public bool IsFree(Vector3 p, float radius, float minDistance, float footprintFactor, int tag)
            {
                Vector2 q = new Vector2(p.x, p.z);
                float reach = Mathf.Max(minDistance, (radius + maxRadius) * footprintFactor);
                int n = Mathf.CeilToInt(reach / cell);
                int cx = (int)Mathf.Floor(p.x / cell), cz = (int)Mathf.Floor(p.z / cell);
                for (int dz = -n; dz <= n; dz++)
                for (int dx = -n; dx <= n; dx++)
                {
                    if (!cells.TryGetValue((cx + dx, cz + dz), out var list)) continue;
                    foreach (var (o, r, t) in list)
                    {
                        float need = (radius + r) * footprintFactor;
                        if (t == tag) need = Mathf.Max(minDistance, need);
                        if (Vector2.Distance(o, q) < need) return false;
                    }
                }
                return true;
            }
        }
    }
}
