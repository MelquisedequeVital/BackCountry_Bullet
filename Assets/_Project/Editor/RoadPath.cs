using System.Collections.Generic;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Curva Catmull-Rom que passa pelos pontos filhos do marcador Estrada.</summary>
    public static class RoadPath
    {
        public static List<Transform> GetControlPoints(Transform estrada)
        {
            var pts = new List<Transform>();
            if (estrada == null) return pts;
            foreach (Transform child in estrada) pts.Add(child);
            return pts;
        }

        /// <summary>Amostra a curva em passos de aproximadamente <paramref name="step"/> metros.</summary>
        public static List<Vector3> Sample(IReadOnlyList<Transform> controlPoints, float step)
        {
            var result = new List<Vector3>();
            int n = controlPoints.Count;
            if (n == 0) return result;
            if (n == 1) { result.Add(controlPoints[0].position); return result; }
            step = Mathf.Max(0.1f, step);

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)].position;
                Vector3 p1 = controlPoints[i].position;
                Vector3 p2 = controlPoints[i + 1].position;
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, n - 1)].position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int s = 0; s < steps; s++)
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }
            result.Add(controlPoints[n - 1].position);
            return result;
        }

        public static float Length(IReadOnlyList<Vector3> samples)
        {
            float len = 0f;
            for (int i = 1; i < samples.Count; i++) len += Vector3.Distance(samples[i - 1], samples[i]);
            return len;
        }

        /// <summary>Distância horizontal (XZ) do ponto à polilinha amostrada.</summary>
        public static float DistanceXZ(IReadOnlyList<Vector3> samples, Vector3 point)
        {
            float best = float.MaxValue;
            var p = new Vector2(point.x, point.z);
            for (int i = 1; i < samples.Count; i++)
            {
                var a = new Vector2(samples[i - 1].x, samples[i - 1].z);
                var b = new Vector2(samples[i].x, samples[i].z);
                var ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
