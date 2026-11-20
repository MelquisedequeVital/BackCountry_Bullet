using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Gera malhas de arco de pedra vazado (estilo Pedra Furada) e os prefabs correspondentes.
    /// O arco abre ao longo do eixo X local; olha-se através dele pelo eixo Z.
    /// </summary>
    public static class RockArchBuilder
    {
        public const string MeshFolder = "Assets/_Project/Environment/Meshes";
        public const string PrefabFolder = "Assets/_Project/Environment/Prefabs";
        public const string MaterialPath = "Assets/_Project/Environment/Materials/M_Rocha_Arenito.mat";
        const string SandstoneTexture = ProceduralTextures.Folder + "/T_Arenito_PLACEHOLDER.png";

        public struct ArchShape
        {
            public string name;
            public float span, height, thickness, depth;
            public int seed;
        }

        public static readonly ArchShape[] Variants =
        {
            new ArchShape { name = "Arco_Pedra_01", span = 30f, height = 22f, thickness = 8f, depth = 11f, seed = 11 },
            new ArchShape { name = "Arco_Pedra_02", span = 22f, height = 18f, thickness = 6.5f, depth = 9f, seed = 23 },
            new ArchShape { name = "Arco_Pedra_03", span = 38f, height = 26f, thickness = 10f, depth = 13f, seed = 37 },
        };

        public static Material EnsureMaterial()
        {
            // Textura própria: as texturas de rocha do pack são atlas de UV e não repetem bem.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SandstoneTexture);
            if (tex == null) tex = ProceduralTextures.CreateTileable("T_Arenito_PLACEHOLDER", 512, ProceduralTextures.Sandstone);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool created = mat == null;
            if (created)
            {
                EnsureFolder("Assets/_Project/Environment/Materials");
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Rocha_Arenito" };
            }
            mat.enableInstancing = true;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.88f, 0.82f, 0.76f)); // arenito menos saturado, perto da rocha das serras
            // Rocha fosca: sem brilho especular nem reflexo do céu (evitava um halo branco nos arcos)
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (created) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Cria (ou recria, se <paramref name="rebuild"/>) malhas e prefabs. Retorna os prefabs.</summary>
        public static List<GameObject> EnsurePrefabs(Material mat, bool rebuild)
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);
            var list = new List<GameObject>();
            foreach (var v in Variants)
            {
                string prefabPath = $"{PrefabFolder}/{v.name}.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null && !rebuild) { list.Add(existing); continue; }

                string meshPath = $"{MeshFolder}/{v.name}.asset";
                var mesh = BuildMesh(v, out var colliders);
                var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (oldMesh != null)
                {
                    // Atualiza pela API de Mesh (CopySerialized não reenviava os dados à GPU no editor)
                    oldMesh.Clear();
                    oldMesh.SetVertices(mesh.vertices);
                    oldMesh.SetNormals(mesh.normals);
                    oldMesh.SetTangents(mesh.tangents);
                    oldMesh.SetUVs(0, mesh.uv);
                    oldMesh.SetTriangles(mesh.triangles, 0);
                    oldMesh.RecalculateBounds();
                    EditorUtility.SetDirty(oldMesh);
                    Object.DestroyImmediate(mesh);
                    mesh = oldMesh;
                }
                else AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject(v.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                foreach (var c in colliders)
                {
                    var child = new GameObject("Col");
                    child.transform.SetParent(go.transform, false);
                    child.transform.localPosition = c.center;
                    child.transform.localRotation = c.rotation;
                    child.AddComponent<BoxCollider>().size = c.size;
                }
                foreach (var t in go.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                list.Add(PrefabUtility.SaveAsPrefabAsset(go, prefabPath));
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        struct BoxSpec { public Vector3 center, size; public Quaternion rotation; }

        static Mesh BuildMesh(ArchShape a, out List<BoxSpec> boxes)
        {
            const int rings = 90, seg = 28;
            const float legDepth = 7f;        // quanto a perna entra no chão
            var rng = new System.Random(a.seed);
            Vector3 noiseOff = new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);
            float asym = Mathf.Lerp(-0.12f, 0.12f, (float)rng.NextDouble());
            float legBias = Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble()); // pernas de espessuras diferentes

            // Caminho: perna esquerda enterrada -> arco -> perna direita enterrada
            var path = new List<Vector3>();
            var thick = new List<float>();
            var deep = new List<float>();
            int legSteps = 6;
            float half = a.span * 0.5f;
            for (int i = legSteps; i >= 1; i--)
            {
                float y = -legDepth * i / legSteps;
                path.Add(new Vector3(-half, y, 0f)); thick.Add(a.thickness * 2.2f * legBias); deep.Add(a.depth * 1.6f);
            }
            for (int i = 0; i <= rings; i++)
            {
                float u = i / (float)rings;
                float th = Mathf.PI * (1f - u);
                float s = Mathf.Max(0f, Mathf.Sin(th)); // Sin(PI) em float sai levemente negativo -> Pow daria NaN
                float x = half * Mathf.Cos(th) + asym * a.span * s * s;
                float y = a.height * Mathf.Pow(s, 0.7f);
                path.Add(new Vector3(x, y, 0f));
                // pernas grossas com sopé, topo mais fino e achatado
                float foot = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(y / (a.height * 0.35f)));
                thick.Add(a.thickness * Mathf.Lerp(1.15f, 1.9f, foot) * (x < 0f ? legBias : 1f));
                deep.Add(a.depth * Mathf.Lerp(0.9f, 1.35f, foot));
            }
            for (int i = 1; i <= legSteps; i++)
            {
                float y = -legDepth * i / legSteps;
                path.Add(new Vector3(half, y, 0f)); thick.Add(a.thickness * 2.2f); deep.Add(a.depth * 1.6f);
            }

            int n = path.Count;
            var verts = new List<Vector3>(n * (seg + 1));
            var uvs = new List<Vector2>(n * (seg + 1));
            var tris = new List<int>();
            var offsets = new Vector3[n * (seg + 1)];
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 normal = Vector3.Cross(Vector3.forward, tangent).normalized; // no plano XY, para fora do arco
                if (Vector3.Dot(normal, path[i] - new Vector3(0f, a.height * 0.3f, 0f)) < 0f) normal = -normal;
                for (int j = 0; j <= seg; j++)
                {
                    float phi = j / (float)seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
                    // seção quase retangular de cantos arredondados
                    float cx = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.45f);
                    float sz = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 0.45f);
                    Vector3 basePos = path[i] + normal * cx * thick[i] * 0.5f + Vector3.forward * sz * deep[i] * 0.5f;
                    float nz = (Noise3((basePos + noiseOff) / 9f) - 0.5f) * 2.4f;     // Noise3 tem pouca variância: normaliza
                    float nz2 = (Noise3((basePos + noiseOff * 1.7f) / 3.5f) - 0.5f) * 2.4f;
                    float ledge = Mathf.Sin((basePos.y + nz * 2f) * Mathf.PI * 2f / 2.6f) * 0.05f; // ressaltos de estrato
                    float disp = Mathf.Max(0.35f, 1f + nz * 0.45f + nz2 * 0.18f + ledge);
                    // abaixo do chão a perna só alarga: evita beiral virado para baixo (sombra preta no pé)
                    if (path[i].y < -0.01f) disp = Mathf.Max(disp, 1.45f);
                    offsets[i * (seg + 1) + j] = (normal * cx * thick[i] * 0.5f + Vector3.forward * sz * deep[i] * 0.5f) * disp;
                }
            }
            int stride = seg + 1;

            // Pernas: do chão até ~45% da altura a seção só pode afinar subindo (sem beiral virado para baixo)
            int crown = 0;
            for (int i = 1; i < n; i++) if (path[i].y > path[crown].y) crown = i;
            void Clamp(int i, int below)
            {
                if (path[i].y < 0f || path[i].y > a.height * 0.45f) return;
                for (int j = 0; j < stride; j++)
                {
                    float lim = offsets[below * stride + j].magnitude;
                    var o = offsets[i * stride + j];
                    if (o.magnitude > lim) offsets[i * stride + j] = o.normalized * lim;
                }
            }
            for (int i = 1; i < crown; i++) Clamp(i, i - 1);
            for (int i = n - 2; i > crown; i--) Clamp(i, i + 1);

            // Trecho enterrado: mesmo contorno da base, alargando um pouco para baixo (sem degrau na linha do chão)
            int leftGround = legSteps, rightGround = n - 1 - legSteps;
            for (int i = 0; i < leftGround; i++)
            for (int j = 0; j < stride; j++)
                offsets[i * stride + j] = offsets[leftGround * stride + j] * (1f + 0.05f * (leftGround - i));
            for (int i = rightGround + 1; i < n; i++)
            for (int j = 0; j < stride; j++)
                offsets[i * stride + j] = offsets[rightGround * stride + j] * (1f + 0.05f * (i - rightGround));

            for (int i = 0; i < n; i++)
            for (int j = 0; j < stride; j++)
            {
                Vector3 vtx = path[i] + offsets[i * stride + j];
                verts.Add(vtx);
                // UV em espaço do objeto: estratos (v) ficam horizontais
                uvs.Add(new Vector2((vtx.x + vtx.z) / 12f, vtx.y / 10f));
            }
            for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < seg; j++)
            {
                int a0 = i * stride + j, a1 = a0 + 1, b0 = a0 + stride, b1 = b0 + 1;
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(a1); tris.Add(b1); tris.Add(b0);
            }
            var mesh = new Mesh { name = a.name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // garante normais para fora (o sentido da winding depende da orientação do frame)
            var normals = mesh.normals;
            int outward = 0;
            for (int i = 0; i < verts.Count; i += 7)
            {
                int ring = i / stride;
                if (Vector3.Dot(normals[i], verts[i] - path[ring]) > 0f) outward++; else outward--;
            }
            if (outward < 0)
            {
                for (int t = 0; t < tris.Count; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Colisores: caixas ao longo do caminho (só a parte acima do chão)
            boxes = new List<BoxSpec>();
            int boxCount = 9;
            int first = legSteps - 1, last = n - legSteps;
            for (int b = 0; b < boxCount; b++)
            {
                int i0 = Mathf.RoundToInt(Mathf.Lerp(first, last, b / (float)boxCount));
                int i1 = Mathf.RoundToInt(Mathf.Lerp(first, last, (b + 1) / (float)boxCount));
                Vector3 p0 = path[i0], p1 = path[i1];
                Vector3 dir = p1 - p0;
                int mid = (i0 + i1) / 2;
                boxes.Add(new BoxSpec
                {
                    center = (p0 + p1) * 0.5f,
                    rotation = Quaternion.LookRotation(Vector3.forward, dir.normalized),
                    size = new Vector3(thick[mid] * 0.85f, dir.magnitude + 1f, deep[mid] * 0.85f)
                });
            }
            return mesh;
        }

        static float Noise3(Vector3 p) =>
            (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z) + Mathf.PerlinNoise(p.z + 57.3f, p.x)) / 3f;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
