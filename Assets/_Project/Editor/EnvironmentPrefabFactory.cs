using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Cria prefabs próprios a partir de peças de modelos de terceiros, sem editar os originais:
    /// a malha é referenciada, os materiais são copiados para _Project/Environment/Materials.
    /// Pivô na base, centrado; escala base aplicada num filho "Mesh".
    /// </summary>
    public static class EnvironmentPrefabFactory
    {
        public const string PrefabFolder = "Assets/_Project/Environment/Prefabs";
        public const string MaterialFolder = "Assets/_Project/Environment/Materials";

        public enum ColliderKind { Nenhum, Box, Capsule, Tronco }

        public class MaterialTweak
        {
            public Color tint = Color.white;
            /// <summary>Liga alpha clip (e duas faces) só nos materiais cuja textura tem alfa (folhas em cartão).</summary>
            public bool alphaClip;
            public float alphaCutoff = 0.4f;
            public string suffix = "";
            /// <summary>Textura para materiais sem textura (ex.: atlas de cor do pack).</summary>
            public Texture2D fallbackTexture;
            /// <summary>Troca material de origem por outro antes de copiar (ex.: flor rosa -> verde).</summary>
            public System.Func<Material, Material> remap;
            /// <summary>Tint por material de origem (ex.: folha oliva e casca cinza na mesma peça). null = usa <see cref="tint"/>.</summary>
            public System.Func<Material, Color?> tintBy;
        }

        public class Options
        {
            public ColliderKind collider = ColliderKind.Nenhum;
            public float colliderShrink = 0.85f;
            public float trunkRadius = 0.25f, trunkHeight = 3f;
            /// <summary>Se > 0, adiciona LODGroup que some abaixo desta altura relativa de tela.</summary>
            public float cullScreenHeight = 0f;
            /// <summary>Objetos pequenos sem sombra economizam draw calls de sombra.</summary>
            public bool castShadows = true;
        }

        /// <summary>Parte de um prefab composto: peça de modelo com pivô na base, posta com TRS local.</summary>
        public struct Part
        {
            public string model, child;
            public Vector3 position, euler, scale;
            public MaterialTweak tweak;
        }

        /// <param name="childName">Nome do objeto dentro do FBX (null = o modelo inteiro).</param>
        public static GameObject Create(string modelPath, string childName, string prefabName, Vector3 baseScale,
            ColliderKind collider, MaterialTweak tweak, float colliderShrink = 0.85f)
            => Create(modelPath, childName, prefabName, baseScale, tweak, new Options { collider = collider, colliderShrink = colliderShrink });

        public static GameObject Create(string modelPath, string childName, string prefabName, Vector3 baseScale, MaterialTweak tweak, Options opt)
        {
            var root = new GameObject(prefabName);
            var meshRoot = new GameObject("Mesh");
            meshRoot.transform.SetParent(root.transform, false);
            if (!AddModelParts(meshRoot.transform, modelPath, childName, tweak)) { Object.DestroyImmediate(root); return null; }
            meshRoot.transform.localScale = baseScale;
            return Finish(root, meshRoot.transform, prefabName, opt);
        }

        public static GameObject CreateComposite(string prefabName, IList<Part> parts, Options opt)
        {
            var root = new GameObject(prefabName);
            var meshRoot = new GameObject("Mesh");
            meshRoot.transform.SetParent(root.transform, false);
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var holder = new GameObject($"Parte_{i:00}");
                holder.transform.SetParent(meshRoot.transform, false);
                var inner = new GameObject("Pivo");
                inner.transform.SetParent(holder.transform, false);
                if (!AddModelParts(inner.transform, p.model, p.child, p.tweak)) { Object.DestroyImmediate(root); return null; }
                // pivô da parte na base centrada
                var b = BoundsRelativeTo(holder.transform, inner.transform);
                inner.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                holder.transform.localPosition = p.position;
                holder.transform.localRotation = Quaternion.Euler(p.euler);
                holder.transform.localScale = p.scale;
            }
            return Finish(root, meshRoot.transform, prefabName, opt);
        }

        static bool AddModelParts(Transform parent, string modelPath, string childName, MaterialTweak tweak)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Debug.LogError($"[Prefabs] Modelo não encontrado: {modelPath}"); return false; }
            Transform src = model.transform;
            if (!string.IsNullOrEmpty(childName))
            {
                src = FindDeep(model.transform, childName);
                if (src == null) { Debug.LogError($"[Prefabs] '{childName}' não existe em {modelPath}"); return false; }
            }
            // Copia as peças com renderer de src (e filhos), com a pose do modelo importado
            // Mantém a rotação de importação (eixo Blender -> Unity); a posição é recentrada depois pelo pivô
            var srcInv = Matrix4x4.identity;
            foreach (var mr in src.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(mr.name);
                part.transform.SetParent(parent, false);
                var m = srcInv * mr.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var newMr = part.AddComponent<MeshRenderer>();
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = CopyMaterial(tweak.remap != null ? tweak.remap(mats[i]) : mats[i], tweak);
                newMr.sharedMaterials = mats;
            }
            return true;
        }

        static GameObject Finish(GameObject root, Transform meshRoot, string prefabName, Options opt)
        {
            // Pivô: base centrada
            var b = LocalBounds(root);
            meshRoot.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            b = LocalBounds(root);

            switch (opt.collider)
            {
                case ColliderKind.Box:
                {
                    var bc = root.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = new Vector3(b.size.x * opt.colliderShrink, b.size.y, b.size.z * opt.colliderShrink);
                    break;
                }
                case ColliderKind.Capsule:
                {
                    var cc = root.AddComponent<CapsuleCollider>();
                    cc.direction = 1;
                    cc.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    cc.height = b.size.y;
                    cc.radius = Mathf.Min(b.extents.x, b.extents.z) * opt.colliderShrink;
                    break;
                }
                case ColliderKind.Tronco:
                {
                    var cc = root.AddComponent<CapsuleCollider>();
                    cc.direction = 1;
                    float h = Mathf.Min(opt.trunkHeight, b.size.y);
                    cc.center = new Vector3(0f, h * 0.5f, 0f);
                    cc.height = h;
                    cc.radius = opt.trunkRadius;
                    break;
                }
            }

            if (!opt.castShadows)
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (opt.cullScreenHeight > 0f)
            {
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(opt.cullScreenHeight, root.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds();
            }

            foreach (var t in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);

            EnsureFolder(PrefabFolder);
            var path = $"{PrefabFolder}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material CopyMaterial(Material src, MaterialTweak tweak)
        {
            if (src == null) return null;
            EnsureFolder(MaterialFolder);
            string name = $"M_{src.name.Replace('.', '_')}{tweak.suffix}";
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(src) { name = name };
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            var baseMap = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : (src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null);
            if (baseMap == null) baseMap = tweak.fallbackTexture;
            mat.SetTexture("_BaseMap", baseMap);
            Color baseColor = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            if (tweak.fallbackTexture != null && baseMap == tweak.fallbackTexture) baseColor = Color.white;
            mat.SetColor("_BaseColor", baseColor * (tweak.tintBy?.Invoke(src) ?? tweak.tint));
            mat.enableInstancing = true;
            // fosco: sem especular nem reflexo do céu
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            bool clip = tweak.alphaClip && TextureHasAlpha(baseMap);
            mat.SetFloat("_AlphaClip", clip ? 1f : 0f);
            if (clip)
            {
                mat.SetFloat("_Cutoff", tweak.alphaCutoff);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); // folhas em cartão: duas faces
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                mat.SetOverrideTag("RenderType", "TransparentCutout");
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 2f);
                mat.renderQueue = -1;
                mat.SetOverrideTag("RenderType", "");
            }
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static bool TextureHasAlpha(Texture tex)
        {
            if (tex == null) return false;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            return imp != null && imp.DoesSourceTextureHaveAlpha() && imp.alphaSource != TextureImporterAlphaSource.None;
        }

        public static Bounds LocalBounds(GameObject root) => BoundsRelativeTo(root.transform, root.transform);

        static Bounds BoundsRelativeTo(Transform space, Transform under)
        {
            var inv = space.worldToLocalMatrix;
            Bounds b = default; bool init = false;
            foreach (var mf in under.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = m.MultiplyPoint3x4(c);
                    if (!init) { b = new Bounds(w, Vector3.zero); init = true; } else b.Encapsulate(w);
                }
            }
            return b;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
