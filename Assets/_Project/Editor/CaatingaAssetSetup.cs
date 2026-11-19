using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Monta prefabs de cenário e as regras de scatter padrão. Pode ser rodado de novo sem perder ajustes nas regras.</summary>
    public static class CaatingaAssetSetup
    {
        const string VegetationFbx = "Assets/Vegetation/Vegetation/Models/Vegetation.fbx";
        public const string ScatterFolder = "Assets/_Project/Environment/Scatter";

        // Granito do pack é escuro e frio: clareia e esquenta
        static readonly Color GraniteTint = new Color(1.18f, 1.06f, 0.92f);

        [MenuItem("Tools/Backcountry Bullet/Recriar prefabs de rocha")]
        public static void BuildRockPrefabs() => RockPrefabs(true);

        /// <summary>Retorna os prefabs por categoria. Só recria os que faltam, a menos que <paramref name="rebuild"/>.</summary>
        public static Dictionary<string, List<GameObject>> RockPrefabs(bool rebuild)
        {
            var tweak = new EnvironmentPrefabFactory.MaterialTweak { tint = GraniteTint, suffix = "_Granito" };
            var box = EnvironmentPrefabFactory.ColliderKind.Box;
            var spec = new (string cat, string child, string name, Vector3 scale)[]
            {
                // blocos de granito arredondados (1–3 m)
                ("Blocos", "Rock",    "Rocha_Bloco_01", Vector3.one * 0.6f),
                ("Blocos", "Rock_01", "Rocha_Bloco_02", Vector3.one * 0.45f),
                ("Blocos", "Rock_04", "Rocha_Bloco_03", Vector3.one * 0.5f),
                ("Blocos", "Rock_07", "Rocha_Bloco_04", Vector3.one * 1.1f),
                ("Blocos", "Rock_03", "Rocha_Bloco_05", Vector3.one * 0.32f),
                // lajedos: afloramentos planos e largos
                ("Lajedos", "Rock_05", "Rocha_Lajedo_01", new Vector3(4.5f, 0.6f, 4.5f)),
                ("Lajedos", "Rock_06", "Rocha_Lajedo_02", new Vector3(3.6f, 0.5f, 3.6f)),
                ("Lajedos", "Rock_08", "Rocha_Lajedo_03", new Vector3(5f, 0.6f, 5f)),
                ("Lajedos", "Rock_10", "Rocha_Lajedo_04", new Vector3(2.4f, 0.4f, 2.4f)),
                // rochas grandes para serras e inselbergs
                ("Grandes", "Rock_09", "Rocha_Grande_01", Vector3.one * 1.4f),
                ("Grandes", "Rock_02", "Rocha_Grande_02", Vector3.one * 1.6f),
                ("Grandes", "Rock_03", "Rocha_Grande_03", Vector3.one * 1.2f),
                ("Grandes", "Rock_01", "Rocha_Grande_04", Vector3.one * 1.5f),
            };
            var result = new Dictionary<string, List<GameObject>>();
            foreach (var s in spec)
            {
                string path = $"{EnvironmentPrefabFactory.PrefabFolder}/{s.name}.prefab";
                var prefab = rebuild ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                // blocos e lajedos somem de longe; rochas grandes (serras) ficam sempre visíveis
                var opt = new EnvironmentPrefabFactory.Options { collider = box, cullScreenHeight = s.cat == "Grandes" ? 0f : (s.cat == "Lajedos" ? 0.008f : 0.012f) };
                if (prefab == null) prefab = EnvironmentPrefabFactory.Create(VegetationFbx, s.child, s.name, s.scale, tweak, opt);
                if (!result.TryGetValue(s.cat, out var list)) result[s.cat] = list = new List<GameObject>();
                if (prefab != null) list.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        public static ScatterSet EnsureScatterSet(TerrainReliefSettings relief)
        {
            EnvironmentPrefabFactory.EnsureFolder(ScatterFolder);
            string path = $"{ScatterFolder}/Scatter_Caatinga.asset";
            var set = AssetDatabase.LoadAssetAtPath<ScatterSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ScatterSet>();
                set.relief = relief;
                AssetDatabase.CreateAsset(set, path);
            }
            return set;
        }

        /// <summary>Cria a regra se não existir (não sobrescreve ajustes) e garante que está no set.</summary>
        public static ScatterRule EnsureRule(ScatterSet set, string assetName, System.Action<ScatterRule> defaults)
        {
            string path = $"{ScatterFolder}/{assetName}.asset";
            var rule = AssetDatabase.LoadAssetAtPath<ScatterRule>(path);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<ScatterRule>();
                defaults(rule);
                AssetDatabase.CreateAsset(rule, path);
            }
            if (!set.rules.Contains(rule)) { set.rules.Add(rule); EditorUtility.SetDirty(set); }
            return rule;
        }

        public static List<ScatterRule.WeightedPrefab> Weighted(IEnumerable<GameObject> prefabs)
        {
            var l = new List<ScatterRule.WeightedPrefab>();
            foreach (var p in prefabs) l.Add(new ScatterRule.WeightedPrefab { prefab = p, weight = 1f });
            return l;
        }
    }
}
