using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Atmosfera da caatinga na seca: sol forte e alto com sombra dura, céu claro, névoa leve e quente
    /// e Global Volume (Color Adjustments, Bloom, Vignette, Tonemapping). Ajuste depois direto na luz,
    /// no material do céu, na janela Lighting e no Volume Profile.
    /// </summary>
    public static class CaatingaAtmosphereSetup
    {
        public const string SkyMaterialPath = "Assets/_Project/Environment/Materials/M_Ceu_Caatinga.mat";
        public const string ProfilePath = "Assets/_Project/Settings/VP_Caatinga.asset";

        [MenuItem("Tools/Backcountry Bullet/Aplicar atmosfera na cena aberta")]
        public static void Apply()
        {
            // ---------------- sol
            var sun = FindSun();
            if (sun == null)
            {
                sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            Undo.RecordObject(sun, "Sol");
            Undo.RecordObject(sun.transform, "Sol");
            sun.name = "Sol";
            sun.transform.rotation = Quaternion.Euler(58f, 125f, 0f);   // sol forte e alto
            sun.color = new Color(1f, 0.97f, 0.92f);                     // luz clara, quase neutra
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;                                  // sombras definidas, mas não pretas
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;

            // ---------------- céu azul com cumulus (textura panorâmica gerada por código)
            EnvironmentPrefabFactory.EnsureFolder("Assets/_Project/Environment/Materials");
            var skyTex = BuildCloudSky(SkyTexturePath, 2048, 1024, 31);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "M_Ceu_Caatinga" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            sky.shader = Shader.Find("Skybox/Panoramic");
            sky.SetTexture("_MainTex", skyTex);
            sky.SetFloat("_Mapping", 1f);       // latitude-longitude
            sky.SetFloat("_ImageType", 0f);     // 360
            sky.SetFloat("_Exposure", 1.0f);
            sky.SetFloat("_Rotation", 0f);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;

            // Ambiente em três tons (não depende de bake): céu azul, horizonte claro, chão avermelhado
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.75f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.66f, 0.63f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.38f, 0.32f);
            RenderSettings.reflectionIntensity = 0.5f;

            // ---------------- névoa leve e azulada à distância (perspectiva atmosférica nas serras)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.86f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 1400f;

            // ---------------- Global Volume
            EnvironmentPrefabFactory.EnsureFolder("Assets/_Project/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var ca = Get<ColorAdjustments>(profile);
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(8f);
            ca.saturation.Override(-8f);                          // só um toque de dessaturação
            ca.colorFilter.Override(new Color(0.99f, 1f, 1.01f));  // praticamente neutro

            var wb = Get<WhiteBalance>(profile);
            wb.temperature.Override(-4f);                         // um pouco menos quente
            wb.tint.Override(0f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.6f);

            var vig = Get<Vignette>(profile);
            vig.intensity.Override(0.14f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.1f, 0.1f, 0.1f));
            EditorUtility.SetDirty(profile);

            var volumeGo = GameObject.Find("Global Volume");
            if (volumeGo == null)
            {
                volumeGo = new GameObject("Global Volume");
                Undo.RegisterCreatedObjectUndo(volumeGo, "Global Volume");
            }
            var volume = volumeGo.GetComponent<Volume>();
            if (volume == null) volume = volumeGo.AddComponent<Volume>(); // GetComponent devolve "null falso" no editor: nada de ??
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            // ---------------- câmera: pós-processamento ligado
            var cam = Camera.main;
            if (cam != null)
            {
                var acd = cam.GetComponent<UniversalAdditionalCameraData>();
                if (acd == null) acd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                Undo.RecordObject(acd, "Câmera");
                acd.renderPostProcessing = true;
                acd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);
            }

            DynamicGI.UpdateEnvironment();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
            Debug.Log("[Atmosfera] Sol, céu, névoa e Global Volume aplicados.");
        }

        public const string SkyTexturePath = "Assets/_Project/Environment/Textures/T_Ceu_Cumulus_PLACEHOLDER.png";

        /// <summary>
        /// Céu panorâmico (lat-long) gerado por código: azul que clareia no horizonte, cumulus brancos de base plana
        /// e sombreado cinza-azulado embaixo, mais densos perto do horizonte. Abaixo do horizonte, tom de terra.
        /// </summary>
        public static Texture2D BuildCloudSky(string path, int w, int h, int seed)
        {
            var rng = new System.Random(seed);
            Vector2 off = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
            var px = new Color[w * h];
            Color zenith = new Color(0.27f, 0.47f, 0.78f), horizon = new Color(0.72f, 0.81f, 0.91f), ground = new Color(0.55f, 0.5f, 0.44f);
            for (int y = 0; y < h; y++)
            {
                float lat = (y / (float)(h - 1)) * Mathf.PI - Mathf.PI * 0.5f;   // -90..90
                float el = Mathf.Sin(lat);                                       // altura no céu
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (el < 0f) c = Color.Lerp(horizon, ground, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-el * 6f)));
                    else
                    {
                        c = Color.Lerp(horizon, zenith, Mathf.Pow(Mathf.Clamp01(el), 0.55f));
                        // nuvens: projeta a direção num plano de nuvens (perspectiva: mais densas e achatadas no horizonte)
                        float lon = x / (float)w * Mathf.PI * 2f;
                        float d = 1f / Mathf.Max(el, 0.03f);
                        Vector2 q = new Vector2(Mathf.Cos(lon), Mathf.Sin(lon)) * d * 0.9f + off;
                        float n = 0f, amp = 0.5f, norm = 0f; Vector2 qq = q;
                        for (int o = 0; o < 5; o++) { n += Mathf.PerlinNoise(qq.x, qq.y) * amp; norm += amp; qq *= 2.1f; amp *= 0.5f; }
                        n /= norm;
                        float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.66f, n));
                        // base plana sombreada: amostra deslocada "para baixo" no plano dá o lado de sombra
                        float nb = Mathf.PerlinNoise(q.x + 0.15f, q.y + 0.15f);
                        float shade = Mathf.Lerp(1f, 0.72f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.7f, nb)));
                        Color cloud = Color.Lerp(new Color(0.78f, 0.81f, 0.87f), Color.white, Mathf.InverseLerp(0.52f, 0.8f, n)) * shade;
                        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.08f, el)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.9f, el)) * 0.6f);
                        c = Color.Lerp(c, cloud, cover * fade);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Distância de sombra do URP asset ativo (asset do projeto: só com autorização).</summary>
        public static void SetShadowDistance(float meters)
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp)
            {
                Undo.RecordObject(urp, "Distância de sombra");
                urp.shadowDistance = meters;
                EditorUtility.SetDirty(urp);
                AssetDatabase.SaveAssets();
            }
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet<T>(out var c))
            {
                c = p.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            c.active = true;
            return c;
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
