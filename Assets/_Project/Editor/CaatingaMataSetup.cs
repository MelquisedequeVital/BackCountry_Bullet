using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Part = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.Part;
using Tweak = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.MaterialTweak;
using Opt = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.Options;
using Col = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.ColliderKind;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Caatinga como mata seca fechada: prefabs dos estratos (massas de arbustos, árvores baixas oliva, mandacaru em candelabro,
    /// macambira placeholder, capim), regras de scatter, detalhes do Terrain e limite da área jogável.
    /// Tudo determinístico (seeds dos assets de relevo, pintura e scatter).
    /// </summary>
    public static class CaatingaMataSetup
    {
        const string VegFbx = "Assets/Vegetation/Vegetation/Models/Vegetation.fbx";
        const string Cactus3 = "Assets/vegetacoes1/FBX/Cactus_3.fbx";
        const string TerrainFolder = "Assets/_Project/Environment/Terrain";
        const string MeshFolder = "Assets/_Project/Environment/Meshes";
        const string ScatterFolder = CaatingaAssetSetup.ScatterFolder;
        public const string LimitName = "Limite_AreaJogavel";

        // Tree_B.001 (arbusto de folhas em cartão) na escala de importação
        const float TreeBW = 4.34f, TreeBH = 6.12f;

        [MenuItem("Tools/Backcountry Bullet/Mata fechada/1. Recriar prefabs da mata")]
        public static void MenuPrefabs() => BuildPrefabs(true);

        [MenuItem("Tools/Backcountry Bullet/Mata fechada/2. Gerar mata (regras, scatter, detalhes, limites)")]
        public static void MenuGenerate() => GenerateAll(false);

        public static ScatterSet LoadSet() => AssetDatabase.LoadAssetAtPath<ScatterSet>($"{ScatterFolder}/Scatter_Caatinga.asset");
        public static TerrainPaintSettings LoadPaint() => AssetDatabase.LoadAssetAtPath<TerrainPaintSettings>($"{TerrainFolder}/CaatingaPaint.asset");

        public static void GenerateAll(bool rebuildPrefabs)
        {
            var prefabs = BuildPrefabs(rebuildPrefabs);
            var set = LoadSet();
            var paint = LoadPaint();
            ConfigureRules(set, paint, prefabs);
            TerrainPainter.Paint(paint);
            Scatterer.GenerateAll(set);
            ConfigureTerrainRendering();
            PaintDetails(paint, prefabs);
            BuildPlayableLimit(paint.relief);
            StripCollidersOutsidePlayable(paint.relief);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        // ================================================================== prefabs

        public class Prefabs
        {
            public List<GameObject> mata = new List<GameObject>(), mataSeca = new List<GameObject>(), arvoresOliva = new List<GameObject>();
            public List<GameObject> mandacarus = new List<GameObject>(), facheiros = new List<GameObject>();
            public GameObject capim01, capim02, macambira;
        }

        static Color? MataTint(Material m, bool yellow)
        {
            switch (m.name)
            {
                case "Tree_B_V": return yellow ? new Color(2.3f, 1.7f, 1.75f) : new Color(2.15f, 1.58f, 2.0f);   // folha -> verde-oliva opaco
                case "Branch_T2_01": return yellow ? new Color(1.42f, 1.2f, 1.2f) : new Color(1.32f, 1.12f, 1.35f);
                case "Tree_B_N": return new Color(0.86f, 0.85f, 0.82f);                                            // galhos cinza-esbranquiçados
                case "Tree_B": return new Color(1.8f, 1.85f, 1.95f);                                               // gravetos secos acinzentados
                case "BarkDecidious": return new Color(1.25f, 1.22f, 1.12f);                                       // casca clara
                default: return null;
            }
        }

        public static Prefabs BuildPrefabs(bool rebuild)
        {
            var p = new Prefabs();
            GameObject Get(string name, System.Func<GameObject> build)
            {
                var path = $"{EnvironmentPrefabFactory.PrefabFolder}/{name}.prefab";
                var go = rebuild ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return go != null ? go : build();
            }

            var oliva = new Tweak { alphaClip = true, alphaCutoff = 0.45f, suffix = "_Mata", tintBy = m => MataTint(m, false) };
            var olivaAmarela = new Tweak { alphaClip = true, alphaCutoff = 0.45f, suffix = "_Mata2", tintBy = m => MataTint(m, true) };

            // ---- estrato arbustivo: massas de 4-6 m de touceiras emendadas, com galhos secos cinza
            var shrubOpt = new Opt { collider = Col.Nenhum, cullScreenHeight = 0.03f, castShadows = false };   // LODs refeitos em ShrubLods()
            p.mata.Add(Get("Mata_Arbusto_01", () => EnvironmentPrefabFactory.CreateComposite("Mata_Arbusto_01", Mata(1, 5, 1, 1, 2.0f, oliva), shrubOpt)));
            p.mata.Add(Get("Mata_Arbusto_02", () => EnvironmentPrefabFactory.CreateComposite("Mata_Arbusto_02", Mata(2, 6, 0, 1, 2.4f, olivaAmarela), shrubOpt)));
            p.mata.Add(Get("Mata_Arbusto_03", () => EnvironmentPrefabFactory.CreateComposite("Mata_Arbusto_03", Mata(3, 4, 2, 1, 1.8f, oliva), shrubOpt)));
            p.mata.Add(Get("Mata_Arbusto_04", () => EnvironmentPrefabFactory.CreateComposite("Mata_Arbusto_04", Mata(4, 3, 1, 0, 1.3f, olivaAmarela), shrubOpt)));
            p.mataSeca.Add(Get("Mata_Seca_01", () => EnvironmentPrefabFactory.CreateComposite("Mata_Seca_01", Mata(5, 2, 3, 1, 1.8f, oliva), shrubOpt)));
            p.mataSeca.Add(Get("Mata_Seca_02", () => EnvironmentPrefabFactory.CreateComposite("Mata_Seca_02", Mata(6, 1, 2, 2, 1.5f, olivaAmarela), shrubOpt)));
            foreach (var m in p.mata) ShrubLods(m);
            foreach (var m in p.mataSeca) ShrubLods(m);

            // ---- estrato arbóreo baixo: 4-7 m, copa rala/média verde-oliva
            var tree = new Opt { collider = Col.Tronco, trunkRadius = 0.2f, trunkHeight = 2.5f, cullScreenHeight = 0.016f };
            p.arvoresOliva.Add(Get("Arvore_Oliva_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_04_V", "Arvore_Oliva_01", Vector3.one * 0.27f, oliva, tree)));
            p.arvoresOliva.Add(Get("Arvore_Oliva_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_06_V", "Arvore_Oliva_02", Vector3.one * 0.25f, olivaAmarela, tree)));
            p.arvoresOliva.Add(Get("Arvore_Oliva_03", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_05_V", "Arvore_Oliva_03", Vector3.one * 0.22f, oliva, tree)));
            p.arvoresOliva.Add(Get("Arvore_Oliva_04", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B.001", "Arvore_Oliva_04", new Vector3(0.95f, 0.85f, 0.95f), olivaAmarela, tree)));

            // ---- emergentes: mandacaru em candelabro (muitos braços saindo baixo) e facheiro, verde-azulado
            var cacto = new Tweak { tint = new Color(1.28f, 1.24f, 1.85f), suffix = "_Mandacaru" };   // verde-azulado claro (glauco)
            var big = new Opt { collider = Col.Capsule, colliderShrink = 0.35f, cullScreenHeight = 0.008f };
            p.mandacarus.Add(Get("Cacto_Mandacaru_01", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_01",
                CaatingaVegetationSetup.Mandacaru(4.6f, new[] { (1.0f, 0f, 3.0f), (1.25f, 72f, 3.2f), (1.1f, 145f, 2.8f), (1.4f, 215f, 3.1f), (1.2f, 290f, 2.6f) }, cacto), big)));
            p.mandacarus.Add(Get("Cacto_Mandacaru_02", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_02",
                CaatingaVegetationSetup.Mandacaru(5.6f, new[] { (1.2f, 20f, 3.6f), (1.0f, 95f, 3.3f), (1.5f, 165f, 3.8f), (1.1f, 240f, 3.0f), (1.6f, 310f, 3.4f), (2.5f, 60f, 2.1f) }, cacto), big)));
            p.mandacarus.Add(Get("Cacto_Mandacaru_03", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_03",
                CaatingaVegetationSetup.Mandacaru(3.9f, new[] { (0.9f, 40f, 2.4f), (1.1f, 160f, 2.7f), (1.0f, 280f, 2.2f) }, cacto), big)));
            foreach (var n in new[] { "Cacto_Facheiro_01", "Cacto_Facheiro_02" })
            {
                var f = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentPrefabFactory.PrefabFolder}/{n}.prefab");
                if (f != null) p.facheiros.Add(f);
            }

            // ---- rasteiro: capim seco (malha do pack) e macambira (placeholder procedural)
            p.capim01 = Get("Capim_Seco_01", () => DetailPrefabFromModel("Capim_Seco_01", VegFbx, "Grass_P.005", 0.4f, new Color(1.75f, 2.2f, 2.2f)));
            p.capim02 = Get("Capim_Seco_02", () => DetailPrefabFromModel("Capim_Seco_02", VegFbx, "Grass_P.003", 0.45f, new Color(1.75f, 2.2f, 2.2f)));
            p.macambira = Get("Macambira_PLACEHOLDER", BuildMacambira);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>
        /// LODs da massa de arbustos: LOD0 completo de perto, LOD1 só com as duas primeiras peças (copa maior e vizinha)
        /// a média distância, e some de longe (o chão de mata pintado no Terrain segura o mosaico verde).
        /// </summary>
        static void ShrubLods(GameObject prefab)
        {
            if (prefab == null) return;
            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mesh = root.transform.Find("Mesh");
                if (mesh == null) return;
                var all = new List<Renderer>(root.GetComponentsInChildren<Renderer>());
                var near = new List<Renderer>();
                for (int i = 0; i < Mathf.Min(1, mesh.childCount); i++) near.AddRange(mesh.GetChild(i).GetComponentsInChildren<Renderer>());
                foreach (var r in all) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lod = root.GetComponent<LODGroup>();
                if (lod == null) lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.12f, all.ToArray()), new LOD(0.022f, near.ToArray()) });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Massa de arbustos: copas de folha em cartão emendadas, galhos secos cinza e gravetos baixos.</summary>
        static List<Part> Mata(int seed, int crowns, int greyBranches, int twigs, float radius, Tweak tw)
        {
            var rng = new System.Random(seed * 7919);
            float R() => (float)rng.NextDouble();
            var parts = new List<Part>();
            for (int i = 0; i < crowns; i++)
            {
                float ang = R() * Mathf.PI * 2f, r = i == 0 ? 0f : radius * Mathf.Sqrt(Mathf.Lerp(0.25f, 1f, R()));
                float h = Mathf.Lerp(1.7f, 2.9f, R()) * (i == 0 ? 1.1f : 1f), w = Mathf.Lerp(2.6f, 3.8f, R());
                parts.Add(new Part { model = VegFbx, child = "Tree_B.001", position = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r),
                    euler = new Vector3(0f, R() * 360f, 0f), scale = new Vector3(w / TreeBW, h / TreeBH, w / TreeBW), tweak = tw });
            }
            for (int i = 0; i < greyBranches; i++)
            {
                float ang = R() * Mathf.PI * 2f, r = radius * 0.8f * Mathf.Sqrt(R());
                float h = Mathf.Lerp(2.2f, 3.3f, R()), w = Mathf.Lerp(1.8f, 2.6f, R());
                parts.Add(new Part { model = VegFbx, child = "Tree_B.002", position = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r),
                    euler = new Vector3(0f, R() * 360f, 0f), scale = new Vector3(w / TreeBW, h / TreeBH, w / TreeBW), tweak = tw });
            }
            for (int i = 0; i < twigs; i++)
            {
                float ang = R() * Mathf.PI * 2f, r = radius * Mathf.Lerp(0.6f, 1.1f, R());
                parts.Add(new Part { model = VegFbx, child = "Plane.016", position = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r),
                    euler = new Vector3(0f, R() * 360f, 0f), scale = Vector3.one * Mathf.Lerp(0.4f, 0.55f, R()), tweak = tw });
            }
            return parts;
        }

        /// <summary>Prefab de detalhe do Terrain: malha única na raiz (exigência dos detalhes em malha), transformação de importação embutida.</summary>
        static GameObject DetailPrefabFromModel(string name, string modelPath, string child, float scale, Color tint)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Transform src = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == child) { src = t; break; }
            if (src == null) { Debug.LogError($"[Mata] '{child}' não existe em {modelPath}"); return null; }
            var mf = src.GetComponent<MeshFilter>();
            var mr = src.GetComponent<MeshRenderer>();
            // embute rotação/escala de importação, pivô na base centrada
            var rotScale = Matrix4x4.TRS(Vector3.zero, src.rotation, Vector3.Scale(src.lossyScale, Vector3.one * scale));
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = mf.sharedMesh, transform = rotScale } }, true, true);
            var b = mesh.bounds;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] -= new Vector3(b.center.x, b.min.y, b.center.z);
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            EnvironmentPrefabFactory.EnsureFolder(MeshFolder);
            var meshPath = $"{MeshFolder}/{name}.asset";
            mesh = SaveMesh(mesh, meshPath);

            var mat = DetailMaterial($"M_{name}", mr.sharedMaterial.GetTexture("_BaseMap"), tint, 0.4f);
            return SaveDetailPrefab(name, mesh, mat);
        }

        /// <summary>Grava a malha no asset existente (mantém GUID e referências dos prefabs) ou cria um novo.</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        static Material DetailMaterial(string name, Texture tex, Color color, float cutoff)
        {
            var path = $"{EnvironmentPrefabFactory.MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetFloat("_Cull", 0f);
            mat.enableInstancing = true;
            if (cutoff > 0f)
            {
                mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", cutoff); mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; mat.SetOverrideTag("RenderType", "TransparentCutout");
            }
            if (created) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject SaveDetailPrefab(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{EnvironmentPrefabFactory.PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// PLACEHOLDER: nenhum pack instalado tem bromélia/macambira. Roseta procedural de folhas estreitas e arqueadas,
        /// verde-amarelada com pontas secas (gradiente numa textura 4x64).
        /// </summary>
        static GameObject BuildMacambira()
        {
            const string name = "Macambira_PLACEHOLDER";
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var norms = new List<Vector3>(); var tris = new List<int>();
            const int leaves = 14, seg = 4;
            for (int l = 0; l < leaves; l++)
            {
                float ang = l * Mathf.PI * 2f / leaves + (R() - 0.5f) * 0.35f;
                float len = Mathf.Lerp(0.55f, 0.95f, R());
                float elev = Mathf.Lerp(50f, 78f, R()) * Mathf.Deg2Rad;
                float droop = Mathf.Lerp(0.25f, 0.55f, R());
                float baseW = Mathf.Lerp(0.05f, 0.08f, R());
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                int start = verts.Count;
                for (int s = 0; s <= seg; s++)
                {
                    float t = s / (float)seg;
                    Vector3 c = dir * (Mathf.Cos(elev) * len * t + 0.03f) + Vector3.up * (Mathf.Sin(elev) * len * t - droop * len * t * t);
                    float w = baseW * (1f - t * 0.92f);
                    Vector3 n = (Vector3.up * 0.8f + dir * 0.4f).normalized;
                    verts.Add(c - side * w); verts.Add(c + side * w);
                    norms.Add(n); norms.Add(n);
                    uvs.Add(new Vector2(0.25f, t)); uvs.Add(new Vector2(0.75f, t));
                }
                for (int s = 0; s < seg; s++)
                {
                    int a = start + s * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EnvironmentPrefabFactory.EnsureFolder(MeshFolder);
            var meshPath = $"{MeshFolder}/{name}.asset";
            mesh = SaveMesh(mesh, meshPath);

            // gradiente base -> ponta
            var tex = ProceduralTextures.CreateTileable("T_Macambira_PLACEHOLDER", 64, (u, v) =>
            {
                Color baseC = new Color(0.36f, 0.43f, 0.18f), mid = new Color(0.62f, 0.62f, 0.27f), tip = new Color(0.66f, 0.50f, 0.30f);
                Color c = v < 0.6f ? Color.Lerp(baseC, mid, v / 0.6f) : Color.Lerp(mid, tip, (v - 0.6f) / 0.4f);
                c *= Mathf.Lerp(0.9f, 1.05f, Mathf.Abs(u - 0.5f) * 2f);
                c.a = 1f;
                return c;
            });
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
            imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
            var mat = DetailMaterial("M_Macambira_PLACEHOLDER", tex, Color.white, 0f);
            return SaveDetailPrefab(name, mesh, mat);
        }

        // ================================================================== regras de scatter

        static ScatterRule Rule(string name)
        {
            var path = $"{ScatterFolder}/Scatter_{name}.asset";
            var r = AssetDatabase.LoadAssetAtPath<ScatterRule>(path);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<ScatterRule>();
                r.categoryName = name;
                AssetDatabase.CreateAsset(r, path);
            }
            return r;
        }

        static List<ScatterRule.WeightedPrefab> W(params (GameObject p, float w)[] items)
        {
            var l = new List<ScatterRule.WeightedPrefab>();
            foreach (var (p, w) in items) if (p != null) l.Add(new ScatterRule.WeightedPrefab { prefab = p, weight = w });
            return l;
        }

        static GameObject P(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentPrefabFactory.PrefabFolder}/{name}.prefab");

        public static void ConfigureRules(ScatterSet set, TerrainPaintSettings paint, Prefabs pf)
        {
            set.paint = paint;
            // Regras antigas de árvores e touceiras isoladas saem (substituídas pela mata e pelas árvores oliva)
            foreach (var old in new[] { "Arvores_Secas", "Arvores_Ralas", "Arbustos_Arvores", "Arbustos_Touceiras" })
            {
                var r = AssetDatabase.LoadAssetAtPath<ScatterRule>($"{ScatterFolder}/Scatter_{old}.asset");
                if (r == null) continue;
                Scatterer.Clear(r);
                r.enabled = false;
                set.rules.Remove(r);
                EditorUtility.SetDirty(r);
            }

            var secas = new List<(GameObject, float)>();
            foreach (var n in new[] { "Arvore_Seca_01", "Arvore_Seca_02", "Arvore_Seca_03", "Arvore_Seca_04", "Arvore_Seca_05" }) secas.Add((P(n), 0.3f));
            secas.Add((P("Arvore_Seca_Clara_01"), 0.35f)); secas.Add((P("Arvore_Seca_Clara_02"), 0.35f));
            var arvores = new List<(GameObject, float)>();
            foreach (var a in pf.arvoresOliva) arvores.Add((a, 1f));
            arvores.AddRange(secas);                          // ~40% sem folha, galhos aparentes
            var emergentes = new List<(GameObject, float)>();
            foreach (var m in pf.mandacarus) emergentes.Add((m, 1f));
            foreach (var f in pf.facheiros) emergentes.Add((f, 0.8f));

            void Common(ScatterRule r, float field)
            {
                r.enabled = true;
                r.vegetationField = field;
                r.invertVegetationField = false;
                r.lajedoRange = new Vector2(0f, 1f);
                r.nearReservedDensity = 1f;            // o campo já abre as clareiras
                r.densityFalloffDistance = 1f;
                r.roadBand = Vector2.zero;
                r.nearRule = null;
                r.placeOnColliders = false;
                r.scaleRange = new Vector2(0.8f, 1.2f);
                r.randomYaw = true;
                r.alignToNormal = 0f;
                r.randomTilt = 0f;
                r.avoidRiverBed = true;
                r.reservedClearance = VegetationField.AreaBare;
                r.roadClearance = VegetationField.RoadBare;
                r.heightRange = new Vector2(-5f, 140f);
                r.patchScale = 0f; r.patchiness = 0f;
            }

            // Touceiras de cobertura ao longo da estrada (fora da faixa de solo exposto)
            var estrada = Rule("Arbustos_Estrada");
            Common(estrada, 0f);
            estrada.prefabs = W((pf.mata[3], 1f), (pf.mataSeca[1], 0.7f), (P("Arbusto_Seco_01"), 0.4f));
            estrada.output = ScatterRule.Output.TerrainTree;
            estrada.roadBand = new Vector2(VegetationField.RoadBare + 1f, VegetationField.RoadOpen);
            estrada.densityPerHectare = 70f; estrada.groupSize = new Vector2Int(1, 2); estrada.groupRadius = 3f;
            estrada.minDistance = 7f; estrada.footprintFactor = 0.6f; estrada.slopeRange = new Vector2(0f, 30f);
            estrada.patchScale = 40f; estrada.patchiness = 0.5f;

            // Emergentes na área jogável (colisor cápsula)
            var mand = Rule("Cactos_Mandacaru");
            Common(mand, 0.6f);
            mand.region = ScatterRule.Region.AreaJogavel;
            mand.output = ScatterRule.Output.GameObject;
            mand.prefabs = W(emergentes.ToArray());
            mand.densityPerHectare = 7f; mand.groupSize = new Vector2Int(1, 2); mand.groupRadius = 6f;
            mand.minDistance = 7f; mand.footprintFactor = 0.4f; mand.slopeRange = new Vector2(0f, 30f);
            mand.patchScale = 120f; mand.patchiness = 0.4f;
            var fach = AssetDatabase.LoadAssetAtPath<ScatterRule>($"{ScatterFolder}/Scatter_Cactos_Facheiro.asset");
            if (fach != null) { Scatterer.Clear(fach); fach.enabled = false; set.rules.Remove(fach); EditorUtility.SetDirty(fach); }

            // Árvores baixas na área jogável (colisor no tronco)
            var arvJ = Rule("Arvores_Jogavel");
            Common(arvJ, 1f);
            arvJ.region = ScatterRule.Region.AreaJogavel;
            arvJ.output = ScatterRule.Output.GameObject;
            arvJ.prefabs = W(arvores.ToArray());
            arvJ.densityPerHectare = 55f; arvJ.groupSize = new Vector2Int(1, 3); arvJ.groupRadius = 7f;
            arvJ.minDistance = 4.5f; arvJ.footprintFactor = 0.3f; arvJ.slopeRange = new Vector2(0f, 34f);
            arvJ.sinkFraction = 0.02f;

            // Fora da área jogável: tudo como Terrain Trees (leve; a mata densa é a barreira visual)
            var emF = Rule("Emergentes_Fora");
            Common(emF, 0.7f);
            emF.region = ScatterRule.Region.ForaDaAreaJogavel;
            emF.output = ScatterRule.Output.TerrainTree;
            emF.prefabs = W(emergentes.ToArray());
            emF.densityPerHectare = 5f; emF.groupSize = new Vector2Int(1, 2); emF.groupRadius = 6f;
            emF.minDistance = 8f; emF.footprintFactor = 0.4f; emF.slopeRange = new Vector2(0f, 32f);
            emF.patchScale = 150f; emF.patchiness = 0.4f;

            var arvF = Rule("Arvores_Fora");
            Common(arvF, 1f);
            arvF.region = ScatterRule.Region.ForaDaAreaJogavel;
            arvF.output = ScatterRule.Output.TerrainTree;
            arvF.prefabs = W(arvores.ToArray());
            arvF.densityPerHectare = 45f; arvF.groupSize = new Vector2Int(1, 3); arvF.groupRadius = 7f;
            arvF.minDistance = 4.5f; arvF.footprintFactor = 0.3f; arvF.slopeRange = new Vector2(0f, 38f);

            // Xique-xique em colônias no chão pedregoso e na borda dos lajedos
            var xx = AssetDatabase.LoadAssetAtPath<ScatterRule>($"{ScatterFolder}/Scatter_Cactos_XiqueXique.asset");
            if (xx != null)
            {
                Scatterer.Clear(xx);
                Common(xx, 0.6f);
                xx.invertVegetationField = true;   // prefere áreas mais abertas
                xx.output = ScatterRule.Output.TerrainTree;
                xx.region = ScatterRule.Region.Qualquer;
                xx.lajedoRange = new Vector2(0f, 0.5f);
                xx.densityPerHectare = 22f; xx.groupSize = new Vector2Int(6, 14); xx.groupRadius = 3.5f;
                xx.minDistance = 0.8f; xx.footprintFactor = 0.55f; xx.slopeRange = new Vector2(0f, 32f);
                xx.patchScale = 70f; xx.patchiness = 0.8f;
                EditorUtility.SetDirty(xx);
            }

            // Massas de arbustos: a camada principal, 60-80% de cobertura fora das clareiras
            var mata = Rule("Mata_Arbustos");
            Common(mata, 1f);
            mata.region = ScatterRule.Region.Qualquer;
            mata.output = ScatterRule.Output.TerrainTree;
            mata.prefabs = W((pf.mata[0], 1f), (pf.mata[1], 1f), (pf.mata[2], 1f), (pf.mata[3], 0.7f),
                             (pf.mataSeca[0], 0.5f), (pf.mataSeca[1], 0.4f), (P("Arbusto_Seco_01"), 0.15f), (P("Arbusto_Seco_02"), 0.15f));
            mata.densityPerHectare = 900f; mata.groupSize = new Vector2Int(1, 1); mata.groupRadius = 0f;
            mata.minDistance = 2.4f; mata.footprintFactor = 0.34f; mata.slopeRange = new Vector2(0f, 50f);

            // Arbustos na base das rochas: mesmas massas (o grupo é limpo junto com a mata)
            var arbR = AssetDatabase.LoadAssetAtPath<ScatterRule>($"{ScatterFolder}/Scatter_Arbustos_Rochas.asset");
            if (arbR != null)
            {
                Scatterer.Clear(arbR);
                arbR.prefabs = W((pf.mata[3], 1f), (pf.mataSeca[1], 0.8f));
                arbR.output = ScatterRule.Output.TerrainTree;
                arbR.densityPerHectare = 1.2f;
                EditorUtility.SetDirty(arbR);
            }

            // Ordem: rochas e cobertura da estrada primeiro, depois emergentes e árvores, colônias de cacto, e a mata preenche o resto
            var order = new List<string> { "Rochas_Grandes", "Rochas_Lajedos", "Rochas_Blocos", "Rochas_Estrada", "Arvore_Marco", "Cactos_Estrada",
                "Arbustos_Estrada", "Cactos_Mandacaru", "Arvores_Jogavel", "Emergentes_Fora", "Arvores_Fora", "Troncos_Secos",
                "Cactos_XiqueXique", "Arbustos_Rochas", "Coroa_De_Frade_Lajedos", "Coroa_De_Frade_Blocos", "Mata_Arbustos" };
            var all = new List<ScatterRule>(set.rules);
            foreach (var r in new[] { estrada, mand, arvJ, emF, arvF, mata }) if (!all.Contains(r)) all.Add(r);
            all.RemoveAll(r => r == null || !r.enabled);
            all.Sort((a, b) =>
            {
                int ia = order.IndexOf(a.GroupName), ib = order.IndexOf(b.GroupName);
                return (ia < 0 ? 999 : ia).CompareTo(ib < 0 ? 999 : ib);
            });
            set.rules = all;
            // MVP: toda a vegetação só no corredor da trilha principal; rochas no mapa todo
            foreach (var r in all) r.onlyNearTrail = !r.GroupName.StartsWith("Rochas_");
            foreach (var r in all) EditorUtility.SetDirty(r);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        // ================================================================== Terrain: render, detalhes, limite

        public static void ConfigureTerrainRendering()
        {
            var t = TerrainReliefGenerator.FindTerrain();
            t.treeDistance = 900f;
            t.treeBillboardDistance = 900f;      // os prefabs não têm billboard: o LODGroup de cada um faz o culling
            t.treeMaximumFullLODCount = 50000;
            t.detailObjectDistance = 65f;
            t.detailObjectDensity = 1f;
            t.drawInstanced = true;
            EditorUtility.SetDirty(t);
        }

        public static void PaintDetails(TerrainPaintSettings paint, Prefabs pf)
        {
            var field = VegetationField.Build(paint);
            if (field == null) return;
            var terrain = field.terrain; var td = field.td;
            DetailPrototype Proto(GameObject prefab, float minW, float maxW, float minH, float maxH) => new DetailPrototype
            {
                usePrototypeMesh = true, prototype = prefab, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = minW, maxWidth = maxW, minHeight = minH, maxHeight = maxH, noiseSpread = 0.35f,
                healthyColor = Color.white, dryColor = new Color(0.92f, 0.9f, 0.85f), useDensityScaling = true, alignToGround = 0.3f,
                positionJitter = 1f,
            };
            td.detailPrototypes = new[]
            {
                Proto(pf.capim01, 0.8f, 1.3f, 0.8f, 1.4f),
                Proto(pf.capim02, 0.8f, 1.2f, 0.8f, 1.3f),
                Proto(pf.macambira, 0.8f, 1.35f, 0.75f, 1.3f),
            };
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            int res = td.detailResolution;
            float cell = field.size.x / res;
            var capA = new int[res, res]; var capB = new int[res, res]; var mac = new int[res, res];
            var rng = new System.Random(paint.seed + 71);
            Vector2 off = new Vector2((float)rng.NextDouble() * 4000f, (float)rng.NextDouble() * 4000f);
            long nCap = 0, nMac = 0;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector2 p = new Vector2(field.origin.x + (x + 0.5f) * cell, field.origin.z + (y + 0.5f) * cell);
                float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                float slope = td.GetSteepness(u, v);
                if (slope > 34f) continue;
                if (field.RoadEdge(p) < VegetationField.RoadBare - 0.5f) continue;
                if (field.ReservedDistance(p) < 2f) continue;
                if (field.RiverDistance(p) < paint.relief.riverBedHalfWidth + 0.5f) continue;
                float trail = field.Trail(p);                     // MVP: só no corredor da trilha
                if (trail < 0.02f) continue;
                float cover = field.Cover(p);
                float laj = field.LajedoMask(p);
                double r = rng.NextDouble();

                // Macambira: tufos sobre e em volta dos lajedos; rara no chão pedregoso aberto
                float onLaj = laj > 0.02f ? Mathf.Lerp(0.9f, 0.45f, Mathf.InverseLerp(0.3f, 1f, laj)) : 0f;
                float clump = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.6f, TerrainPainter.Fbm((p + off) / 6f, 2)));
                float macW = trail * Mathf.Max(onLaj * clump * 2.2f, (1f - cover) * clump * 0.08f * Mathf.InverseLerp(6f, 20f, slope + 6f));
                int m = Mathf.FloorToInt(macW + (float)r);
                if (m > 0) { mac[y, x] = Mathf.Min(m, 3); nMac += mac[y, x]; }

                // Capim seco: manchas nas clareiras, beira de estrada e borda da mata; ralo dentro da mata
                if (laj > 0.4f) continue;
                float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.58f, TerrainPainter.Fbm((p - off) / 18f, 3)));
                float openness = Mathf.Lerp(1f, 0.25f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.9f, cover)));
                float capW = trail * patch * openness * 1.5f;
                int c = Mathf.FloorToInt(capW + (float)rng.NextDouble());
                if (c > 0)
                {
                    if (rng.NextDouble() < 0.65) capA[y, x] = Mathf.Min(c, 3); else capB[y, x] = Mathf.Min(c, 3);
                    nCap += c;
                }
            }
            td.SetDetailLayer(0, 0, 0, capA);
            td.SetDetailLayer(0, 0, 1, capB);
            td.SetDetailLayer(0, 0, 2, mac);
            EditorUtility.SetDirty(td);
            Debug.Log($"[Mata] Detalhes: {nCap} tufos de capim, {nMac} macambiras (PLACEHOLDER).");
        }

        /// <summary>Colisor só onde o jogador encosta: remove (override na instância) os colisores dos objetos do Scatter fora da área jogável.</summary>
        public static int StripCollidersOutsidePlayable(TerrainReliefSettings relief)
        {
            var terrain = TerrainReliefGenerator.FindTerrain();
            var size = terrain.terrainData.size; var o = terrain.transform.position;
            Vector2 c = new Vector2(o.x + size.x * 0.5f, o.z + size.z * 0.5f);
            float half = relief.playableHalfSize + 5f;
            var root = GameObject.Find($"{TerrainReliefGenerator.SceneryRootName}/{Scatterer.ScatterRootName}");
            if (root == null) return 0;
            int n = 0;
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                var p = col.transform.position;
                if (Mathf.Abs(p.x - c.x) < half && Mathf.Abs(p.z - c.y) < half) continue;
                try { Object.DestroyImmediate(col); }
                catch (System.Exception) { col.enabled = false; }   // se a instância não aceitar remoção, só desliga
                n++;
            }
            Debug.Log($"[Mata] {n} colisores removidos fora da área jogável.");
            return n;
        }

        /// <summary>Paredes invisíveis na borda da área jogável: a mata densa de fora é só cenário.</summary>
        public static void BuildPlayableLimit(TerrainReliefSettings relief)
        {
            var terrain = TerrainReliefGenerator.FindTerrain();
            var scenery = GameObject.Find(TerrainReliefGenerator.SceneryRootName);
            var old = scenery.transform.Find(LimitName);
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(LimitName);
            root.transform.SetParent(scenery.transform, false);
            var size = terrain.terrainData.size; var o = terrain.transform.position;
            Vector3 c = new Vector3(o.x + size.x * 0.5f, relief.baseHeight, o.z + size.z * 0.5f);
            float h = relief.playableHalfSize, height = 60f, thick = 2f;
            void Wall(string n, Vector3 pos, Vector3 sz)
            {
                var w = new GameObject(n);
                w.transform.SetParent(root.transform, false);
                w.transform.position = pos;
                w.AddComponent<BoxCollider>().size = sz;
                GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            }
            Wall("Limite_N", c + new Vector3(0f, height * 0.5f - 10f, h + thick * 0.5f), new Vector3(h * 2f + thick * 2f, height, thick));
            Wall("Limite_S", c + new Vector3(0f, height * 0.5f - 10f, -h - thick * 0.5f), new Vector3(h * 2f + thick * 2f, height, thick));
            Wall("Limite_L", c + new Vector3(h + thick * 0.5f, height * 0.5f - 10f, 0f), new Vector3(thick, height, h * 2f));
            Wall("Limite_O", c + new Vector3(-h - thick * 0.5f, height * 0.5f - 10f, 0f), new Vector3(thick, height, h * 2f));
        }

        /// <summary>Fração do chão coberta por copa (mata + árvores), na área jogável e fora das clareiras.</summary>
        public static string MeasureCoverage(TerrainPaintSettings paint, float step = 2f)
        {
            var field = VegetationField.Build(paint);
            var td = field.td;
            var hash = new Dictionary<(int, int), List<(Vector2 p, float r)>>();
            void Add(Vector2 p, float r)
            {
                var k = ((int)Mathf.Floor(p.x / 8f), (int)Mathf.Floor(p.y / 8f));
                if (!hash.TryGetValue(k, out var l)) hash[k] = l = new List<(Vector2, float)>();
                l.Add((p, r));
            }
            var protos = td.treePrototypes;
            var pr = new float[protos.Length];
            for (int i = 0; i < protos.Length; i++)
            {
                var b = EnvironmentPrefabFactory.LocalBounds(protos[i].prefab);
                pr[i] = Mathf.Min(b.extents.x, b.extents.z) * 0.9f;   // copa: raio menor da caixa
            }
            foreach (var ti in td.treeInstances)
                Add(new Vector2(field.origin.x + ti.position.x * field.size.x, field.origin.z + ti.position.z * field.size.z), pr[ti.prototypeIndex] * ti.widthScale);
            foreach (var g in new[] { "Arvores_Jogavel", "Cactos_Mandacaru" })
            {
                var grp = Scatterer.FindGroup(g, false);
                if (grp == null) continue;
                foreach (Transform t in grp)
                {
                    var b = EnvironmentPrefabFactory.LocalBounds(t.gameObject);
                    Add(new Vector2(t.position.x, t.position.z), Mathf.Min(b.extents.x, b.extents.z) * t.localScale.x * 0.8f);
                }
            }
            bool Covered(Vector2 p)
            {
                int cx = (int)Mathf.Floor(p.x / 8f), cy = (int)Mathf.Floor(p.y / 8f);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (hash.TryGetValue((cx + dx, cy + dy), out var l))
                        foreach (var (q, r) in l) if ((q - p).sqrMagnitude < r * r) return true;
                return false;
            }
            int nPlay = 0, cPlay = 0, nMata = 0, cMata = 0, nAll = 0, cAll = 0;
            for (float x = field.origin.x + 2f; x < field.origin.x + field.size.x - 2f; x += step)
            for (float z = field.origin.z + 2f; z < field.origin.z + field.size.z - 2f; z += step)
            {
                var p = new Vector2(x, z);
                bool cov = Covered(p);
                nAll++; if (cov) cAll++;
                if (field.InPlayable(p)) { nPlay++; if (cov) cPlay++; }
                // fora das clareiras: longe da estrada/vila/datacenter, fora de lajedo, leito e rocha
                if (field.Trail(p) > 0.95f && field.RoadEdge(p) > VegetationField.RoadOpen && field.ReservedDistance(p) > 20f
                    && field.Cover(p) > 0.25f) { nMata++; if (cov) cMata++; }
            }
            return $"cobertura: mapa {100f * cAll / nAll:F0}% | área jogável {100f * cPlay / nPlay:F0}% | corredor da trilha fora das clareiras {100f * cMata / Mathf.Max(1, nMata):F0}% | terrain trees {td.treeInstanceCount}";
        }
    }
}
