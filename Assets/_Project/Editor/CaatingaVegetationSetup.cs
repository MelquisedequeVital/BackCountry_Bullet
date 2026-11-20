using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Part = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.Part;
using Tweak = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.MaterialTweak;
using Opt = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.Options;
using Col = BackcountryBullet.EditorTools.EnvironmentPrefabFactory.ColliderKind;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Prefabs de vegetação da caatinga montados a partir dos packs instalados.</summary>
    public static class CaatingaVegetationSetup
    {
        const string VegFbx = "Assets/Vegetation/Vegetation/Models/Vegetation.fbx";
        const string Cactus1 = "Assets/vegetacoes1/FBX/Cactus_1.fbx";
        const string Cactus3 = "Assets/vegetacoes1/FBX/Cactus_3.fbx";
        const string Cactus4 = "Assets/vegetacoes1/FBX/Cactus_4.fbx";

        // Cactus_3 na escala de importação: 0,39 x 0,72 m
        const float C3W = 0.39f, C3H = 0.72f;

        [MenuItem("Tools/Backcountry Bullet/Recriar prefabs de vegetação")]
        public static void Rebuild() => VegetationPrefabs(true);

        public static Dictionary<string, List<GameObject>> VegetationPrefabs(bool rebuild)
        {
            var result = new Dictionary<string, List<GameObject>>();
            void Add(string cat, string name, System.Func<GameObject> build)
            {
                var path = $"{EnvironmentPrefabFactory.PrefabFolder}/{name}.prefab";
                var p = rebuild ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p == null) p = build();
                if (!result.TryGetValue(cat, out var l)) result[cat] = l = new List<GameObject>();
                if (p != null) l.Add(p);
            }

            // ---------------- árvores
            var seca = new Tweak { alphaClip = true, tint = new Color(1.05f, 1f, 0.95f), suffix = "_Seca" };
            var clara = new Tweak { alphaClip = true, tint = new Color(0.95f, 0.9f, 0.84f), suffix = "_Clara" };
            var rala = new Tweak { alphaClip = true, alphaCutoff = 0.62f, tint = new Color(1.3f, 1.0f, 0.5f), suffix = "_Rala" }; // folhagem rala, amarelada
            var marco = new Tweak { alphaClip = true, tint = new Color(0.95f, 1.02f, 0.78f), suffix = "_Marco" };
            var tree = new Opt { collider = Col.Tronco, trunkRadius = 0.22f, trunkHeight = 2.5f, cullScreenHeight = 0.012f };

            Add("ArvoresSecas", "Arvore_Seca_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_03", "Arvore_Seca_01", Vector3.one * 0.3f, seca, tree));
            Add("ArvoresSecas", "Arvore_Seca_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_04", "Arvore_Seca_02", Vector3.one * 0.3f, seca, tree));
            Add("ArvoresSecas", "Arvore_Seca_03", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_05", "Arvore_Seca_03", Vector3.one * 0.25f, seca, tree));
            Add("ArvoresSecas", "Arvore_Seca_04", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_06", "Arvore_Seca_04", Vector3.one * 0.27f, seca, tree));
            Add("ArvoresSecas", "Arvore_Seca_05", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B", "Arvore_Seca_05", Vector3.one * 0.85f, seca, tree));
            Add("ArvoresSecas", "Arvore_Seca_Clara_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_04_N", "Arvore_Seca_Clara_01", Vector3.one * 0.27f, clara, tree));
            Add("ArvoresSecas", "Arvore_Seca_Clara_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B.002", "Arvore_Seca_Clara_02", Vector3.one * 0.8f, clara, tree));

            Add("ArvoresRalas", "Arvore_Rala_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_04_V", "Arvore_Rala_01", Vector3.one * 0.3f, rala, tree));
            Add("ArvoresRalas", "Arvore_Rala_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_03_V", "Arvore_Rala_02", Vector3.one * 0.32f, rala, tree));
            Add("ArvoresRalas", "Arvore_Rala_03", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_06_V", "Arvore_Rala_03", Vector3.one * 0.27f, rala, tree));
            Add("ArvoresRalas", "Arvore_Rala_04", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B.001", "Arvore_Rala_04", Vector3.one * 0.85f, rala, tree));

            Add("ArvoreMarco", "Arvore_Juazeiro", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_05_V", "Arvore_Juazeiro", Vector3.one * 0.3f, marco,
                new Opt { collider = Col.Tronco, trunkRadius = 0.35f, trunkHeight = 3f, cullScreenHeight = 0.006f }));

            // ---------------- troncos caídos
            var logOpt = new Opt { collider = Col.Box, colliderShrink = 0.8f, cullScreenHeight = 0.025f, castShadows = false };
            Add("Troncos", "Tronco_Seco_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Trunk_01", "Tronco_Seco_01", Vector3.one * 0.6f, seca, logOpt));
            Add("Troncos", "Tronco_Seco_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Trunk_02", "Tronco_Seco_02", Vector3.one * 0.5f, seca, logOpt));
            Add("Troncos", "Galho_Seco_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Branch_C", "Galho_Seco_01", Vector3.one * 0.45f, seca,
                new Opt { collider = Col.Nenhum, cullScreenHeight = 0.02f, castShadows = false }));

            // ---------------- arbustos (touceiras espinhosas)
            var shrub = new Opt { collider = Col.Nenhum, cullScreenHeight = 0.025f, castShadows = false };
            Add("ArbustosSecos", "Arbusto_Seco_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B", "Arbusto_Seco_01", Vector3.one * 0.38f, seca, shrub));
            Add("ArbustosSecos", "Arbusto_Seco_02", () => EnvironmentPrefabFactory.Create(VegFbx, "Plane.016", "Arbusto_Seco_02", Vector3.one * 0.5f, seca, shrub));
            Add("ArbustosSecos", "Arbusto_Seco_03", () => EnvironmentPrefabFactory.Create(VegFbx, "Plane.014", "Arbusto_Seco_03", Vector3.one * 0.85f, seca, shrub));
            Add("ArbustosSecos", "Arbusto_Ralo_01", () => EnvironmentPrefabFactory.Create(VegFbx, "Tree_B.001", "Arbusto_Ralo_01", Vector3.one * 0.4f, rala, shrub));
            // bush_1 (Into The Wild) ficou de fora: verde cartunesco, fora do clima e do estilo.

            // ---------------- cactos (montados com as peças do pack vegetacoes1)
            var cactoTint = new Tweak { tint = new Color(1.05f, 1.02f, 1.4f), suffix = "_Cacto" }; // verde-azulado acinzentado
            var big = new Opt { collider = Col.Capsule, colliderShrink = 0.35f, cullScreenHeight = 0.012f };
            Add("Mandacarus", "Cacto_Mandacaru_01", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_01",
                Mandacaru(4.2f, new[] { (1.5f, 0f, 1.9f), (2.0f, 180f, 1.6f) }, cactoTint), big));
            Add("Mandacarus", "Cacto_Mandacaru_02", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_02",
                Mandacaru(4.8f, new[] { (1.4f, 10f, 2.2f), (1.9f, 135f, 2.0f), (2.5f, 250f, 1.7f) }, cactoTint), big));
            Add("Mandacarus", "Cacto_Mandacaru_03", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Mandacaru_03",
                Mandacaru(5.5f, new[] { (1.3f, 20f, 2.4f), (1.7f, 110f, 2.6f), (2.2f, 200f, 2.1f), (2.7f, 290f, 1.9f) }, cactoTint), big));

            var mid = new Opt { collider = Col.Capsule, colliderShrink = 0.4f, cullScreenHeight = 0.02f };
            Add("Facheiros", "Cacto_Facheiro_01", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Facheiro_01",
                Facheiro(new[] { (Vector2.zero, 3.4f, 0f) }, cactoTint), mid));
            Add("Facheiros", "Cacto_Facheiro_02", () => EnvironmentPrefabFactory.CreateComposite("Cacto_Facheiro_02",
                Facheiro(new[] { (Vector2.zero, 3.6f, 0f), (new Vector2(0.35f, 0.15f), 2.7f, 6f), (new Vector2(-0.2f, 0.32f), 2.1f, -5f) }, cactoTint), mid));

            var small = new Opt { collider = Col.Nenhum, cullScreenHeight = 0.035f, castShadows = false };
            Add("XiqueXiques", "Cacto_XiqueXique_01", () => EnvironmentPrefabFactory.CreateComposite("Cacto_XiqueXique_01", XiqueXique(7, 11, cactoTint), small));
            Add("XiqueXiques", "Cacto_XiqueXique_02", () => EnvironmentPrefabFactory.CreateComposite("Cacto_XiqueXique_02", XiqueXique(9, 29, cactoTint), small));

            var pink = FindMaterial(Cactus4, "Pink");
            var cap = new Tweak { suffix = "_Cacto", remap = m => pink != null ? pink : m };
            var tiny = new Opt { collider = Col.Nenhum, cullScreenHeight = 0.05f, castShadows = false };
            Add("CoroasDeFrade", "Cacto_CoroaDeFrade_01", () => EnvironmentPrefabFactory.CreateComposite("Cacto_CoroaDeFrade_01", new List<Part>
            {
                new Part { model = Cactus1, position = Vector3.zero, euler = Vector3.zero, scale = new Vector3(4.2f, 1.6f, 4.2f), tweak = cactoTint },
                new Part { model = Cactus1, position = new Vector3(0f, 0.24f, 0f), euler = Vector3.zero, scale = new Vector3(2.2f, 0.4f, 2.2f), tweak = cap },
            }, tiny));

            AssetDatabase.SaveAssets();
            return result;
        }

        internal static List<Part> Mandacaru(float trunkH, (float h, float ang, float armH)[] arms, Tweak tw)
        {
            var parts = new List<Part>
            {
                new Part { model = Cactus3, position = Vector3.zero, euler = Vector3.zero, scale = new Vector3(1.6f, trunkH / C3H, 1.6f), tweak = tw }
            };
            const float elbow = 0.75f;
            foreach (var (h, ang, armH) in arms)
            {
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.right;
                // cotovelo horizontal saindo do tronco
                parts.Add(new Part
                {
                    model = Cactus3, position = dir * 0.1f + Vector3.up * h,
                    euler = Quaternion.FromToRotation(Vector3.up, dir).eulerAngles,
                    scale = new Vector3(1.15f, elbow / C3H, 1.15f), tweak = tw
                });
                // braço subindo, levemente aberto
                parts.Add(new Part
                {
                    model = Cactus3, position = dir * (0.1f + elbow - 0.22f) + Vector3.up * (h - 0.22f),
                    euler = (Quaternion.AngleAxis(-5f, Vector3.Cross(dir, Vector3.up)) ).eulerAngles,
                    scale = new Vector3(1.25f, armH / C3H, 1.25f), tweak = tw
                });
            }
            return parts;
        }

        internal static List<Part> Facheiro((Vector2 off, float h, float tilt)[] cols, Tweak tw)
        {
            var parts = new List<Part>();
            foreach (var (off, h, tilt) in cols)
                parts.Add(new Part { model = Cactus3, position = new Vector3(off.x, 0f, off.y), euler = new Vector3(tilt, 0f, tilt * 0.5f),
                    scale = new Vector3(0.95f, h / C3H, 0.95f), tweak = tw });
            return parts;
        }

        /// <summary>Xique-xique: ramos baixos e tortos espalhados, com algumas pontas subindo.</summary>
        static List<Part> XiqueXique(int branches, int seed, Tweak tw)
        {
            var rng = new System.Random(seed);
            var parts = new List<Part>();
            for (int i = 0; i < branches; i++)
            {
                float ang = i * 360f / branches + (float)(rng.NextDouble() * 30 - 15);
                float tilt = 58f + (float)rng.NextDouble() * 20f;
                float len = 0.9f + (float)rng.NextDouble() * 0.6f;
                var rot = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(0f, 0f, -tilt);
                parts.Add(new Part { model = Cactus3, position = new Vector3(0f, 0.05f, 0f), euler = rot.eulerAngles,
                    scale = new Vector3(1f, len / C3H, 1f), tweak = tw });
                if (rng.NextDouble() < 0.6)
                {
                    Vector3 tip = rot * Vector3.up * (len - 0.15f) + new Vector3(0f, 0.05f, 0f);
                    float tipTilt = 10f + (float)rng.NextDouble() * 25f;
                    var rot2 = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(0f, 0f, -tipTilt);
                    parts.Add(new Part { model = Cactus3, position = tip, euler = rot2.eulerAngles,
                        scale = new Vector3(0.95f, (0.45f + (float)rng.NextDouble() * 0.35f) / C3H, 0.95f), tweak = tw });
                }
            }
            return parts;
        }

        static Material FindMaterial(string modelPath, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Material m && m.name == name) return m;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (go != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials) if (m != null && m.name == name) return m;
            return null;
        }
    }
}
