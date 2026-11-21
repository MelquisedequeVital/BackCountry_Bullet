using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Monta o Bar do Zé Ferrugem (modelo do Blender exportado em FBX):
    ///  1. configura as texturas (normal map, 2048),
    ///  2. cria os materiais URP Lit a partir de Bar_ZeFerrugem_Materiais.json (exportado junto do FBX),
    ///  3. remapeia os materiais do FBX para eles,
    ///  4. cria o prefab Bar_ZeFerrugem com colisores (paredes, chão, balcão, mesas, cadeiras) e luzes,
    ///  5. coloca o bar na cena Caatinga, no lugar do Spot_Bar (Seu Zé atrás do balcão, porta para o leste),
    ///     e move a zona da janela para dentro do bar, na janela virada para os robôs azuis.
    /// Roda sozinho uma vez (quando o prefab ainda não existe). Para refazer: menu BackCountry → Bar.
    /// </summary>
    [InitializeOnLoad]
    public static class BarZeFerrugemSetup
    {
        const string Pasta = "Assets/_Project/Environment/Bar";
        const string CaminhoFbx = Pasta + "/Bar_ZeFerrugem.fbx";
        const string CaminhoJson = Pasta + "/Bar_ZeFerrugem_Materiais.json";
        const string PastaTexturas = Pasta + "/Texturas";
        const string PastaMateriais = Pasta + "/Materiais";
        const string CaminhoPrefab = "Assets/_Project/Environment/Prefabs/Bar_ZeFerrugem.prefab";
        const string CaminhoCena = "Assets/_Project/Scenes/Caatinga.unity";
        const string NomeNaCena = "Bar_ZeFerrugem";

        // Distância (m) do centro do balcão até onde o Seu Zé fica (atrás dele, entre o balcão e a prateleira)
        const float AtrasDoBalcao = 1.3f;

        static BarZeFerrugemSetup()
        {
            EditorApplication.delayCall += () =>
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoFbx);
                if (fbx == null) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefab) == null)
                {
                    if (CriarTudo()) ColocarNaCaatinga();
                    return;
                }
                // FBX reexportado com materiais novos: refaz os materiais (o prefab e a cena se atualizam sozinhos)
                if (MateriaisSemRemapear(fbx).Count > 0) CriarTudo();
                AbrirJanelaUmaVez();
            };
        }

        static List<string> MateriaisSemRemapear(GameObject fbx) =>
            fbx.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(x => x != null && AssetDatabase.GetAssetPath(x) == CaminhoFbx)
                .Select(x => x.name).Distinct().ToList();

        [MenuItem("BackCountry/Bar/Abrir a janela virada para os robôs")]
        static void MenuAbrirJanela() => AbrirJanela(true);

        // Bares colocados antes desta opção existir: abre a janela uma vez (depois, quem quiser fecha à mão)
        static void AbrirJanelaUmaVez()
        {
            const string chave = "BackCountry.Bar.JanelaAberta.v1";
            if (EditorPrefs.GetBool(chave, false)) return;
            if (AbrirJanela(false)) EditorPrefs.SetBool(chave, true);
        }

        /// <summary>
        /// Desliga o vitral da janela lateral mais perto dos robôs azuis, deixando o vão aberto
        /// (a moldura continua, com colisor, então o jogador não passa por ela).
        /// </summary>
        static bool AbrirJanela(bool abrirCenaSePreciso)
        {
            Scene cena = SceneManager.GetSceneByPath(CaminhoCena);
            if (!cena.isLoaded)
            {
                if (!abrirCenaSePreciso || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Single);
            }
            var bar = Achar(cena, NomeNaCena);
            if (bar == null) { if (abrirCenaSePreciso) Debug.LogWarning("Bar: a Caatinga não tem o Bar_ZeFerrugem."); return false; }

            bool estavaSuja = cena.isDirty;
            string janela = JanelaDosRobos(cena, bar);
            if (janela == null) return false;

            var vitral = bar.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == janela.Replace("_Moldura", "_Vitral"));
            if (vitral == null || !vitral.gameObject.activeSelf) return true;

            Undo.RecordObject(vitral.gameObject, "Abrir janela do bar");
            vitral.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(cena);
            if (!estavaSuja) EditorSceneManager.SaveScene(cena);
            Debug.Log($"Bar: {vitral.name} desligado; a {janela} agora está aberta para a rua." +
                      (estavaSuja ? " Salve a cena com Ctrl+S." : " Cena salva."));
            return true;
        }

        /// <summary>A janela lateral (com vitral) mais perto do robô azul.</summary>
        static string JanelaDosRobos(Scene cena, GameObject bar)
        {
            var robo = Achar(cena, "InimigoTutorial1_A");
            Vector3 referencia = robo != null ? robo.transform.position : bar.transform.position + Vector3.back * 30f;
            return new[] { "Janela_03_Moldura", "Janela_04_Moldura", "Janela_05_Moldura", "Janela_06_Moldura" }
                .Select(n => (nome: n, b: Bounds(bar, n))).Where(x => x.b != null)
                .OrderBy(x => Vector3.Distance(x.b.Value.center, referencia)).Select(x => x.nome).FirstOrDefault();
        }

        [MenuItem("BackCountry/Bar/Recriar materiais e prefab do bar")]
        static void MenuRecriar() => CriarTudo();

        [MenuItem("BackCountry/Bar/Colocar o bar na Caatinga")]
        static void MenuColocar() => ColocarNaCaatinga();

        // =====================================================================
        // Materiais e prefab
        // =====================================================================

        class Mat
        {
            public string nome;
            public float[] cor;
            public float metalico, rugosidade, transparencia;
            public float[] emissao, tiling;
            public string tex_cor, tex_normal, tex_metal;
        }

        static bool CriarTudo()
        {
            if (!File.Exists(CaminhoJson)) { Debug.LogError($"Bar: não achei {CaminhoJson}"); return false; }
            var mats = LerJson(File.ReadAllText(CaminhoJson));

            ConfigurarTexturas(mats);
            if (!AssetDatabase.IsValidFolder(PastaMateriais)) AssetDatabase.CreateFolder(Pasta, "Materiais");

            var importer = (ModelImporter)AssetImporter.GetAtPath(CaminhoFbx);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var m in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.nome), CriarMaterial(m));
            importer.SaveAndReimport();

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoFbx);
            var semMaterial = MateriaisSemRemapear(fbx);
            if (semMaterial.Count > 0) Debug.LogWarning("Bar: materiais sem remapear: " + string.Join(", ", semMaterial));

            CriarPrefab(fbx, mats);
            AssetDatabase.SaveAssets();
            Debug.Log($"Bar: {mats.Count} materiais e o prefab {CaminhoPrefab} prontos.");
            return true;
        }

        static void ConfigurarTexturas(List<Mat> mats)
        {
            var normais = new HashSet<string>(mats.Where(m => m.tex_normal != null).Select(m => m.tex_normal));
            var lineares = new HashSet<string>(mats.Where(m => m.tex_metal != null).Select(m => m.tex_metal));
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PastaTexturas }))
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guid);
                string arquivo = Path.GetFileName(caminho);
                var ti = (TextureImporter)AssetImporter.GetAtPath(caminho);
                ti.maxTextureSize = 2048;
                ti.textureType = normais.Contains(arquivo) ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normais.Contains(arquivo) && !lineares.Contains(arquivo);
                ti.SaveAndReimport();
            }
        }

        static Texture2D Tex(string arquivo) =>
            string.IsNullOrEmpty(arquivo) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(PastaTexturas + "/" + arquivo);

        static Material CriarMaterial(Mat d)
        {
            string caminho = $"{PastaMateriais}/{d.nome}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, caminho); }
            else mat.shader = lit;
            mat.shaderKeywords = Array.Empty<string>();

            var baseTex = Tex(d.tex_cor);
            var cor = baseTex != null ? Color.white : new Color(d.cor[0], d.cor[1], d.cor[2]);
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetTextureScale("_BaseMap", new Vector2(d.tiling[0], d.tiling[1]));
            mat.SetFloat("_Metallic", d.metalico);
            mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - d.rugosidade));

            var metal = Tex(d.tex_metal);
            mat.SetTexture("_MetallicGlossMap", metal);
            if (metal != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");

            var normal = Tex(d.tex_normal);
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP");

            if (d.emissao != null && d.emissao.Length == 4)
            {
                // Força do Blender → intensidade HDR do URP (o bloom do Global Volume faz o brilho)
                float forca = Mathf.Min(d.emissao[3] * 0.4f, 6f);
                mat.SetColor("_EmissionColor", new Color(d.emissao[0], d.emissao[1], d.emissao[2]) * forca);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            bool transparente = d.transparencia > 0.01f;
            if (transparente)
            {
                // vidro bem transparente; líquido e vitral um pouco mais "cheios"
                float alfa = d.nome.Contains("Liquido") ? 0.75f
                           : d.nome.Contains("Vitral") ? 0.55f
                           : d.nome.Contains("Plastico") ? 0.2f
                           : 0.3f;
                cor.a = alfa;
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetShaderPassEnabled("DepthOnly", false);
                mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = -1;
                mat.SetShaderPassEnabled("DepthOnly", true);
                mat.SetShaderPassEnabled("ShadowCaster", true);
            }
            mat.SetColor("_BaseColor", cor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Peças que o jogador (e o NavMesh) precisam enxergar como sólidas
        static readonly string[] ComColisor =
        {
            "Sala_Paredes_Piso", "Teto", "Porta_Principal_Moldura", "Balcao_Corpo", "Balcao_Tampo",
            "Prateleira_", "Mesa_0", "Cadeira_", "Banqueta_",
        };
        static readonly string[] SemColisor = { "_Toalha", "_Perna", "_Apoio_Pes", "Neon_" };

        static void CriarPrefab(GameObject fbx, List<Mat> mats)
        {
            var raiz = new GameObject(NomeNaCena);
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            modelo.transform.SetParent(raiz.transform, false);

            int colisores = 0;
            foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                bool janela = n.StartsWith("Janela_") && n.EndsWith("_Moldura");
                if (!janela && (!ComColisor.Any(n.StartsWith) || SemColisor.Any(n.Contains))) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colisores++;
            }
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            // Luzes: uma em cada luminária e nos neons principais (sem sombra, para pesar pouco)
            var luzes = new GameObject("Luzes").transform;
            luzes.SetParent(raiz.transform, false);
            int nLuzes = 0;
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Luminaria_") && n.EndsWith("_Lampada"))
                    CriarLuz(luzes, n.Replace("_Lampada", "_Luz"), r.bounds.center, new Color(1f, 0.82f, 0.6f), 1.6f, 5f);
                else if (n == "Neon_Balcao")
                    CriarLuz(luzes, "Luz_Neon_Balcao", r.bounds.center, new Color(0.05f, 0.85f, 1f), 1f, 3f);
                else if (n == "Neon_Cacto")
                    CriarLuz(luzes, "Luz_Neon_Cacto", r.bounds.center, new Color(0.15f, 1f, 0.2f), 0.8f, 2.5f);
                else continue;
                nLuzes++;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoPrefab));
            PrefabUtility.SaveAsPrefabAsset(raiz, CaminhoPrefab);
            UnityEngine.Object.DestroyImmediate(raiz);
            Debug.Log($"Bar: prefab com {colisores} colisores e {nLuzes} luzes.");
        }

        static void CriarLuz(Transform pai, string nome, Vector3 pos, Color cor, float intensidade, float alcance)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cor;
            l.intensity = intensidade;
            l.range = alcance;
            l.shadows = LightShadows.None;
        }

        // =====================================================================
        // Cena
        // =====================================================================

        static void ColocarNaCaatinga()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefab);
            if (prefab == null) { Debug.LogError("Bar: o prefab ainda não existe (BackCountry → Bar → Recriar)."); return; }

            Scene cena = SceneManager.GetSceneByPath(CaminhoCena);
            if (!cena.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Single);
            }
            bool estavaSuja = cena.isDirty;

            if (Achar(cena, NomeNaCena) != null)
            {
                Debug.Log("Bar: a Caatinga já tem o Bar_ZeFerrugem; nada mudou.");
                return;
            }

            var seuZe = Achar(cena, "NPCTutorial1");
            var spot = Achar(cena, "Spot_Bar");
            Vector3 alvo = seuZe != null ? seuZe.transform.position
                         : spot != null ? spot.transform.position + new Vector3(-2f, 0f, 0f)
                         : new Vector3(373f, 20f, 385f);

            var bar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
            Undo.RegisterCreatedObjectUndo(bar, "Colocar bar");
            var grupo = Achar(cena, "Cena1_Bar");
            if (grupo != null) bar.transform.SetParent(grupo.transform, true);
            bar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bar.transform.localScale = Vector3.one;

            var balcao = Bounds(bar, "Balcao_Corpo");
            var porta = Bounds(bar, "Porta_Principal_Moldura");
            var cadeira = Bounds(bar, "Cadeira_Mesa01_1");
            var sala = Bounds(bar, "Sala_Paredes_Piso");
            if (balcao == null || porta == null || cadeira == null || sala == null)
            {
                Debug.LogError("Bar: não achei as peças do modelo (balcão/porta/cadeira/sala); o bar ficou na origem.");
                return;
            }

            float largura = Mathf.Max(sala.Value.size.x, sala.Value.size.z);
            if (largura < 5f || largura > 20f)
                Debug.LogWarning($"Bar: o bar ficou com {largura:0.0} m de largura (esperado ~10 m). Confira a escala do FBX.");

            // Gira para a porta ficar a leste (+X) do balcão, como o Jogador (x 381) e o Seu Zé (x 373) já estão
            Vector3 dir = porta.Value.center - balcao.Value.center; dir.y = 0f; dir.Normalize();
            Quaternion giro = Quaternion.FromToRotation(dir, Vector3.right);
            Vector3 pontoSeuZe = balcao.Value.center - dir * AtrasDoBalcao;
            pontoSeuZe.y = cadeira.Value.min.y; // altura do piso de dentro
            bar.transform.SetPositionAndRotation(alvo - giro * pontoSeuZe, giro);

            // Zona da janela: dentro do bar, na frente da janela lateral mais perto dos robôs azuis
            var zona = Achar(cena, "NPCTutorial1_ZonaJanela");
            string janela = JanelaDosRobos(cena, bar);
            if (janela != null)
            {
                var b = Bounds(bar, janela).Value;
                if (zona != null)
                {
                    Vector3 centro = Bounds(bar, "Sala_Paredes_Piso").Value.center;
                    Vector3 paraDentro = centro - b.center; paraDentro.y = 0f; paraDentro.Normalize();
                    Vector3 pos = b.center + paraDentro * 1.0f;
                    pos.y = alvo.y;
                    Undo.RecordObject(zona.transform, "Mover zona da janela");
                    zona.transform.SetPositionAndRotation(pos, giro);
                    Debug.Log($"Bar: zona da janela movida para a frente da {janela} ({pos}).");
                }
                var vitral = bar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == janela.Replace("_Moldura", "_Vitral"));
                if (vitral != null) vitral.gameObject.SetActive(false); // janela aberta para ver os robôs
                EditorPrefs.SetBool("BackCountry.Bar.JanelaAberta.v1", true);
            }

            EditorSceneManager.MarkSceneDirty(cena);
            if (!estavaSuja) EditorSceneManager.SaveScene(cena);
            Debug.Log($"Bar: colocado na Caatinga em {bar.transform.position} (giro {giro.eulerAngles.y:0}°)." +
                      (estavaSuja ? " A cena tinha outras mudanças não salvas: salve com Ctrl+S." : " Cena salva.") +
                      " Refaça o Bake do NavMesh.");
        }

        static GameObject Achar(Scene cena, string nome)
        {
            foreach (var raiz in cena.GetRootGameObjects())
            {
                if (raiz.name == nome) return raiz;
                var t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nome);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        static Bounds? Bounds(GameObject raiz, string nome)
        {
            var r = raiz.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.gameObject.name == nome);
            return r != null ? r.bounds : (Bounds?)null;
        }

        // =====================================================================
        // JSON (o JsonUtility não lê dicionário, então lê na mão o formato que o export do Blender gera)
        // =====================================================================

        static List<Mat> LerJson(string json)
        {
            var obj = (Dictionary<string, object>)MiniJson.Ler(json);
            var lista = new List<Mat>();
            foreach (var kv in (Dictionary<string, object>)obj["materiais"])
            {
                var d = (Dictionary<string, object>)kv.Value;
                lista.Add(new Mat
                {
                    nome = kv.Key,
                    cor = Floats(d, "cor") ?? new[] { 0.8f, 0.8f, 0.8f },
                    metalico = Float(d, "metalico"),
                    rugosidade = Float(d, "rugosidade", 0.5f),
                    transparencia = Float(d, "transparencia"),
                    emissao = Floats(d, "emissao"),
                    tiling = Floats(d, "tiling") ?? new[] { 1f, 1f },
                    tex_cor = d.TryGetValue("tex_cor", out var a) ? a as string : null,
                    tex_normal = d.TryGetValue("tex_normal", out var b) ? b as string : null,
                    tex_metal = d.TryGetValue("tex_metal", out var c) ? c as string : null,
                });
            }
            return lista;
        }

        static float Float(Dictionary<string, object> d, string k, float padrao = 0f) =>
            d.TryGetValue(k, out var v) && v is double x ? (float)x : padrao;

        static float[] Floats(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is List<object> l ? l.Select(x => (float)(double)x).ToArray() : null;

        static class MiniJson
        {
            public static object Ler(string s) { int i = 0; return Valor(s, ref i); }

            static void Pular(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Valor(string s, ref int i)
            {
                Pular(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>(); i++;
                    while (true)
                    {
                        Pular(s, ref i);
                        if (s[i] == '}') { i++; return d; }
                        string k = Texto(s, ref i); Pular(s, ref i); i++; // ':'
                        d[k] = Valor(s, ref i); Pular(s, ref i);
                        if (s[i] == ',') i++;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>(); i++;
                    while (true)
                    {
                        Pular(s, ref i);
                        if (s[i] == ']') { i++; return l; }
                        l.Add(Valor(s, ref i)); Pular(s, ref i);
                        if (s[i] == ',') i++;
                    }
                }
                if (c == '"') return Texto(s, ref i);
                if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
                if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
                if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
                int ini = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(ini, i - ini), System.Globalization.CultureInfo.InvariantCulture);
            }

            static string Texto(string s, ref int i)
            {
                var sb = new System.Text.StringBuilder(); i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        if (s[i] == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 5; continue; }
                        sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] });
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
        }
    }
}
