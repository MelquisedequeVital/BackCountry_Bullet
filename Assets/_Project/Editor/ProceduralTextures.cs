using System;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>Gera texturas repetíveis (seamless) como PNG no projeto. Usado para placeholders de solo e rocha.</summary>
    public static class ProceduralTextures
    {
        public const string Folder = "Assets/_Project/Environment/Textures";

        /// <summary>Cria (ou sobrescreve) um PNG repetível. <paramref name="pixel"/> recebe (u, v) em [0,1).</summary>
        public static Texture2D CreateTileable(string name, int size, Func<float, float, Color> pixel, bool linear = false)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(Folder).Replace('\\', '/'), System.IO.Path.GetFileName(Folder));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = pixel(x / (float)size, y / (float)size);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.sRGBTexture = !linear;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Perlin periódico: repete a cada <paramref name="period"/> unidades em u e v.</summary>
        public static float TileNoise(float u, float v, float period, float seed = 0f)
        {
            float x = u * period, y = v * period;
            float a = Mathf.PerlinNoise(x + seed, y + seed);
            float b = Mathf.PerlinNoise(x - period + seed, y + seed);
            float c = Mathf.PerlinNoise(x + seed, y - period + seed);
            float d = Mathf.PerlinNoise(x - period + seed, y - period + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float TileFbm(float u, float v, int basePeriod, int octaves, float seed = 0f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += TileNoise(u, v, period, seed + i * 17.3f) * amp;
                norm += amp; amp *= 0.5f; period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Cartão de tufo de capim seco cor de palha, com alfa (para detalhe do Terrain).</summary>
        public static Texture2D DryGrassCard(string name = "T_CapimSeco_PLACEHOLDER", int size = 256, int seed = 5)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(Folder).Replace('\\', '/'), System.IO.Path.GetFileName(Folder));
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.8f, 0.7f, 0.48f, 0f);
            var rng = new System.Random(seed);
            int blades = 46;
            for (int b = 0; b < blades; b++)
            {
                float x0 = size * (0.2f + 0.6f * (float)rng.NextDouble());
                float lean = ((float)rng.NextDouble() - 0.5f) * size * 0.55f;
                float h = size * (0.45f + 0.5f * (float)rng.NextDouble());
                float w0 = 2.2f + (float)rng.NextDouble() * 2.2f;
                float tone = (float)rng.NextDouble();
                Color c = Color.Lerp(new Color(0.72f, 0.6f, 0.38f), new Color(0.93f, 0.84f, 0.6f), tone);
                for (int y = 0; y < (int)h; y++)
                {
                    float t = y / h;
                    float cx = x0 + lean * t * t;
                    float w = w0 * (1f - t * 0.85f);
                    Color shade = c * Mathf.Lerp(0.7f, 1.05f, t);
                    for (int x = Mathf.Max(0, (int)(cx - w)); x <= Mathf.Min(size - 1, (int)(cx + w)); x++)
                    {
                        float a = Mathf.Clamp01(w - Mathf.Abs(x - cx) + 0.5f);
                        int i = y * size + x;
                        if (a > px[i].a) px[i] = new Color(shade.r, shade.g, shade.b, a);
                    }
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(px); tex.Apply();
            string path = $"{Folder}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Arenito com estratos horizontais (bandas ao longo de v), estilo Serra da Capivara.</summary>
        public static Color Sandstone(float u, float v)
        {
            float warp = (TileFbm(u, v, 3, 3, 5f) - 0.5f) * 0.12f;
            float band = Mathf.Sin((v + warp) * Mathf.PI * 2f * 9f) * 0.5f + 0.5f;
            float fine = Mathf.Sin((v + warp * 1.7f) * Mathf.PI * 2f * 31f) * 0.5f + 0.5f;
            float grain = TileFbm(u, v, 16, 3, 41f);
            float stain = TileFbm(u, v, 4, 2, 77f);
            Color dark = new Color(0.47f, 0.29f, 0.2f), mid = new Color(0.7f, 0.46f, 0.31f), light = new Color(0.86f, 0.66f, 0.48f);
            Color c = Color.Lerp(mid, light, band * 0.7f + fine * 0.15f);
            c = Color.Lerp(c, dark, Mathf.SmoothStep(0.55f, 0.8f, stain) * 0.55f);
            c *= Mathf.Lerp(0.85f, 1.1f, grain);
            c.a = 1f;
            return c;
        }
    }
}
