using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    [CustomEditor(typeof(ScatterSet))]
    public class ScatterSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var set = (ScatterSet)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Gera na cena aberta, em Cenario/Scatter/<categoria>. A ordem da lista define quem ocupa espaço primeiro. Mesma seed = mesmo resultado.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gerar tudo", GUILayout.Height(28))) Scatterer.GenerateAll(set);
                if (GUILayout.Button("Limpar tudo", GUILayout.Height(28))) Scatterer.ClearAll(set);
            }
            EditorGUILayout.Space();
            foreach (var rule in set.rules)
            {
                if (rule == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var g = Scatterer.FindGroup(rule.GroupName, false);
                    EditorGUILayout.LabelField($"{rule.GroupName} ({(g ? g.childCount : 0)})", GUILayout.MinWidth(140));
                    if (GUILayout.Button("Gerar", GUILayout.Width(60))) Scatterer.Generate(set, rule);
                    if (GUILayout.Button("Limpar", GUILayout.Width(60))) Scatterer.Clear(rule);
                }
            }
        }
    }

    [CustomEditor(typeof(ScatterRule))]
    public class ScatterRuleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rule = (ScatterRule)target;
            EditorGUILayout.Space();
            var set = FindSet(rule);
            if (set == null)
            {
                EditorGUILayout.HelpBox("Adicione esta regra a um Scatter Set para gerar.", MessageType.Warning);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gerar esta categoria", GUILayout.Height(26))) Scatterer.Generate(set, rule);
                if (GUILayout.Button("Limpar", GUILayout.Height(26))) Scatterer.Clear(rule);
            }
        }

        static ScatterSet FindSet(ScatterRule rule)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ScatterSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<ScatterSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (set != null && set.rules.Contains(rule)) return set;
            }
            return null;
        }
    }
}
