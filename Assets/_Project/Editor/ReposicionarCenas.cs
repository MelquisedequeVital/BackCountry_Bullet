using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackcountryBullet.EditorTools
{
    /// <summary>
    /// Põe as cenas 1 a 3 do tutorial em ordem ao longo da Estrada (do começo, na vila, até a torre):
    ///  Cena 1: o bar do lado do começo da estrada, porta virada para ela; o Jogador começa na frente da porta;
    ///          os robôs azuis mais à frente, na estrada, vistos pela janela lateral aberta.
    ///  Cena 2: a praça (Véio Bento e moradores) mais além; a zona de alerta e os robôs roxos depois.
    ///  Cena 3: o portão (Dona Socorro) mais além ainda; a zona e o robô laranja depois.
    ///  A Cena 4 (torre) não muda.
    /// Também aplaina o terreno embaixo do bar e tira a vegetação (árvores e capim do terreno e o scatter da cena)
    /// de dentro do bar, da frente dele e de perto de cada personagem.
    /// Roda sozinho uma vez; para refazer: BackCountry → Cenas → Reposicionar.
    /// As posições são "distância ao longo da estrada" (m, a partir do P00) + "lado" (m, + = à direita de quem
    /// vai para a torre), então se a estrada mudar é só rodar de novo.
    /// </summary>
    [InitializeOnLoad]
    public static class ReposicionarCenas
    {
        const string CaminhoCena = "Assets/_Project/Scenes/Caatinga.unity";
        const string ChaveAuto = "BackCountry.Cenas.Reposicionadas.v1";

        // ----- Cena 1: bar -----
        // A porta do bar fica antes do começo da estrada (dentro da área da vila), do lado direito, virada para a estrada.
        const float BarAntesDoComeco = 6f;   // m antes do P00
        const float BarLado = 9f;            // m para o lado da estrada até a porta
        const float FrenteDaPorta = 4f;      // onde o Jogador começa, saindo da porta em direção à estrada
        const float AtrasDoBalcao = 1.3f;

        struct Lugar
        {
            public string nome; public float s, lado; public Olhar olhar;
            public Lugar(string n, float s, float lado, Olhar o) { nome = n; this.s = s; this.lado = lado; olhar = o; }
        }
        enum Olhar { ParaQuemVem, ParaATorre, ParaAEstrada }

        static readonly Lugar[] Lugares =
        {
            // Cena 1 — robôs azuis na estrada, a ~35 m da janela
            new Lugar("InimigoTutorial1_A", 32f, 2f, Olhar.ParaQuemVem),
            new Lugar("InimigoTutorial1_B", 38f, -2f, Olhar.ParaQuemVem),
            // Cena 2 — praça
            new Lugar("PontoDeRetorno_Praca", 58f, 0f, Olhar.ParaATorre),
            new Lugar("NPCTutorial2", 74f, 6f, Olhar.ParaQuemVem),
            new Lugar("NPCMorador_1", 68f, 8f, Olhar.ParaAEstrada),
            new Lugar("NPCMorador_2", 80f, 9f, Olhar.ParaAEstrada),
            new Lugar("NPCMorador_3", 76f, -8f, Olhar.ParaAEstrada),
            new Lugar("InimigoTutorial2_ZonaAlerta", 88f, 0f, Olhar.ParaATorre),
            new Lugar("InimigoTutorial2_A", 106f, -6f, Olhar.ParaQuemVem),
            new Lugar("InimigoTutorial2_B", 107f, 0f, Olhar.ParaQuemVem),
            new Lugar("InimigoTutorial2_C", 106f, 6f, Olhar.ParaQuemVem),
            // Cena 3 — portão
            new Lugar("PontoDeRetorno_Portao", 130f, 0f, Olhar.ParaATorre),
            new Lugar("NPCTutorial3", 142f, 6f, Olhar.ParaQuemVem),
            new Lugar("InimigoTutorial3_ZonaAlerta", 153f, 0f, Olhar.ParaATorre),
            new Lugar("InimigoTutorial3", 171f, 0f, Olhar.ParaQuemVem),
        };

        // Largura das caixas que precisam cobrir a estrada de lado a lado
        static readonly Dictionary<string, float> LarguraDaCaixa = new()
        {
            { "PontoDeRetorno_Praca", 12f }, { "PontoDeRetorno_Portao", 12f },
            { "InimigoTutorial2_ZonaAlerta", 16f }, { "InimigoTutorial3_ZonaAlerta", 16f },
        };

        static ReposicionarCenas()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(ChaveAuto, false)) return;
                var cena = SceneManager.GetSceneByPath(CaminhoCena);
                if (!cena.isLoaded) return;
                if (Reposicionar(cena)) EditorPrefs.SetBool(ChaveAuto, true);
            };
        }

        [MenuItem("BackCountry/Cenas/Reposicionar cenas 1-3 na estrada (e limpar vegetação)")]
        static void Menu()
        {
            var cena = SceneManager.GetSceneByPath(CaminhoCena);
            if (!cena.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Single);
            }
            Reposicionar(cena);
        }

        // =====================================================================

        static bool Reposicionar(Scene cena)
        {
            var layout = Achar(cena, CaatingaLayoutSettings.LayoutRootName);
            var estrada = layout != null ? layout.transform.Find(CaatingaLayoutSettings.EstradaName) : null;
            var pontos = RoadPath.Sample(RoadPath.GetControlPoints(estrada), 0.5f);
            var terreno = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == cena);
            var bar = Achar(cena, "Bar_ZeFerrugem");
            var jogador = Achar(cena, "Jogador");
            var seuZe = Achar(cena, "NPCTutorial1");
            if (pontos.Count < 2 || terreno == null || bar == null || jogador == null || seuZe == null)
            {
                Debug.LogWarning("Cenas: faltou a Estrada, o Terrain, o Bar_ZeFerrugem, o Jogador ou o NPCTutorial1 na Caatinga; nada mudou.");
                return false;
            }
            bool estavaSuja = cena.isDirty;
            var acumulado = Acumulado(pontos);
            var limpar = new List<(Vector3 c, float r)>();

            // ---------- Cena 1: bar, Seu Zé, Jogador ----------
            Vector3 inicio = pontos[0];
            Vector3 frenteInicio = Tangente(pontos, acumulado, 0f);
            Vector3 direitaInicio = Direita(frenteInicio);
            Vector3 porta = inicio - frenteInicio * BarAntesDoComeco + direitaInicio * BarLado;
            Vector3 paraEstrada = -direitaInicio; // a porta olha para a estrada

            Undo.RecordObject(bar.transform, "Reposicionar cenas");
            var pecas = PecasDoBar(bar);
            if (pecas == null) return false;
            var (balcaoL, portaL, pisoL, salaL) = pecas.Value;
            Vector3 dirL = portaL - balcaoL; dirL.y = 0f; dirL.Normalize();
            // Gira o bar para a porta olhar para a estrada (dirL é a direção balcão → porta no espaço do bar)
            Quaternion giro = Quaternion.LookRotation(paraEstrada) * Quaternion.Inverse(Quaternion.LookRotation(dirL));
            float alturaPiso = Altura(terreno, porta);
            Vector3 portaNoChao = new Vector3(portaL.x, pisoL, portaL.z);
            bar.transform.SetPositionAndRotation(new Vector3(porta.x, alturaPiso, porta.z) - giro * portaNoChao, giro);

            Vector3 centroBar = bar.transform.TransformPoint(new Vector3(salaL.center.x, pisoL, salaL.center.z));
            Vector3 meiaSala = new Vector3(salaL.extents.x, 0f, salaL.extents.z);

            Vector3 lugarSeuZe = bar.transform.TransformPoint(new Vector3(balcaoL.x, pisoL, balcaoL.z) - dirL * AtrasDoBalcao);
            Mover(seuZe, lugarSeuZe, Quaternion.LookRotation(paraEstrada));

            Vector3 lugarJogador = porta + paraEstrada * FrenteDaPorta;
            lugarJogador.y = alturaPiso;
            // O Jogador começa olhando para o bar; o pivô dele fica ~1 m acima do chão (como estava antes)
            float pivoJogador = jogador.transform.position.y - Altura(terreno, jogador.transform.position);
            Mover(jogador, lugarJogador + Vector3.up * Mathf.Clamp(pivoJogador, 0f, 1.5f), Quaternion.LookRotation(-paraEstrada));

            // Marcador do layout acompanha o bar
            var spot = layout.transform.Find(CaatingaLayoutSettings.VilaName + "/" + CaatingaLayoutSettings.BarName);
            if (spot != null) { Undo.RecordObject(spot, "Reposicionar cenas"); spot.position = centroBar; }

            // ---------- Cenas 1 a 3 ao longo da estrada ----------
            foreach (var l in Lugares)
            {
                var go = Achar(cena, l.nome);
                if (go == null) { Debug.LogWarning($"Cenas: não achei {l.nome}."); continue; }
                Vector3 frente = Tangente(pontos, acumulado, l.s);
                Vector3 p = NaEstrada(pontos, acumulado, l.s) + Direita(frente) * l.lado;
                p.y = Altura(terreno, p);
                Quaternion rot = l.olhar switch
                {
                    Olhar.ParaATorre => Quaternion.LookRotation(frente),
                    Olhar.ParaAEstrada => Quaternion.LookRotation(Plano(NaEstrada(pontos, acumulado, l.s - 4f) - p)),
                    _ => Quaternion.LookRotation(Plano(NaEstrada(pontos, acumulado, l.s - 10f) - p)),
                };
                Mover(go, p, rot);
                if (LarguraDaCaixa.TryGetValue(l.nome, out float largura) && go.TryGetComponent<BoxCollider>(out var caixa))
                {
                    Undo.RecordObject(caixa, "Reposicionar cenas");
                    var tam = caixa.size; tam.x = largura; caixa.size = tam;
                }
                if (!l.nome.Contains("Zona") && !l.nome.Contains("PontoDeRetorno")) limpar.Add((p, 2.5f));
            }

            // ---------- Janela aberta para os robôs azuis e zona da janela ----------
            var robo = Achar(cena, "InimigoTutorial1_A");
            AbrirJanelaParaOsRobos(bar, robo != null ? robo.transform.position : porta + frenteInicio * 30f, centroBar, giro, alturaPiso, cena);

            // ---------- Terreno e vegetação ----------
            limpar.Add((lugarJogador, 3f));
            int removidos = LimparEAplainar(cena, terreno, centroBar, giro, meiaSala, porta, paraEstrada, alturaPiso, limpar);

            EditorSceneManager.MarkSceneDirty(cena);
            AssetDatabase.SaveAssets();
            if (!estavaSuja) EditorSceneManager.SaveScene(cena);
            Debug.Log($"Cenas: bar no começo da estrada (porta em {porta:F0}), Jogador na frente; cenas 2 e 3 mais adiante na estrada. " +
                      $"{removidos} plantas/objetos tirados de dentro e da frente do bar e de perto dos personagens. " +
                      (estavaSuja ? "Salve a cena com Ctrl+S. " : "Cena salva. ") + "Refaça o Bake do NavMesh.");
            return true;
        }

        // =====================================================================
        // Bar
        // =====================================================================

        /// <summary>Centro do balcão, centro da porta, altura do piso e caixa da sala, no espaço local do bar.</summary>
        static (Vector3 balcao, Vector3 porta, float piso, Bounds sala)? PecasDoBar(GameObject bar)
        {
            Renderer R(string n) => bar.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.gameObject.name == n);
            var balcao = R("Balcao_Corpo"); var porta = R("Porta_Principal_Moldura");
            var cadeira = R("Cadeira_Mesa01_1"); var sala = R("Sala_Paredes_Piso");
            if (balcao == null || porta == null || cadeira == null || sala == null)
            {
                Debug.LogWarning("Cenas: o Bar_ZeFerrugem não tem as peças esperadas (balcão/porta/cadeira/sala).");
                return null;
            }
            var t = bar.transform;
            Vector3 L(Vector3 w) => t.InverseTransformPoint(w);
            // caixa da sala em espaço local (a partir dos 8 cantos da malha)
            var mf = sala.GetComponent<MeshFilter>();
            var b = new Bounds(L(sala.bounds.center), Vector3.zero);
            if (mf != null && mf.sharedMesh != null)
            {
                var mb = mf.sharedMesh.bounds; var st = sala.transform;
                b = new Bounds(L(st.TransformPoint(mb.center)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    b.Encapsulate(L(st.TransformPoint(c)));
                }
            }
            return (L(balcao.bounds.center), L(porta.bounds.center), L(cadeira.bounds.min).y, b);
        }

        static void AbrirJanelaParaOsRobos(GameObject bar, Vector3 robo, Vector3 centroBar, Quaternion giro, float piso, Scene cena)
        {
            var janelas = new[] { "Janela_03", "Janela_04", "Janela_05", "Janela_06" }
                .Select(n => (n, m: bar.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.gameObject.name == n + "_Moldura")))
                .Where(x => x.m != null).ToList();
            if (janelas.Count == 0) return;
            var escolhida = janelas.OrderBy(x => Vector3.Distance(x.m.bounds.center, robo)).First();
            foreach (var (n, _) in janelas)
            {
                var vitral = bar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n + "_Vitral");
                if (vitral == null) continue;
                bool aberta = n == escolhida.n;
                if (vitral.gameObject.activeSelf == !aberta) continue;
                Undo.RecordObject(vitral.gameObject, "Reposicionar cenas");
                vitral.gameObject.SetActive(!aberta);
            }
            var zona = Achar(cena, "NPCTutorial1_ZonaJanela");
            if (zona != null)
            {
                Vector3 c = escolhida.m.bounds.center;
                Vector3 paraDentro = Plano(centroBar - c);
                Vector3 pos = c + paraDentro * 1f; pos.y = piso;
                Mover(zona, pos, giro);
            }
            Debug.Log($"Cenas: {escolhida.n} aberta (sem vitral), virada para os robôs azuis.");
        }

        // =====================================================================
        // Terreno e vegetação
        // =====================================================================

        static int LimparEAplainar(Scene cena, Terrain terreno, Vector3 centroBar, Quaternion giro, Vector3 meiaSala,
                                   Vector3 porta, Vector3 paraEstrada, float alturaPiso, List<(Vector3 c, float r)> circulos)
        {
            var td = terreno.terrainData;
            Vector3 origem = terreno.transform.position, tam = td.size;
            Quaternion inv = Quaternion.Inverse(giro);
            const float margem = 2.5f;   // vegetação fora da parede
            const float transicao = 6f;  // rampa do terreno aplainado até o original

            // Retângulo do bar (com a margem) em volta de centroBar, girado com o bar; mais a faixa da porta até a estrada
            float DistBar(Vector3 p)
            {
                Vector3 l = inv * (p - centroBar);
                float dx = Mathf.Max(0f, Mathf.Abs(l.x) - meiaSala.x), dz = Mathf.Max(0f, Mathf.Abs(l.z) - meiaSala.z);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
            float DistFrente(Vector3 p) // faixa de 6 m de largura da porta até 9 m para a frente
            {
                Vector3 d = Plano(p - porta);
                float ao = Vector3.Dot(d, paraEstrada);
                float lado = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, paraEstrada)));
                float fora = Mathf.Max(0f, lado - 3f) + Mathf.Max(0f, -ao) + Mathf.Max(0f, ao - 9f);
                return fora;
            }
            bool Limpar(Vector3 p) =>
                DistBar(p) < margem || DistFrente(p) < 0.5f || circulos.Any(c => Plano(p - c.c).magnitude < c.r);

            // --- relevo: chão plano na altura do piso embaixo do bar e na frente dele, com rampa suave ---
            Undo.RegisterCompleteObjectUndo(td, "Reposicionar cenas");
            int res = td.heightmapResolution;
            float raio = Mathf.Max(meiaSala.x, meiaSala.z) + margem + transicao + 10f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((centroBar.x - raio - origem.x) / tam.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((centroBar.z - raio - origem.z) / tam.z * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((centroBar.x + raio - origem.x) / tam.x * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((centroBar.z + raio - origem.z) / tam.z * (res - 1)), 0, res - 1);
            var h = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float alvo = (alturaPiso - 0.02f - origem.y) / tam.y; // o piso do bar fica um tiquinho acima do chão
            for (int z = 0; z < h.GetLength(0); z++)
                for (int x = 0; x < h.GetLength(1); x++)
                {
                    var p = new Vector3(origem.x + (x0 + x) / (float)(res - 1) * tam.x, 0f, origem.z + (z0 + z) / (float)(res - 1) * tam.z);
                    float d = Mathf.Min(DistBar(p), DistFrente(p));
                    float peso = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(margem, margem + transicao, d));
                    if (peso > 0f) h[z, x] = Mathf.Lerp(h[z, x], alvo, peso);
                }
            td.SetHeights(x0, z0, h);

            int removidos = 0;

            // --- árvores do terreno ---
            var arvores = td.treeInstances;
            var ficam = arvores.Where(a => !Limpar(origem + Vector3.Scale(a.position, tam))).ToArray();
            removidos += arvores.Length - ficam.Length;
            td.treeInstances = ficam;

            // --- capim/detalhes do terreno ---
            int dres = td.detailResolution;
            for (int camada = 0; camada < td.detailPrototypes.Length; camada++)
            {
                var mapa = td.GetDetailLayer(0, 0, dres, dres, camada);
                bool mudou = false;
                // só a região perto dos pontos a limpar
                foreach (var (c, r) in circulos.Append((centroBar, raio)))
                {
                    int cx0 = Mathf.Clamp(Mathf.FloorToInt((c.x - r - origem.x) / tam.x * dres), 0, dres - 1);
                    int cz0 = Mathf.Clamp(Mathf.FloorToInt((c.z - r - origem.z) / tam.z * dres), 0, dres - 1);
                    int cx1 = Mathf.Clamp(Mathf.CeilToInt((c.x + r - origem.x) / tam.x * dres), 0, dres - 1);
                    int cz1 = Mathf.Clamp(Mathf.CeilToInt((c.z + r - origem.z) / tam.z * dres), 0, dres - 1);
                    for (int z = cz0; z <= cz1; z++)
                        for (int x = cx0; x <= cx1; x++)
                        {
                            if (mapa[z, x] == 0) continue;
                            var p = new Vector3(origem.x + (x + 0.5f) / dres * tam.x, 0f, origem.z + (z + 0.5f) / dres * tam.z);
                            if (!Limpar(p)) continue;
                            mapa[z, x] = 0; mudou = true;
                        }
                }
                if (mudou) td.SetDetailLayer(0, 0, camada, mapa);
            }
            terreno.Flush();
            EditorUtility.SetDirty(td);

            // --- scatter da cena (rochas, troncos, cactos colocados como objetos) ---
            var scatter = cena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Scatter").ToList();
            foreach (var raiz in scatter)
            {
                var alvos = raiz.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != raiz && (t.GetComponent<Renderer>() != null || t.GetComponent<LODGroup>() != null || PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
                    .Select(t => PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) ?? t.gameObject)
                    .Distinct().Where(g => g != null && g.transform.IsChildOf(raiz) && Limpar(g.transform.position)).ToList();
                foreach (var g in alvos)
                {
                    if (g == null) continue; // já saiu junto com o pai
                    Undo.DestroyObjectImmediate(g); removidos++;
                }
            }
            return removidos;
        }

        // =====================================================================
        // Estrada
        // =====================================================================

        static List<float> Acumulado(List<Vector3> p)
        {
            var a = new List<float> { 0f };
            for (int i = 1; i < p.Count; i++) a.Add(a[i - 1] + Plano(p[i] - p[i - 1]).magnitude);
            return a;
        }

        static Vector3 NaEstrada(List<Vector3> p, List<float> a, float s)
        {
            if (s <= 0f) return p[0] + Tangente(p, a, 0f) * s;
            for (int i = 1; i < p.Count; i++)
                if (a[i] >= s) return Vector3.Lerp(p[i - 1], p[i], Mathf.InverseLerp(a[i - 1], a[i], s));
            return p[p.Count - 1];
        }

        static Vector3 Tangente(List<Vector3> p, List<float> a, float s)
        {
            Vector3 d = NaEstradaSemExtrapolar(p, a, Mathf.Max(s, 0f) + 2f) - NaEstradaSemExtrapolar(p, a, Mathf.Max(s, 0f) - 2f);
            return Plano(d);
        }

        static Vector3 NaEstradaSemExtrapolar(List<Vector3> p, List<float> a, float s)
        {
            s = Mathf.Clamp(s, 0f, a[a.Count - 1]);
            for (int i = 1; i < p.Count; i++)
                if (a[i] >= s) return Vector3.Lerp(p[i - 1], p[i], Mathf.InverseLerp(a[i - 1], a[i], s));
            return p[p.Count - 1];
        }

        static Vector3 Direita(Vector3 frente) => Vector3.Cross(Vector3.up, frente).normalized;
        // Vetor no plano do chão (nunca zero, para o LookRotation não reclamar)
        static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v : Vector3.forward * 1e-3f; }
        static float Altura(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        // =====================================================================

        static void Mover(GameObject go, Vector3 pos, Quaternion rot)
        {
            Undo.RecordObject(go.transform, "Reposicionar cenas");
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
    }
}
