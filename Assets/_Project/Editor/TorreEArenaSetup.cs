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
    /// Monta o final da fase:
    ///  - Torre_NimbusCorp (o arranha-céu de 100 m no datacenter, que dá para ver de longe e guia o jogador),
    ///    com uma porta de luz na frente: perto dela aparece "[E] Entrar".
    ///  - Arena_Galpao (o galpão onde acontece a luta com o Super Mecha), longe do mapa; a porta leva o jogador
    ///    para o ponto "Chegada" numa ponta do galpão. O Boss fica no meio e o Coronel num camarote atrás dele.
    /// Roda sozinho uma vez (quando os prefabs ainda não existem). Para refazer: BackCountry → Torre e Arena.
    /// </summary>
    [InitializeOnLoad]
    public static class TorreEArenaSetup
    {
        const string PastaEnv = "Assets/_Project/Environment";
        const string PastaPrefabs = PastaEnv + "/Prefabs";
        const string CaminhoCena = "Assets/_Project/Scenes/Caatinga.unity";
        const string PrefabTorre = PastaPrefabs + "/Torre_NimbusCorp.prefab";
        const string PrefabArena = PastaPrefabs + "/Arena_Galpao.prefab";
        const string PrefabRetorno = "Assets/Prefabs/Fluxo/PontoDeRetorno.prefab";

        // A arena fica fora do terreno (que vai de 120 a ~880), ao sul do mapa
        static readonly Vector3 PosicaoDaArena = new Vector3(600f, 20f, -250f);
        // Distâncias dentro do galpão (m), medidas ao longo do comprimento a partir da ponta da chegada
        const float ChegadaDaPonta = 6f, BossDaChegada = 34f, CoronelAtrasDoBoss = 9f, AlturaDoCamarote = 4f;

        static TorreEArenaSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTorre) != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabArena) != null)
                {
                    FecharGalpao(); // galpões criados antes de existir o fechamento
                    return;
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PastaEnv + "/Torre/Torre_NimbusCorp.fbx") == null) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PastaEnv + "/Arena/Arena_Galpao.fbx") == null) return;
                if (CriarPrefabs()) ColocarNaCena();
            };
        }

        [MenuItem("BackCountry/Torre e Arena/Recriar materiais e prefabs")]
        static void MenuRecriar() => CriarPrefabs();

        [MenuItem("BackCountry/Torre e Arena/Colocar na Caatinga (torre, arena, boss e Coronel)")]
        static void MenuColocar() => ColocarNaCena();

        // =====================================================================
        // Prefabs
        // =====================================================================

        static bool CriarPrefabs()
        {
            var torre = ImportarModelo(PastaEnv + "/Torre", "Torre_NimbusCorp");
            var arena = ImportarModelo(PastaEnv + "/Arena", "Arena_Galpao");
            if (torre == null || arena == null) return false;
            Directory.CreateDirectory(PastaPrefabs);
            CriarPrefabTorre(torre);
            CriarPrefabArena(arena);
            AssetDatabase.SaveAssets();
            Debug.Log("Torre e Arena: materiais e prefabs prontos.");
            return true;
        }

        static GameObject ImportarModelo(string pasta, string nome)
        {
            string fbx = $"{pasta}/{nome}.fbx", json = $"{pasta}/{nome}_Materiais.json";
            if (!File.Exists(json)) { Debug.LogError($"Torre e Arena: falta {json}"); return null; }
            var mats = (Dictionary<string, object>)Json.Ler(File.ReadAllText(json));
            ConfigurarTexturas(pasta + "/Texturas");
            if (!AssetDatabase.IsValidFolder(pasta + "/Materiais")) AssetDatabase.CreateFolder(pasta, "Materiais");

            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.globalScale = 1f; imp.useFileScale = true;
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None; imp.importBlendShapes = false;
            imp.addCollider = true; // paredes, chão e fachada sólidos (e o NavMesh enxerga)
            imp.indexFormat = ModelImporterIndexFormat.Auto;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key),
                             CriarMaterial(pasta, kv.Key, (Dictionary<string, object>)kv.Value));
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        }

        static void ConfigurarTexturas(string pasta)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { pasta }))
            {
                string c = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(c);
                bool normal = c.Contains("_Normal"), linear = c.Contains("_MetalSuave");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !linear;
                ti.alphaSource = linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
        }

        static Material CriarMaterial(string pasta, string nome, Dictionary<string, object> d)
        {
            string caminho = $"{pasta}/Materiais/{nome}.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, caminho); } else mat.shader = lit;
            mat.shaderKeywords = System.Array.Empty<string>();
            Texture2D T(string k) => d.TryGetValue(k, out var v) && v is string s ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{pasta}/Texturas/{s}") : null;

            var cor = Color.white;
            if (d.TryGetValue("cor", out var c) && c is List<object> l) cor = new Color((float)(double)l[0], (float)(double)l[1], (float)(double)l[2]);
            var baseTex = T("tex_cor");
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", d.TryGetValue("suavidade", out var sv) ? (float)(double)sv : 0.5f);
            var ms = T("tex_metal");
            mat.SetTexture("_MetallicGlossMap", ms);
            if (ms != null) { mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1f); mat.SetFloat("_Metallic", 1f); }
            var nrm = T("tex_normal");
            mat.SetTexture("_BumpMap", nrm);
            if (nrm != null) mat.EnableKeyword("_NORMALMAP");

            if (d.TryGetValue("emissao_pela_cor", out var em) && baseTex != null)
            {
                // a fachada usa a própria textura como luz (as janelas e neons claros brilham à noite e de longe)
                mat.SetTexture("_EmissionMap", baseTex);
                mat.SetColor("_EmissionColor", Color.white * (float)(double)em * 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            bool transp = d.TryGetValue("transparente", out var tr) && tr is bool b && b;
            if (transp)
            {
                cor.a = 0.3f;
                mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetShaderPassEnabled("DepthOnly", false); mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One); mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetOverrideTag("RenderType", "Opaque"); mat.renderQueue = -1;
                mat.SetShaderPassEnabled("DepthOnly", true); mat.SetShaderPassEnabled("ShadowCaster", true);
            }
            mat.SetColor("_BaseColor", cor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MaterialDaPorta()
        {
            string caminho = PastaEnv + "/Torre/Materiais/TORRE_PortaDeLuz.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, caminho); }
            mat.SetColor("_BaseColor", new Color(0.1f, 0.8f, 1f));
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 1f) * 4f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void CriarPrefabTorre(GameObject fbx)
        {
            var raiz = new GameObject("Torre_NimbusCorp");
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            modelo.transform.SetParent(raiz.transform, false);
            Estatico(modelo);
            var b = Limites(modelo);

            // Entrada na frente (lado -Z do modelo): porta de luz + luz + a PortaDeEntrada
            var entrada = new GameObject("Entrada");
            entrada.transform.SetParent(raiz.transform, false);
            entrada.transform.localPosition = new Vector3(b.center.x, 0f, b.min.z - 2f);
            entrada.transform.localRotation = Quaternion.LookRotation(Vector3.forward); // de frente para a torre
            var porta = entrada.AddComponent<PortaDeEntrada>();
            porta.texto = "Entrar";
            porta.distancia = 4f;

            var luz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            luz.name = "PortaDeLuz";
            Object.DestroyImmediate(luz.GetComponent<Collider>());
            luz.transform.SetParent(entrada.transform, false);
            luz.transform.localPosition = new Vector3(0f, 2.5f, 1.6f);
            luz.transform.localScale = new Vector3(3.5f, 5f, 0.3f);
            luz.GetComponent<MeshRenderer>().sharedMaterial = MaterialDaPorta();

            var ponto = new GameObject("Luz").AddComponent<Light>();
            ponto.transform.SetParent(entrada.transform, false);
            ponto.transform.localPosition = new Vector3(0f, 3f, 0f);
            ponto.type = LightType.Point; ponto.color = new Color(0.2f, 0.85f, 1f); ponto.intensity = 3f; ponto.range = 10f;
            ponto.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabTorre);
            Object.DestroyImmediate(raiz);
        }

        static void CriarPrefabArena(GameObject fbx)
        {
            var raiz = new GameObject("Arena_Galpao");
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            modelo.transform.SetParent(raiz.transform, false);
            Estatico(modelo);

            var (pontaA, pontaB) = Pontas(modelo);
            Vector3 eixo = (pontaB - pontaA); eixo.y = 0f; eixo.Normalize();

            var chegada = new GameObject("Chegada").transform;
            chegada.SetParent(raiz.transform, false);
            chegada.localPosition = new Vector3(pontaA.x, 0f, pontaA.z) + eixo * ChegadaDaPonta;
            chegada.localRotation = Quaternion.LookRotation(eixo);

            // Luzes do galpão: duas fileiras ao longo do comprimento (sem sombra)
            var luzes = new GameObject("Luzes").transform;
            luzes.SetParent(raiz.transform, false);
            var b = Limites(modelo);
            Vector3 lado = Vector3.Cross(Vector3.up, eixo);
            float comprimento = Vector3.Distance(pontaA, pontaB), largura = Mathf.Abs(Vector3.Dot(b.size, lado));
            for (int i = 0; i < 6; i++)
                for (int j = -1; j <= 1; j += 2)
                {
                    var l = new GameObject($"Luz_{i}_{(j < 0 ? "E" : "D")}").AddComponent<Light>();
                    l.transform.SetParent(luzes, false);
                    l.transform.localPosition = new Vector3(pontaA.x, 5f, pontaA.z) + eixo * ((i + 0.5f) * comprimento / 6f) + lado * (j * largura * 0.25f);
                    l.type = LightType.Point; l.range = 18f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                    l.color = i % 2 == 0 ? new Color(1f, 0.9f, 0.75f) : new Color(0.55f, 0.8f, 1f);
                }

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabArena);
            Object.DestroyImmediate(raiz);
            FecharGalpao();
        }

        // =====================================================================
        // Fechamento do galpão
        // =====================================================================

        [MenuItem("BackCountry/Torre e Arena/Fechar o galpão (portões, chão de fora, barreiras, NavMesh)")]
        static void MenuFechar() => FecharGalpao(true);

        /// <summary>
        /// Fecha o galpão para ninguém ver nem cair no "limbo": troca os portões abertos por fechados,
        /// põe um chão de terra em volta (o que se vê pelas janelas), paredes invisíveis por fora,
        /// deixa o vidro fosco e dá ao galpão um NavMesh Surface próprio (ele fica fora do volume do NavMesh do mapa).
        /// Edita o prefab no lugar, então a cena não perde nada (posição, porta → Chegada etc.).
        /// </summary>
        static void FecharGalpao(bool refazer = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabArena);
            if (asset == null || (!refazer && asset.transform.Find("Fechamento") != null)) return;
            var raiz = PrefabUtility.LoadPrefabContents(PrefabArena);
            try
            {
                var velho = raiz.transform.Find("Fechamento");
                if (velho != null && !refazer) return;
                if (velho != null) Object.DestroyImmediate(velho.gameObject);

                var modelo = raiz.GetComponentsInChildren<Transform>(true).First(t => t.name == "Arena_Galpao" && t != raiz.transform).gameObject;
                var fech = new GameObject("Fechamento").transform;
                fech.SetParent(raiz.transform, false);
                Renderer[] rs = modelo.GetComponentsInChildren<Renderer>(true);

                // 1) Portões: onde há portão aberto, desliga e põe uma cópia do fechado (mesmo encaixe no vão)
                var molduras = rs.Where(r => r.name.StartsWith("wall_gates")).ToList();
                var fechado = rs.FirstOrDefault(r => r.name.StartsWith("Gates_closed"));
                Renderer MolduraMaisPerto(Renderer r) => molduras.OrderBy(m => Vector3.Distance(m.bounds.center, r.bounds.center)).First();
                int portoes = 0;
                if (fechado != null && molduras.Count > 0)
                {
                    Vector3 encaixe = fechado.transform.position - MolduraMaisPerto(fechado).bounds.center;
                    foreach (var aberto in rs.Where(r => r.name.StartsWith("Gates_opened")))
                    {
                        var vao = MolduraMaisPerto(aberto);
                        var copia = Object.Instantiate(fechado.gameObject, fech);
                        copia.name = "Portao_Fechado_" + aberto.name;
                        copia.transform.SetPositionAndRotation(vao.bounds.center + encaixe, fechado.transform.rotation);
                        copia.transform.localScale = fechado.transform.lossyScale;
                        aberto.gameObject.SetActive(false);
                        portoes++;
                    }
                }

                var b = Limites(modelo);

                // 2) Chão de terra em volta (com colisão: se algo escapar, não cai no vazio)
                var chao = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chao.name = "ChaoDeFora";
                chao.transform.SetParent(fech, false);
                chao.transform.localPosition = new Vector3(b.center.x, -0.52f, b.center.z); // topo 2 cm abaixo do piso
                chao.transform.localScale = new Vector3(400f, 1f, 400f);
                chao.GetComponent<MeshRenderer>().sharedMaterial = MaterialChaoDeFora();

                // 3) Paredes invisíveis logo por fora das paredes do galpão, e um teto invisível
                float alto = b.size.y + 4f;
                void Barreira(string n, Vector3 centro, Vector3 tamanho)
                {
                    var g = new GameObject(n);
                    g.transform.SetParent(fech, false);
                    g.transform.localPosition = centro;
                    g.AddComponent<BoxCollider>().size = tamanho;
                }
                Barreira("Barreira_X-", new Vector3(b.min.x - 0.6f, alto / 2f, b.center.z), new Vector3(1f, alto, b.size.z + 2f));
                Barreira("Barreira_X+", new Vector3(b.max.x + 0.6f, alto / 2f, b.center.z), new Vector3(1f, alto, b.size.z + 2f));
                Barreira("Barreira_Z-", new Vector3(b.center.x, alto / 2f, b.min.z - 0.6f), new Vector3(b.size.x + 2f, alto, 1f));
                Barreira("Barreira_Z+", new Vector3(b.center.x, alto / 2f, b.max.z + 0.6f), new Vector3(b.size.x + 2f, alto, 1f));
                Barreira("Barreira_Teto", new Vector3(b.center.x, b.max.y + 0.6f, b.center.z), new Vector3(b.size.x + 2f, 1f, b.size.z + 2f));
                Estatico(fech.gameObject);

                // 4) Vidro fosco: dá para ver a luz de fora, mas não o "fim do mundo"
                var vidro = AssetDatabase.LoadAssetAtPath<Material>(PastaEnv + "/Arena/Materiais/GALPAO_Glass.mat");
                if (vidro != null)
                {
                    vidro.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.88f, 0.85f));
                    vidro.SetFloat("_Smoothness", 0.4f);
                    EditorUtility.SetDirty(vidro);
                }

                // 5) NavMesh próprio do galpão (o NavMesh do mapa só cobre um volume em volta da fase)
                var sup = raiz.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (sup == null) sup = raiz.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                sup.agentTypeID = 0; // Humanoid
                sup.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
                sup.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabArena);
                Debug.Log($"Torre e Arena: galpão fechado ({portoes} portões fechados, chão de fora, barreiras, vidro fosco, NavMesh Surface próprio). " +
                          "Faça o Bake do NavMesh Surface do Arena_Galpao.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static Material MaterialChaoDeFora()
        {
            string caminho = PastaEnv + "/Arena/Materiais/ARENA_ChaoDeFora.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, caminho); }
            var terra = AssetDatabase.LoadAssetAtPath<Texture2D>(PastaEnv + "/Arena/Texturas/Galpao_concrete_Cor.jpg");
            mat.SetTexture("_BaseMap", terra);
            mat.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
            mat.SetColor("_BaseColor", new Color(0.75f, 0.6f, 0.45f));
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>As duas pontas do galpão (os pilares das cabeceiras), no espaço do prefab.</summary>
        static (Vector3 a, Vector3 b) Pontas(GameObject modelo)
        {
            var cab = modelo.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("side_column")).ToList();
            if (cab.Count >= 2) return (cab[0].bounds.center, cab[1].bounds.center);
            var b = Limites(modelo); // sem os pilares: usa o lado mais comprido
            Vector3 meio = b.extents.x >= b.extents.z ? new Vector3(b.extents.x, 0, 0) : new Vector3(0, 0, b.extents.z);
            return (b.center - meio, b.center + meio);
        }

        static Bounds Limites(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Estatico(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        // =====================================================================
        // Cena
        // =====================================================================

        static void ColocarNaCena()
        {
            var pTorre = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTorre);
            var pArena = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabArena);
            if (pTorre == null || pArena == null) { Debug.LogError("Torre e Arena: rode 'Recriar materiais e prefabs' antes."); return; }

            Scene cena = SceneManager.GetSceneByPath(CaminhoCena);
            if (!cena.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Single);
            }
            bool estavaSuja = cena.isDirty;
            var grupo = Achar(cena, "Cena4_Torre");
            var terreno = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == cena);

            // ---------- Torre no datacenter, com a entrada virada para o fim da estrada ----------
            var torre = Achar(cena, "Torre_NimbusCorp");
            if (torre == null)
            {
                torre = (GameObject)PrefabUtility.InstantiatePrefab(pTorre, cena);
                Undo.RegisterCreatedObjectUndo(torre, "Torre e Arena");
                if (grupo != null) torre.transform.SetParent(grupo.transform, true);
            }
            var dc = Achar(cena, CaatingaLayoutSettings.DatacenterName);
            Vector3 centro = dc != null ? dc.transform.position + new Vector3(0f, 0f, 15f) : new Vector3(600f, 20f, 615f);
            var estrada = Achar(cena, CaatingaLayoutSettings.EstradaName);
            Vector3 fimDaEstrada = estrada != null && estrada.transform.childCount > 0
                ? estrada.transform.GetChild(estrada.transform.childCount - 1).position : centro + new Vector3(-20f, 0f, -70f);
            Vector3 paraEstrada = fimDaEstrada - centro; paraEstrada.y = 0f; paraEstrada.Normalize();
            if (terreno != null) centro.y = terreno.SampleHeight(centro) + terreno.transform.position.y;
            // a frente do prefab (onde fica a Entrada) é o -Z local
            Undo.RecordObject(torre.transform, "Torre e Arena");
            torre.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(-paraEstrada));

            // ---------- Arena longe do mapa ----------
            var arena = Achar(cena, "Arena_Galpao");
            if (arena == null)
            {
                arena = (GameObject)PrefabUtility.InstantiatePrefab(pArena, cena);
                Undo.RegisterCreatedObjectUndo(arena, "Torre e Arena");
                if (grupo != null) arena.transform.SetParent(grupo.transform, true);
            }
            Undo.RecordObject(arena.transform, "Torre e Arena");
            arena.transform.SetPositionAndRotation(PosicaoDaArena, Quaternion.identity);

            var chegada = arena.transform.Find("Chegada");
            var entrada = torre.GetComponentInChildren<PortaDeEntrada>(true);
            if (entrada != null && chegada != null)
            {
                Undo.RecordObject(entrada, "Torre e Arena");
                entrada.destino = chegada;
                // ajusta a altura da entrada ao chão (o terreno pode não ser plano na frente da fachada)
                if (terreno != null)
                {
                    Undo.RecordObject(entrada.transform, "Torre e Arena");
                    var p = entrada.transform.position;
                    p.y = terreno.SampleHeight(p) + terreno.transform.position.y;
                    entrada.transform.position = p;
                }
            }

            // ---------- Boss, Coronel e camarote dentro do galpão ----------
            if (chegada != null)
            {
                Vector3 frente = chegada.forward;
                Vector3 posBoss = chegada.position + frente * BossDaChegada;
                Vector3 posCoronel = posBoss + frente * CoronelAtrasDoBoss;
                Quaternion olhandoChegada = Quaternion.LookRotation(-frente);
                Mover(Achar(cena, "Boss"), posBoss, olhandoChegada);
                var camarote = Achar(cena, "Camarote_Coronel_PLACEHOLDER");
                if (camarote != null)
                {
                    Undo.RecordObject(camarote.transform, "Torre e Arena");
                    camarote.transform.SetPositionAndRotation(posCoronel + Vector3.up * (AlturaDoCamarote * 0.5f), olhandoChegada);
                    camarote.transform.localScale = new Vector3(4f, AlturaDoCamarote, 4f);
                }
                Mover(Achar(cena, "NPCBoss"), posCoronel + Vector3.up * AlturaDoCamarote, olhandoChegada);

                // Ponto de retorno na chegada: morrendo na luta, renasce dentro do galpão
                var retorno = Achar(cena, "PontoDeRetorno_Arena");
                var pRet = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRetorno);
                if (retorno == null && pRet != null)
                {
                    retorno = (GameObject)PrefabUtility.InstantiatePrefab(pRet, cena);
                    retorno.name = "PontoDeRetorno_Arena";
                    Undo.RegisterCreatedObjectUndo(retorno, "Torre e Arena");
                    if (grupo != null) retorno.transform.SetParent(grupo.transform, true);
                }
                if (retorno != null)
                {
                    Mover(retorno, chegada.position, chegada.rotation);
                    if (retorno.TryGetComponent<BoxCollider>(out var caixa)) { Undo.RecordObject(caixa, "Torre e Arena"); caixa.size = new Vector3(12f, 3f, 3f); }
                }
            }

            EditorSceneManager.MarkSceneDirty(cena);
            if (!estavaSuja) EditorSceneManager.SaveScene(cena);
            Debug.Log($"Torre e Arena: torre em {centro:F0} com a entrada virada para a estrada; galpão em {PosicaoDaArena:F0}; " +
                      "Boss, Coronel e ponto de retorno dentro do galpão. " +
                      (estavaSuja ? "Salve a cena com Ctrl+S. " : "Cena salva. ") + "Refaça o Bake do NavMesh.");
        }

        static void Mover(GameObject go, Vector3 pos, Quaternion rot)
        {
            if (go == null) return;
            Undo.RecordObject(go.transform, "Torre e Arena");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
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

        // ---------- JSON mínimo (objetos, listas, números, textos, true/false/null) ----------
        static class Json
        {
            public static object Ler(string s) { int i = 0; return V(s, ref i); }
            static void P(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            static object V(string s, ref int i)
            {
                P(s, ref i);
                switch (s[i])
                {
                    case '{':
                        var d = new Dictionary<string, object>(); i++;
                        while (true)
                        {
                            P(s, ref i); if (s[i] == '}') { i++; return d; }
                            string k = T(s, ref i); P(s, ref i); i++; d[k] = V(s, ref i); P(s, ref i); if (s[i] == ',') i++;
                        }
                    case '[':
                        var l = new List<object>(); i++;
                        while (true)
                        {
                            P(s, ref i); if (s[i] == ']') { i++; return l; }
                            l.Add(V(s, ref i)); P(s, ref i); if (s[i] == ',') i++;
                        }
                    case '"': return T(s, ref i);
                }
                if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int a = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(a, i - a), System.Globalization.CultureInfo.InvariantCulture);
            }
            static string T(string s, ref int i)
            {
                var sb = new System.Text.StringBuilder(); i++;
                while (s[i] != '"') { if (s[i] == '\\') i++; sb.Append(s[i]); i++; }
                i++; return sb.ToString();
            }
        }
    }
}
