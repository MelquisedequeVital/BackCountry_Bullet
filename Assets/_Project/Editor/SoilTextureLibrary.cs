using System;
using UnityEditor;
using UnityEngine;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Texturas de solo da caatinga geradas por código (placeholders, sufixo _PLACEHOLDER) e os TerrainLayers.
    /// Cada função devolve cor e altura (0..1) para o mesmo (u, v); a altura vira normal map.
    /// </summary>
    public static class SoilTextureLibrary
    {
        public const string LayerFolder = "Assets/_Project/Environment/Terrain";

        /// <summary>Ordem = índice no splatmap. Paleta da caatinga: solo vermelho-alaranjado, capim seco, bege pedregoso, granito claro.</summary>
        public enum Soil { TerraVermelha, CapimSeco, Cascalho, AreiaLeito, RochaEncosta, TerraBatida, MataChao }

        public struct SoilDef
        {
            public Soil soil;
            public string texName, layerName;
            public float tileSize, normalStrength;
            public Func<float, float, (Color c, float h)> sample;
        }

        public static readonly SoilDef[] Defs =
        {
            new SoilDef { soil = Soil.TerraVermelha, texName = "T_Solo_TerraVermelha_PLACEHOLDER", layerName = "TL_TerraVermelha", tileSize = 6f, normalStrength = 2f,  sample = TerraVermelha },
            new SoilDef { soil = Soil.CapimSeco,    texName = "T_Solo_CapimSeco_PLACEHOLDER",    layerName = "TL_CapimSeco",    tileSize = 3f, normalStrength = 3f,  sample = CapimSeco },
            new SoilDef { soil = Soil.Cascalho,     texName = "T_Solo_Cascalho_PLACEHOLDER",     layerName = "TL_Cascalho",     tileSize = 3f, normalStrength = 3f,  sample = Cascalho },
            new SoilDef { soil = Soil.AreiaLeito,   texName = "T_Solo_AreiaLeito_PLACEHOLDER",   layerName = "TL_AreiaLeito",   tileSize = 4f, normalStrength = 2f,  sample = AreiaLeito },
            new SoilDef { soil = Soil.RochaEncosta, texName = "T_Solo_RochaEncosta_PLACEHOLDER", layerName = "TL_RochaEncosta", tileSize = 9f, normalStrength = 5f,  sample = RochaEncosta },
            new SoilDef { soil = Soil.TerraBatida,  texName = "T_Solo_TerraBatida_PLACEHOLDER",  layerName = "TL_TerraBatida",  tileSize = 5f, normalStrength = 1.5f, sample = TerraBatida },
            new SoilDef { soil = Soil.MataChao,     texName = "T_Solo_MataChao_PLACEHOLDER",     layerName = "TL_MataChao",     tileSize = 7f, normalStrength = 4f,  sample = MataChao },
        };

        /// <summary>Assets de solo da versão anterior (paleta de deserto), apagados ao recriar as camadas.</summary>
        public static readonly string[] ObsoleteAssets =
        {
            LayerFolder + "/TL_TerraOcre.terrainlayer", LayerFolder + "/TL_ChaoRachado.terrainlayer",
            ProceduralTextures.Folder + "/T_Solo_TerraOcre_PLACEHOLDER.png", ProceduralTextures.Folder + "/T_Solo_TerraOcre_PLACEHOLDER_N.png",
            ProceduralTextures.Folder + "/T_Solo_ChaoRachado_PLACEHOLDER.png", ProceduralTextures.Folder + "/T_Solo_ChaoRachado_PLACEHOLDER_N.png",
        };

        /// <summary>Gera texturas (se faltarem ou <paramref name="rebuild"/>) e devolve os TerrainLayers na ordem de <see cref="Soil"/>.</summary>
        public static TerrainLayer[] EnsureLayers(bool rebuild, int size = 512)
        {
            var layers = new TerrainLayer[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                var d = Defs[i];
                string albedoPath = $"{ProceduralTextures.Folder}/{d.texName}.png";
                string normalPath = $"{ProceduralTextures.Folder}/{d.texName}_N.png";
                if (rebuild || AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) == null)
                    BuildTextures(d, size);

                string layerPath = $"{LayerFolder}/{d.layerName}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                bool created = layer == null;
                if (created) layer = new TerrainLayer();
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                layer.normalScale = 1f;
                layer.tileSize = new Vector2(d.tileSize, d.tileSize);
                layer.metallic = 0f;
                layer.smoothness = 0f;
                if (created) AssetDatabase.CreateAsset(layer, layerPath);
                else EditorUtility.SetDirty(layer);
                layers[i] = layer;
            }
            AssetDatabase.SaveAssets();
            if (rebuild)
                foreach (var path in ObsoleteAssets)
                    if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            return layers;
        }

        static void BuildTextures(SoilDef d, int size)
        {
            var heights = new float[size * size];
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var (c, h) = d.sample(x / (float)size, y / (float)size);
                c.a = 1f; // alfa = suavidade no TerrainLit: solo fosco
                colors[y * size + x] = c;
                heights[y * size + x] = h;
            }
            ProceduralTextures.CreateTileable(d.texName, size, (u, v) => colors[Idx(u, v, size)]);

            // Normal map por diferenças centrais com wrap (continua repetível)
            var normals = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = heights[y * size + (x - 1 + size) % size], r = heights[y * size + (x + 1) % size];
                float b = heights[((y - 1 + size) % size) * size + x], t = heights[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * d.normalStrength, (b - t) * d.normalStrength, 1f).normalized;
                normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            var normalTex = ProceduralTextures.CreateTileable(d.texName + "_N", size, (u, v) => normals[Idx(u, v, size)], linear: true);
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normalTex));
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }

        static int Idx(float u, float v, int size) =>
            Mathf.Clamp(Mathf.RoundToInt(v * size), 0, size - 1) * size + Mathf.Clamp(Mathf.RoundToInt(u * size), 0, size - 1);

        // ------------------------------------------------------------------ ruídos

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Voronoi repetível numa grade n×n. f1/f2 em unidades de célula; id aleatório da célula mais próxima.</summary>
        static (float f1, float f2, float id) Voronoi(float u, float v, int n, int seed, float jitter = 0.85f)
        {
            float x = u * n, y = v * n;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f, id = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = ((gx % n) + n) % n, wy = ((gy % n) + n) % n;
                float px = gx + 0.5f + (Hash(wx, wy, seed) - 0.5f) * jitter;
                float py = gy + 0.5f + (Hash(wx, wy, seed + 101) - 0.5f) * jitter;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; id = Hash(wx, wy, seed + 7); }
                else if (d < f2) f2 = d;
            }
            return (f1, f2, id);
        }

        static float Fbm(float u, float v, int period, int oct, float seed) => ProceduralTextures.TileFbm(u, v, period, oct, seed);

        // ------------------------------------------------------------------ solos

        static Color Rgb(float r, float g, float b) => new Color(r, g, b);

        /// <summary>Solo vermelho-alaranjado/salmão da caatinga, com pedrinhas bege e manchas mais escuras de matéria orgânica.</summary>
        static (Color, float) TerraVermelha(float u, float v)
        {
            float n = Fbm(u, v, 4, 4, 1f);
            float fine = Fbm(u, v, 32, 2, 3f);
            float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.7f, Fbm(u, v, 6, 3, 9f)));
            Color c = Color.Lerp(Rgb(0.60f, 0.34f, 0.23f), Rgb(0.75f, 0.47f, 0.33f), n);
            c = Color.Lerp(c, Rgb(0.47f, 0.30f, 0.22f), dark * 0.45f);
            c *= Mathf.Lerp(0.9f, 1.07f, fine);
            float h = n * 0.35f + fine * 0.15f;
            var (f1, _, id) = Voronoi(u, v, 22, 11);
            float r = 0.06f + id * 0.06f;
            if (f1 < r && id > 0.62f)
            {
                float dome = Mathf.Sqrt(1f - (f1 / r) * (f1 / r));
                c = Color.Lerp(c, Rgb(0.78f, 0.69f, 0.58f) * Mathf.Lerp(0.8f, 1.08f, id), 0.85f) * Mathf.Lerp(0.85f, 1.05f, dome);
                h += dome * 0.5f;
            }
            return (c, h);
        }

        /// <summary>Solo coberto de capim seco amarelado e gravetos: lâminas de palha em várias direções sobre terra avermelhada.</summary>
        static (Color, float) CapimSeco(float u, float v)
        {
            float n = Fbm(u, v, 5, 3, 201f);
            Color c = Color.Lerp(Rgb(0.52f, 0.33f, 0.23f), Rgb(0.64f, 0.43f, 0.30f), n);
            float h = n * 0.15f;
            for (int layer = 0; layer < 3; layer++)
            {
                var (f1, _, id) = Voronoi(u, v, 9 + layer * 5, 300 + layer * 17, 1f);
                float ang = id * Mathf.PI;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float freq = 150f + layer * 40f;
                float across = (u * ca + v * sa) * freq + Fbm(u, v, 12, 2, 50f + layer) * 6f;
                float along = (-u * sa + v * ca) * freq * 0.08f;
                float blade = Mathf.Pow(Mathf.Abs(Mathf.Sin(across * Mathf.PI)), 6f);
                float breakup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.6f,
                    Mathf.PerlinNoise(along + id * 40f, across * 0.5f + layer * 9f)));
                float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.55f, Fbm(u, v, 6, 2, 70f + layer * 13f)));
                float m = blade * breakup * cover;
                if (m <= 0.01f) continue;
                float tone = Hash((int)(id * 1000f), layer, 5);
                Color straw = tone < 0.7f
                    ? Color.Lerp(Rgb(0.70f, 0.58f, 0.35f), Rgb(0.86f, 0.76f, 0.50f), tone / 0.7f)
                    : Color.Lerp(Rgb(0.58f, 0.55f, 0.49f), Rgb(0.70f, 0.67f, 0.60f), (tone - 0.7f) / 0.3f); // gravetos cinza
                c = Color.Lerp(c, straw * Mathf.Lerp(0.85f, 1.05f, f1), m);
                h = Mathf.Max(h, (0.3f + layer * 0.2f) * m);
            }
            return (c, h);
        }

        /// <summary>Chão bege pedregoso: seixos de granito claro sobre terra salmão.</summary>
        static (Color, float) Cascalho(float u, float v)
        {
            float wob = (Fbm(u, v, 24, 2, 5f) - 0.5f) * 0.35f;
            var (f1, _, id) = Voronoi(u, v, 18, 3, 0.9f);
            f1 += wob;
            float r = 0.2f + id * 0.18f;
            float soil = Fbm(u, v, 16, 2, 7f);
            Color c = Color.Lerp(Rgb(0.64f, 0.46f, 0.34f), Rgb(0.74f, 0.57f, 0.44f), soil);
            float h = soil * 0.15f;
            if (f1 < r)
            {
                float dome = Mathf.Pow(1f - (f1 / r) * (f1 / r), 0.25f);
                Color stone = Color.Lerp(Rgb(0.70f, 0.62f, 0.55f), Rgb(0.86f, 0.79f, 0.70f), id);
                stone *= Mathf.Lerp(0.78f, 1.06f, dome) * Mathf.Lerp(0.93f, 1.04f, Fbm(u, v, 64, 1, id * 50f));
                c = stone;
                h = 0.25f + dome * 0.35f;
            }
            var (s1, _, sid) = Voronoi(u, v, 45, 17);
            if (f1 >= r && s1 < 0.22f)
            {
                c = Color.Lerp(c, Rgb(0.80f, 0.72f, 0.63f) * Mathf.Lerp(0.82f, 1.08f, sid), 0.8f);
                h += 0.25f;
            }
            return (c, h);
        }

        static (Color, float) AreiaLeito(float u, float v)
        {
            float n = Fbm(u, v, 5, 3, 31f);
            float grain = Fbm(u, v, 64, 2, 41f);
            float ripple = Mathf.Sin((u * 2f + v + (n - 0.5f) * 0.4f) * Mathf.PI * 2f * 9f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Rgb(0.74f, 0.63f, 0.51f), Rgb(0.83f, 0.73f, 0.61f), n);
            c *= Mathf.Lerp(0.93f, 1.05f, grain) * Mathf.Lerp(0.96f, 1.03f, ripple);
            return (c, ripple * 0.35f + grain * 0.2f + n * 0.2f);
        }

        /// <summary>Granito claro, entre bege e rosado, com pontos de biotita e manchas de escorrimento/líquen.</summary>
        static (Color, float) RochaEncosta(float u, float v)
        {
            float big = Fbm(u, v, 3, 4, 51f);
            float ridged = 1f - Mathf.Abs(Fbm(u, v, 6, 3, 61f) * 2f - 1f);
            float speck = Fbm(u, v, 128, 1, 71f);
            float stain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.75f, Fbm(u, v, 4, 2, 81f)));
            Color c = Color.Lerp(Rgb(0.66f, 0.59f, 0.54f), Rgb(0.84f, 0.77f, 0.70f), big);
            c = Color.Lerp(c, Rgb(0.80f, 0.66f, 0.58f), Fbm(u, v, 5, 2, 91f) * 0.4f);
            c = Color.Lerp(c, Rgb(0.46f, 0.44f, 0.41f), stain * 0.4f);
            var (b1, _, bid) = Voronoi(u, v, 90, 77);
            if (b1 < 0.18f && bid > 0.55f) c *= 0.6f;
            c *= Mathf.Lerp(0.88f, 1.06f, speck);
            c *= Mathf.Lerp(0.84f, 1f, ridged);
            return (c, big * 0.4f + ridged * 0.5f + speck * 0.1f);
        }

        /// <summary>
        /// Chão de mata: mosaico de tufos verde-oliva opaco (copa baixa e touceiras vistas de cima/longe),
        /// gravetos cinza e folhiço com falhas de solo avermelhado.
        /// </summary>
        static (Color, float) MataChao(float u, float v)
        {
            float n = Fbm(u, v, 4, 3, 401f);
            float fine = Fbm(u, v, 40, 2, 403f);
            Color soil = Color.Lerp(Rgb(0.45f, 0.30f, 0.22f), Rgb(0.55f, 0.37f, 0.27f), n);
            Color c = Color.Lerp(soil, Rgb(0.40f, 0.33f, 0.24f), 0.5f);        // folhiço
            float h = n * 0.1f;
            // tufos: células de Voronoi com domo, tons de oliva variados
            var (f1, f2, id) = Voronoi(u, v, 14, 409, 0.95f);
            float r = 0.55f + id * 0.25f;
            float tuft = Mathf.SmoothStep(0f, 1f, 1f - f1 / r);
            float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.4f, Fbm(u, v, 5, 2, 411f) + id * 0.25f));
            if (tuft > 0f && cover > 0f)
            {
                Color olive = id < 0.75f
                    ? Color.Lerp(Rgb(0.28f, 0.30f, 0.17f), Rgb(0.44f, 0.45f, 0.26f), id / 0.75f)
                    : Color.Lerp(Rgb(0.50f, 0.48f, 0.42f), Rgb(0.62f, 0.60f, 0.55f), (id - 0.75f) / 0.25f); // copa seca acinzentada
                olive *= Mathf.Lerp(0.75f, 1.08f, tuft) * Mathf.Lerp(0.85f, 1.1f, fine);
                c = Color.Lerp(c, olive, Mathf.Clamp01(tuft * 1.6f) * cover);
                h = Mathf.Max(h, tuft * 0.7f * cover + fine * 0.1f);
            }
            // gravetos cinza
            float twig = Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 0.8f + v * 0.6f) * 260f + Fbm(u, v, 20, 2, 417f) * 30f)), 40f)
                         * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.7f, Fbm(u, v, 24, 2, 419f)));
            c = Color.Lerp(c, Rgb(0.62f, 0.60f, 0.55f), twig * 0.8f);
            return (c, h + twig * 0.2f);
        }

        /// <summary>Terra batida da estrada e da vila: vermelho-alaranjada clara e compactada.</summary>
        static (Color, float) TerraBatida(float u, float v)
        {
            float n = Fbm(u, v, 3, 3, 91f);
            float fine = Fbm(u, v, 48, 2, 97f);
            Color c = Color.Lerp(Rgb(0.69f, 0.48f, 0.36f), Rgb(0.79f, 0.59f, 0.46f), n);
            c *= Mathf.Lerp(0.95f, 1.04f, fine);
            float h = n * 0.15f + fine * 0.1f;
            var (f1, _, id) = Voronoi(u, v, 30, 23);
            if (f1 < 0.1f && id > 0.6f) { c = Color.Lerp(c, Rgb(0.78f, 0.70f, 0.61f), 0.7f); h += 0.2f; }
            return (c, h);
        }
    }
}
