using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Desenha na Scene view as áreas reservadas, a estrada e os spots de _Layout.
    /// Só editor: os marcadores continuam GameObjects vazios na cena.
    /// </summary>
    [InitializeOnLoad]
    public static class LayoutGizmoDrawer
    {
        const string SettingsPath = "Assets/_Project/Environment/CaatingaLayout.asset";
        static CaatingaLayoutSettings _settings;

        static LayoutGizmoDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static CaatingaLayoutSettings Settings
        {
            get
            {
                if (_settings == null) _settings = AssetDatabase.LoadAssetAtPath<CaatingaLayoutSettings>(SettingsPath);
                return _settings;
            }
        }

        static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            var root = GameObject.Find(CaatingaLayoutSettings.LayoutRootName);
            if (root == null) return;
            var s = Settings;
            if (s == null) return;

            var t = root.transform;
            DrawArea(t.Find(CaatingaLayoutSettings.VilaName), s.vilaSize, new Color(1f, 0.75f, 0.2f));
            DrawArea(t.Find(CaatingaLayoutSettings.DatacenterName), s.datacenterSize, new Color(0.3f, 0.8f, 1f));
            DrawSpot(t.Find(CaatingaLayoutSettings.VilaName + "/" + CaatingaLayoutSettings.BarName), new Color(1f, 0.5f, 0.1f));
            DrawSpot(t.Find(CaatingaLayoutSettings.PortaoName), new Color(1f, 0.3f, 0.3f));
            DrawRoad(t.Find(CaatingaLayoutSettings.EstradaName), s);
        }

        static void DrawArea(Transform area, Vector2 size, Color color)
        {
            if (area == null) return;
            Vector3 c = area.position;
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            var verts = new[]
            {
                c + new Vector3(-hx, 0.5f, -hz), c + new Vector3(-hx, 0.5f, hz),
                c + new Vector3(hx, 0.5f, hz), c + new Vector3(hx, 0.5f, -hz)
            };
            Handles.DrawSolidRectangleWithOutline(verts, new Color(color.r, color.g, color.b, 0.08f), color);
            Handles.Label(c + Vector3.up * 3f, area.name, EditorStyles.whiteBoldLabel);
        }

        static void DrawSpot(Transform spot, Color color)
        {
            if (spot == null) return;
            Handles.color = color;
            Handles.DrawWireDisc(spot.position, Vector3.up, 2f, 3f);
            Handles.DrawLine(spot.position, spot.position + Vector3.up * 6f, 3f);
            Handles.Label(spot.position + Vector3.up * 7f, spot.name, EditorStyles.whiteBoldLabel);
        }

        static void DrawRoad(Transform estrada, CaatingaLayoutSettings s)
        {
            if (estrada == null) return;
            var pts = RoadPath.GetControlPoints(estrada);
            if (pts.Count < 2) return;
            var samples = RoadPath.Sample(pts, s.roadSampleStep * 2f);
            float half = s.roadWidth * 0.5f;
            var left = new Vector3[samples.Count];
            var right = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 dir = samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)];
                dir.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * half;
                left[i] = samples[i] - side + Vector3.up * 0.3f;
                right[i] = samples[i] + side + Vector3.up * 0.3f;
            }
            Handles.color = new Color(0.85f, 0.6f, 0.35f);
            Handles.DrawAAPolyLine(3f, left);
            Handles.DrawAAPolyLine(3f, right);
            foreach (var p in pts) Handles.DrawWireDisc(p.position, Vector3.up, 1f);
            Handles.Label(samples[samples.Count / 2] + Vector3.up * 4f,
                $"{estrada.name} ({RoadPath.Length(samples):F0} m)", EditorStyles.whiteBoldLabel);
        }
    }
}
