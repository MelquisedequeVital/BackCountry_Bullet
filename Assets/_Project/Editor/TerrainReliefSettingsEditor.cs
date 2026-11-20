using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    [CustomEditor(typeof(TerrainReliefSettings))]
    public class TerrainReliefSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Sobrescreve o heightmap do Terrain da cena aberta (com Undo). Mesma seed = mesmo relevo.", MessageType.Info);
            if (GUILayout.Button("Gerar relevo", GUILayout.Height(28)))
                TerrainReliefGenerator.Generate((TerrainReliefSettings)target);
            if (GUILayout.Button("Recriar malhas dos arcos de pedra"))
            {
                var s = (TerrainReliefSettings)target;
                RockArchBuilder.EnsurePrefabs(s.rockArchMaterial != null ? s.rockArchMaterial : RockArchBuilder.EnsureMaterial(), true);
            }
        }
    }
}
