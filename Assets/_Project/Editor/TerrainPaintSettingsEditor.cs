using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    [CustomEditor(typeof(TerrainPaintSettings))]
    public class TerrainPaintSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Sobrescreve o splatmap do Terrain da cena aberta (com Undo). Retoques à mão com o pincel de Paint Texture continuam possíveis, mas somem ao pintar de novo.", MessageType.Info);
            var s = (TerrainPaintSettings)target;
            if (GUILayout.Button("Pintar solo", GUILayout.Height(28)))
                TerrainPainter.Paint(s);
            if (GUILayout.Button("Pintar capim seco (detalhe)"))
                TerrainPainter.PaintGrass(s);
            if (GUILayout.Button("Recriar texturas placeholder e TerrainLayers"))
            {
                s.layers = SoilTextureLibrary.EnsureLayers(true);
                EditorUtility.SetDirty(s);
            }
        }
    }
}
