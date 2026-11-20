using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chefe final (Mech). Uma fase, cinco ataques, todos usando o que o tutorial ensinou:
///   • Combo de espada e Corte pesado  → esquiva armada (a preparação lenta da animação é o aviso);
///   • Tiro da mão e Barragem em leque → esquiva armada / posicionamento (esferas retas, como o robô roxo);
///   • Investida com jetpack (especial) → mergulho desarmado (como o robô laranja) e depois fica ATORDOADO.
/// Leva pouquíssimo dano normalmente e muito dano atordoado (a hora de descarregar a arma).
///
/// As animações são controladas por código (CrossFade): no Animator basta criar os estados com os nomes
/// configurados abaixo, SEM setas e SEM parâmetros.
/// </summary>
public class ChefeMecha : InimigoBase, IModificadorDeDano
{
    private enum Estado { Esperando, Perseguindo, Atacando, Aviso, Investindo, Atordoado }

    // =====================================================================
    // Configuração
    // =====================================================================

    [Header("Perseguição")]
    [Tooltip("Acima desta distância ele corre atrás do jogador (abaixo, anda com a 'Velocidade' lá de cima)")]
    [Min(0f)] public float distanciaParaCorrer = 7f;
    [Tooltip("Velocidade correndo (m/s)")]
    [Min(0.1f)] public float velocidadeCorrendo = 5f;
    [Tooltip("Descanso entre um ataque e outro (segundos)")]
    [Min(0f)] public float descansoEntreAtaques = 0.7f;

    [Header("Dano recebido")]
    [Tooltip("Fração do dano fora do atordoamento (0.1 = só 10%: dá para arranhar, mas o grosso vem atordoado)")]
    [Range(0f, 1f)] public float danoForaDoAtordoamento = 0.1f;
    [Tooltip("Multiplicador do dano enquanto está atordoado")]
    [Min(0f)] public float danoQuandoAtordoado = 1.5f;
    [Tooltip("Tempo ajoelhado e vulnerável depois da investida")]
    [Min(0f)] public float tempoAtordoado = 3f;
    [Tooltip("Som de tiro batendo na armadura (fora do atordoamento)")]
    public AudioClip somDeTiroBloqueado;

    [Header("Corpo a corpo")]
    [Tooltip("Distância em que ele começa um golpe (maior que o alcance, porque ele avança durante o golpe)")]
    [Min(0.1f)] public float distanciaParaGolpear = 5f;
    [Min(0.1f)] public float alcanceDoGolpe = 4.5f;
    [Tooltip("Quantos metros ele avança durante a preparação de cada golpe (faz o golpe conectar)")]
    [Min(0f)] public float avancoDoGolpe = 2.2f;
    [Tooltip("Ele para de avançar quando chega a esta distância do jogador (não atravessa você)")]
    [Min(0f)] public float distanciaMinimaNoAvanco = 1.8f;
    [Tooltip("Largura do golpe à frente do robô (graus). Mais estreito = dar um passo para o lado funciona")]
    [Range(10f, 180f)] public float anguloDoGolpe = 80f;
    [Tooltip("Ponto da animação em que os golpes terminam")]
    [Range(0f, 1f)] public float pontoDeFimDoGolpe = 0.85f;
    [Tooltip("Fração da preparação em que ele para de virar e de avançar (o golpe 'se compromete'). " +
             "Depois disso, sair do lado ou esquivar funciona.")]
    [Range(0f, 1f)] public float pontoDeCompromisso = 0.7f;

    [Header("Preparação lenta dos golpes (o aviso)")]
    [Tooltip("O começo do golpe (o braço indo para trás) toca devagar e depois o golpe sai na velocidade normal. " +
             "Isto é até que fração do caminho até o impacto ele fica devagar (0.75 = 3/4 da preparação)")]
    [Range(0f, 1f)] public float trechoLento = 0.75f;
    [Tooltip("Velocidade da preparação no combo rápido (0.4 = 40% da velocidade normal)")]
    [Range(0.05f, 1f)] public float lentidaoDoCombo = 0.4f;
    [Tooltip("Velocidade da preparação no corte pesado (mais lenta: dá para reconhecer o golpe forte)")]
    [Range(0.05f, 1f)] public float lentidaoDoCortePesado = 0.25f;

    [Header("Golpes (pontos medidos no pico da espada)")]
    [Min(0f)] public float danoCorteRapido = 15f;
    [Range(0f, 1f)] public float pontoCorteRapido = 0.3f;
    [Min(0f)] public float danoEstocada = 15f;
    [Range(0f, 1f)] public float pontoEstocada = 0.34f;
    [Min(0f)] public float danoCortePesado = 30f;
    [Range(0f, 1f)] public float pontoCortePesado = 0.33f;

    [Header("Acerto pela lâmina da espada")]
    [Tooltip("Marcado: o golpe só acerta se a lâmina de verdade encostar no jogador (a ponta da espada vale). " +
             "Desmarcado: usa só distância + cone à frente (menos preciso).")]
    public bool acertoPelaLamina = true;
    [Tooltip("Nome do objeto da espada dentro do robô")]
    public string nomeDaEspada = "Core_Sword";
    [Tooltip("Folga em volta do corpo do jogador (metros). Maior = a lâmina acerta 'de raspão'")]
    [Min(0f)] public float folgaDoAcerto = 0.2f;
    [Tooltip("Quanto antes do ponto do golpe a lâmina já pode acertar (fração da animação)")]
    [Range(0f, 0.3f)] public float janelaAntesDoGolpe = 0.06f;
    [Tooltip("Até que ponto da animação a lâmina do CORTE RÁPIDO ainda corta (medido: a varredura acaba ~0.40)")]
    [Range(0f, 1f)] public float fimDaLaminaCorteRapido = 0.42f;
    [Tooltip("Até que ponto a lâmina da ESTOCADA ainda corta (medido: a ponta fica estendida até ~0.55)")]
    [Range(0f, 1f)] public float fimDaLaminaEstocada = 0.56f;
    [Tooltip("Até que ponto a lâmina do CORTE PESADO ainda corta (medido: desce em 0.31-0.36 e fica no chão até ~0.55)")]
    [Range(0f, 1f)] public float fimDaLaminaCortePesado = 0.56f;
    [Tooltip("Desenha a lâmina na aba Scene durante o golpe (verde = não encostou, vermelho = acertou)")]
    public bool mostrarLaminaNaCena = false;

    [Header("Acerto de perto (colado no robô)")]
    [Tooltip("Quando o jogador está colado no robô, a espada passa por cima/por trás dele. " +
             "Marcado: no pico do golpe, quem estiver bem perto e na frente leva o golpe mesmo assim.")]
    public bool acertoDePerto = true;
    [Tooltip("Até esta distância (m, do centro do robô ao jogador) o golpe acerta de perto")]
    [Min(0f)] public float distanciaDePerto = 2.8f;
    [Tooltip("Largura da área de perto à frente do robô (graus). Mais largo que o golpe normal: colado, não dá para escapar pelo lado")]
    [Range(10f, 360f)] public float anguloDePerto = 160f;

    [Header("Ataques à distância (escolha)")]
    [Tooltip("Só atira com o jogador a pelo menos esta distância")]
    [Min(0f)] public float distanciaParaAtirar = 5f;
    [Tooltip("Tempo mínimo entre dois ataques à distância")]
    [Min(0f)] public float intervaloDeTiros = 3.5f;
    [Tooltip("Chance (0 a 1) de atirar quando pode; senão ele corre até você")]
    [Range(0f, 1f)] public float chanceDeAtirar = 0.6f;
    [Tooltip("Entre os ataques à distância: chance de ser o Tiro da mão (o resto é a Barragem)")]
    [Range(0f, 1f)] public float chanceDoTiroDaMao = 0.6f;

    [Header("Tiro da mão (Mech_Shoot2.R)")]
    [Tooltip("Osso de onde saem os tiros (a mão direita, que fica livre)")]
    public string ossoDaMao = "hand.r";
    [Tooltip("Pontos da animação em que cada tiro sai (um tiro por valor). A mão fica erguida de 0.2 a 0.6.")]
    public float[] pontosDosTirosDaMao = { 0.36f, 0.46f, 0.56f };
    [Min(0.5f)] public float velocidadeDoTiroDaMao = 12f;
    [Min(0f)] public float danoDoTiroDaMao = 12f;

    [Header("Barragem em leque (Mech_ShoulderShoot1)")]
    public string[] ossosDosOmbros = { "Shoulder_L_Attachment.L", "Shoulder_L_Attachment.R" };
    [Tooltip("Pontos da animação em que cada onda sai (um valor = uma onda)")]
    public float[] pontosDasOndas = { 0.5f };
    [Tooltip("Quantas esferas por onda. Poucas, abrindo em leque: os espaços entre elas crescem com a distância")]
    [Min(1)] public int esferasPorOnda = 5;
    [Tooltip("Abertura do leque (graus)")]
    [Range(10f, 180f)] public float aberturaDoLeque = 60f;
    [Min(0.5f)] public float velocidadeDaBarragem = 9f;
    [Min(0f)] public float danoDaBarragem = 15f;

    [Header("Investida com jetpack (o especial)")]
    [Tooltip("Tempo entre investidas (segundos)")]
    [Min(1f)] public float intervaloDeEspeciais = 8f;
    [Min(0f)] public float tempoDeAvisoDaInvestida = 1f;
    [Min(1f)] public float velocidadeDaInvestida = 14f;
    [Min(0f)] public float correcaoDeRota = 140f;
    [Min(0f)] public float distanciaParaSeguirReto = 4f;
    [Min(0.1f)] public float raioDaInvestida = 2.2f;
    [Min(0f)] public float danoDaInvestida = 35f;
    [Min(0f)] public float distanciaAlemDoAlvo = 5f;
    [Min(0.1f)] public float duracaoMaximaDaInvestida = 2f;
    [Tooltip("A investida derruba o jogador (empurrão + queda + levantar)")]
    public bool derrubaOJogador = true;

    [Header("Visual")]
    public Color corDoAviso = new Color(1f, 0.4f, 0.05f);
    public Color corVulneravel = new Color(0.3f, 0.8f, 1f);
    public Color corDosTiros = new Color(1f, 0.3f, 0.1f);
    [Range(0.1f, 1f)] public float tamanhoDosTiros = 0.45f;
    [Tooltip("Fogo opcional do jetpack, ligado na investida (ex.: WarFX > Fire > WFX_FlameThrower Looped)")]
    public GameObject fogoDoJetpack;
    public string ossoDoJetpack = "Back_S_Attachment";
    public Vector3 rotacaoDoFogo = new Vector3(90f, 0f, 0f);

    [Header("Sons (todos opcionais)")]
    [Tooltip("Sons de espada; um é sorteado a cada golpe")]
    public AudioClip[] sonsDaEspada;
    [Tooltip("Quando a espada acerta o jogador")]
    public AudioClip somDaEspadaAcertando;
    [Tooltip("Cada tiro da mão")]
    public AudioClip somDoTiroDaMao;
    [Tooltip("Cada onda da barragem")]
    public AudioClip somDaBarragem;
    [Tooltip("Começo do aviso da investida")]
    public AudioClip somDoAviso;
    [Tooltip("Som em loop do jetpack durante a investida")]
    public AudioClip somDoJetpack;
    [Range(0f, 1f)] public float volumeDosAtaques = 1f;
    [Tooltip("Variação aleatória do tom, para os sons não ficarem repetitivos")]
    [Range(0f, 0.3f)] public float variacaoDeTom = 0.08f;

    [Header("Estados no Animator (Base Layer)")]
    public string estadoParado = "Parado";
    public string estadoAndar = "Andar";
    public string estadoCorteRapido = "CorteRapido";
    public string estadoEstocada = "Estocada";
    public string estadoCortePesado = "CortePesado";
    public string estadoTiroMao = "TiroMao";
    public string estadoBarragem = "Barragem";
    public string estadoInvestida = "Investida";
    public string estadoAtordoado = "Atordoado";
    public string estadoMorte = "Morte";

    // =====================================================================
    // Estado interno
    // =====================================================================

    /// <summary>Um pedaço de ataque: uma animação e os momentos em que algo acontece nela.</summary>
    private class Etapa
    {
        public int estado;
        public float[] pontos;
        public Action<int> aoChegarNoPonto; // recebe o índice do ponto
        public float fim;

        // Golpes: avanço, som da espada e preparação lenta
        public float avanco;
        public float jaAvancou;
        public bool comSomDeEspada;
        public bool somTocado;
        public float lentoAte;
        public float lentidao = 1f;

        // Acerto pela lâmina: confere o contato numa janela em volta do pontos[0]
        public bool porLamina;
        public float dano;
        public bool acertou;
        public float fimDaLamina; // até onde (fração da animação) a lâmina ainda corta
    }

    private Estado estado = Estado.Esperando;
    private float tempoNoEstado;

    private readonly Queue<Etapa> etapas = new Queue<Etapa>();
    private Etapa etapaAtual;
    private int proximoPonto;
    private bool etapaComecou;

    private float proximoAtaquePermitido;
    private float proximoTiroPermitido;
    private float proximoEspecialPermitido;

    // Investida
    private Vector3 direcaoDaInvestida;
    private float distanciaParaPercorrer, distanciaPercorrida;
    private bool seguindoReto, investidaAcertou;

    // Visual, som e referências
    private BrilhoDoCorpo brilho;
    private BarraDeVida barra;
    private Transform mao, jetpack;
    private Transform[] ombros;
    private GameObject fogo;
    private AudioSource fonte, fonteDoJetpack;
    private int locomocaoAtual;
    private int ombroDaVez;

    // Lâmina
    private Renderer[] partesDaEspada = new Renderer[0];
    private Mesh malhaTemporaria;
    private readonly List<Vector3> verticesLocais = new List<Vector3>();
    private readonly List<Vector3> pontosDaLamina = new List<Vector3>();
    private readonly List<Vector3> pontosAnteriores = new List<Vector3>();

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    protected override void Awake()
    {
        base.Awake();
        agente.stoppingDistance = distanciaParaGolpear * 0.85f;

        mao = AcharOsso(ossoDaMao);
        jetpack = AcharOsso(ossoDoJetpack);
        ombros = Array.ConvertAll(ossosDosOmbros, AcharOsso);
        brilho = new BrilhoDoCorpo(transform, new Vector3(0f, 2.5f, 1f), alcanceDaLuz: 8f, intensidadeMaximaDaLuz: 8f);
        barra = GetComponent<BarraDeVida>();

        CriarFogoDoJetpack();
        ManterPecasSempreVisiveis();
        PrepararEspada();
        foreach (string nome in NomesDosEstados()) AvisarSeFaltarEstado(nome);

        vida.AoReceberDano += AoReceberDano;
    }

    void OnDestroy()
    {
        if (vida != null) vida.AoReceberDano -= AoReceberDano;
        if (malhaTemporaria != null) Destroy(malhaTemporaria);
    }

    // Controlado por CrossFade: não precisa de parâmetros no Animator
    protected override string[] ParametrosNecessarios() => new string[0];

    /// <summary>Pouquíssimo dano normalmente; dano extra atordoado.</summary>
    public float MultiplicadorDeDano => estado == Estado.Atordoado ? danoQuandoAtordoado : danoForaDoAtordoamento;

    protected override void AtualizarIA(float deltaTime)
    {
        tempoNoEstado += deltaTime;

        if (!AlvoEstaVivo() && estado != Estado.Esperando)
        {
            LimparAtaque();
            TrocarPara(Estado.Esperando);
        }

        switch (estado)
        {
            case Estado.Esperando: AtualizarEsperando(); break;
            case Estado.Perseguindo: AtualizarPerseguindo(); break;
            case Estado.Atacando: AtualizarEtapas(); break;
            case Estado.Aviso: AtualizarAviso(); break;
            case Estado.Investindo: AtualizarInvestindo(deltaTime); break;
            case Estado.Atordoado: AtualizarAtordoado(); break;
        }
    }

    private void TrocarPara(Estado novo)
    {
        estado = novo;
        tempoNoEstado = 0f;
        locomocaoAtual = 0; // ao voltar a andar, a animação de locomoção é escolhida de novo
        if (!EstaMorto) animator.speed = 1f; // desfaz a preparação lenta, se houver
    }

    // =====================================================================
    // Esperando / Perseguindo
    // =====================================================================

    // Antes da luta: parado e com a barra escondida até o jogador chegar perto
    private void AtualizarEsperando()
    {
        PararDeAndar();
        barra?.Mostrar(false);

        if (AlvoEstaVivo() && DistanciaAteAlvo() <= distanciaDeDeteccao)
        {
            barra?.Mostrar(true);
            proximoEspecialPermitido = Time.time + intervaloDeEspeciais * 0.6f;
            TrocarPara(Estado.Perseguindo);
        }
    }

    private void AtualizarPerseguindo()
    {
        float distancia = DistanciaAteAlvo();
        bool podeAtacar = Time.time >= proximoAtaquePermitido;

        if (podeAtacar && Time.time >= proximoEspecialPermitido)
        {
            IniciarInvestida();
            return;
        }

        if (distancia <= distanciaParaGolpear)
        {
            PararDeAndar();
            VirarParaAlvo();
            if (podeAtacar) IniciarAtaqueCorpoACorpo();
            return;
        }

        // Média/longa distância: às vezes atira (tiro da mão ou barragem); no resto corre até você
        if (podeAtacar && distancia >= distanciaParaAtirar && Time.time >= proximoTiroPermitido && EstaDeFrenteParaAlvo(30f))
        {
            proximoTiroPermitido = Time.time + 1f; // não sorteia de novo todo quadro
            if (UnityEngine.Random.value < chanceDeAtirar)
            {
                proximoTiroPermitido = Time.time + intervaloDeTiros;
                if (UnityEngine.Random.value < chanceDoTiroDaMao) IniciarTiroDaMao();
                else IniciarBarragem();
                return;
            }
        }

        // Longe corre, perto anda (o jogador não consegue simplesmente "fugir" dele)
        agente.speed = distancia > distanciaParaCorrer ? velocidadeCorrendo : velocidade;
        AndarAte(alvo.position);
    }

    // =====================================================================
    // Corpo a corpo
    // =====================================================================

    // O aviso é a própria animação: a preparação do golpe toca devagar e o golpe sai rápido.
    // O corte pesado tem a preparação mais lenta, então dá para "ler" qual golpe vem.
    private void IniciarAtaqueCorpoACorpo()
    {
        if (UnityEngine.Random.value < 0.55f)
        {
            // Combo: corte rápido emendado na estocada (a estocada prepara um pouco mais rápido, mantendo o ritmo)
            AdicionarEtapaDeGolpe(estadoCorteRapido, pontoCorteRapido, danoCorteRapido, lentidaoDoCombo, fimDaLaminaCorteRapido);
            AdicionarEtapaDeGolpe(estadoEstocada, pontoEstocada, danoEstocada, Mathf.Lerp(lentidaoDoCombo, 1f, 0.5f), fimDaLaminaEstocada);
        }
        else
        {
            AdicionarEtapaDeGolpe(estadoCortePesado, pontoCortePesado, danoCortePesado, lentidaoDoCortePesado, fimDaLaminaCortePesado);
        }

        ComecarEtapas();
    }

    private void AdicionarEtapaDeGolpe(string nomeDoEstado, float ponto, float dano, float lentidao, float fimDaLamina)
    {
        bool porLamina = acertoPelaLamina && partesDaEspada.Length > 0;

        etapas.Enqueue(new Etapa
        {
            estado = Animator.StringToHash(nomeDoEstado),
            pontos = new[] { ponto },
            // Com a lâmina, o acerto é conferido numa janela (VerificarLamina); sem ela, no ponto exato
            aoChegarNoPonto = porLamina ? (Action<int>)null : _ => AplicarGolpePorDistancia(dano),
            fim = pontoDeFimDoGolpe,
            avanco = avancoDoGolpe,
            comSomDeEspada = true,
            lentoAte = ponto * trechoLento,
            lentidao = lentidao,
            porLamina = porLamina,
            dano = dano,
            fimDaLamina = Mathf.Max(fimDaLamina, ponto)
        });
    }

    // Modo antigo (sem lâmina): acerta se o jogador estiver perto e na frente no momento do golpe
    private void AplicarGolpePorDistancia(float dano)
    {
        if (!AlvoEstaVivo()) return;

        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;
        if (paraAlvo.magnitude > alcanceDoGolpe || Vector3.Angle(transform.forward, paraAlvo) > anguloDoGolpe * 0.5f)
            return;

        CausarDanoDeEspada(dano);
    }

    // Se o jogador estiver invencível, a Vida ignora o dano e avisa "desviou" (feedback de esquiva);
    // nesse caso não toca o som de impacto
    private void CausarDanoDeEspada(float dano)
    {
        Vector3 direcao = DirecaoPlanaAteAlvo();
        bool desviou = alvoDanificavel is Vida vidaDoAlvo && vidaDoAlvo.EstaInvencivel();
        alvoDanificavel.ReceberDano(dano, CentroDoAlvo, direcao);
        if (!desviou) TocarSom(somDaEspadaAcertando);
    }

    // =====================================================================
    // Acerto pela lâmina
    // =====================================================================

    private void PrepararEspada()
    {
        Transform espada = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == nomeDaEspada) { espada = t; break; }

        if (espada == null)
        {
            if (acertoPelaLamina)
                Debug.LogWarning($"ChefeMecha: espada '{nomeDaEspada}' não encontrada; os golpes usam distância + cone.", this);
            return;
        }

        partesDaEspada = espada.GetComponentsInChildren<Renderer>(true);
        malhaTemporaria = new Mesh();
    }

    // Dentro da janela do golpe, confere se algum ponto da espada (ou o caminho que ele fez desde
    // o quadro anterior) encosta no corpo do jogador. Acerta no máximo uma vez por golpe.
    private void VerificarLamina(Etapa etapa, float progresso)
    {
        // A janela vai de um pouco antes do pico da espada até o fim medido do corte deste golpe
        float ponto = etapa.pontos[0];
        bool naJanela = progresso >= ponto - janelaAntesDoGolpe && progresso <= etapa.fimDaLamina;

        if (!naJanela || etapa.acertou || !AlvoEstaVivo())
        {
            pontosAnteriores.Clear();
            return;
        }

        ColetarPontosDaLamina(pontosDaLamina);
        bool encostou = LaminaEncostaNoJogador();
        if (mostrarLaminaNaCena) DesenharLamina(encostou);

        pontosAnteriores.Clear();
        pontosAnteriores.AddRange(pontosDaLamina);

        // Colado no robô a lâmina passa por cima/por trás do jogador: no pico do golpe, acerta mesmo assim
        if (!encostou && acertoDePerto && progresso >= ponto && JogadorColadoNaFrente())
            encostou = true;

        if (encostou)
        {
            etapa.acertou = true;
            CausarDanoDeEspada(etapa.dano);
        }
    }

    private bool JogadorColadoNaFrente()
    {
        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;
        return paraAlvo.magnitude <= distanciaDePerto &&
               Vector3.Angle(transform.forward, paraAlvo) <= anguloDePerto * 0.5f;
    }

    // Posição real (no mundo) de cada vértice das peças da espada neste quadro
    private void ColetarPontosDaLamina(List<Vector3> destino)
    {
        destino.Clear();
        foreach (Renderer parte in partesDaEspada)
        {
            if (parte == null || !parte.enabled) continue;

            Matrix4x4 paraOMundo;
            if (parte is SkinnedMeshRenderer pele)
            {
                pele.BakeMesh(malhaTemporaria, true); // já inclui a escala; o resto vem da posição/rotação
                malhaTemporaria.GetVertices(verticesLocais);
                paraOMundo = Matrix4x4.TRS(pele.transform.position, pele.transform.rotation, Vector3.one);
            }
            else if (parte.TryGetComponent(out MeshFilter filtro) && filtro.sharedMesh != null)
            {
                filtro.sharedMesh.GetVertices(verticesLocais);
                paraOMundo = parte.transform.localToWorldMatrix;
            }
            else continue;

            foreach (Vector3 v in verticesLocais)
                destino.Add(paraOMundo.MultiplyPoint3x4(v));
        }
    }

    // O corpo do jogador é tratado como uma cápsula vertical (do pé à cabeça) com a folga configurada
    private bool LaminaEncostaNoJogador()
    {
        Vector3 base_, topo;
        float raio;
        Collider colisor = ColisorDoAlvo;
        if (colisor != null && colisor.enabled)
        {
            Bounds b = colisor.bounds;
            raio = Mathf.Min(b.extents.x, b.extents.z);
            base_ = new Vector3(b.center.x, b.min.y + raio, b.center.z);
            topo = new Vector3(b.center.x, b.max.y - raio, b.center.z);
        }
        else
        {
            raio = 0.4f;
            base_ = alvo.position + Vector3.up * 0.4f;
            topo = alvo.position + Vector3.up * 1.4f;
        }
        raio += folgaDoAcerto;

        bool temAnterior = pontosAnteriores.Count == pontosDaLamina.Count;
        for (int i = 0; i < pontosDaLamina.Count; i++)
        {
            if (DistanciaAteSegmento(pontosDaLamina[i], base_, topo) <= raio) return true;

            if (!temAnterior) continue;
            // Varredura: pontos intermediários entre o quadro anterior e este (a espada é rápida)
            for (int passo = 1; passo <= 2; passo++)
            {
                Vector3 meio = Vector3.Lerp(pontosAnteriores[i], pontosDaLamina[i], passo / 3f);
                if (DistanciaAteSegmento(meio, base_, topo) <= raio) return true;
            }
        }
        return false;
    }

    private static float DistanciaAteSegmento(Vector3 ponto, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(ponto - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return Vector3.Distance(ponto, a + ab * t);
    }

    private void DesenharLamina(bool encostou)
    {
        Color cor = encostou ? Color.red : Color.green;
        for (int i = 1; i < pontosDaLamina.Count; i += 3)
            Debug.DrawLine(pontosDaLamina[i - 1], pontosDaLamina[i], cor, 0.5f);
    }

    // Antes do impacto o robô dá um passo na direção do jogador (só até o ponto de compromisso)
    private void AvancarNoGolpe(Etapa etapa, float progresso)
    {
        if (etapa.avanco <= 0f || etapa.pontos.Length == 0) return;

        float fimDoAvanco = Mathf.Max(etapa.pontos[0] * pontoDeCompromisso, 0.01f);
        float desejado = etapa.avanco * Mathf.Clamp01(progresso / fimDoAvanco);
        float passo = desejado - etapa.jaAvancou;
        etapa.jaAvancou = desejado;

        if (passo > 0f && DistanciaAteAlvo() > distanciaMinimaNoAvanco)
            agente.Move(DirecaoPlanaAteAlvo() * passo); // respeita as bordas do NavMesh
    }

    // O "whoosh" toca um pouco antes do impacto, para combinar com o movimento da espada
    private void TocarSomDaEspada(Etapa etapa, float progresso)
    {
        if (!etapa.comSomDeEspada || etapa.somTocado || etapa.pontos.Length == 0) return;

        if (progresso >= etapa.pontos[0] * 0.7f)
        {
            etapa.somTocado = true;
            TocarSorteado(sonsDaEspada);
        }
    }

    // Durante a preparação (antes de "lentoAte") a animação toca devagar; depois volta ao normal
    private void AjustarVelocidadeDaPreparacao(Etapa etapa, float progresso)
    {
        bool preparando = etapa.lentidao < 1f && progresso < etapa.lentoAte;
        animator.speed = preparando ? etapa.lentidao : 1f;
    }

    // =====================================================================
    // Ataques à distância (esferas retas: esquiva armada)
    // =====================================================================

    private void IniciarTiroDaMao()
    {
        etapas.Enqueue(new Etapa
        {
            estado = Animator.StringToHash(estadoTiroMao),
            pontos = pontosDosTirosDaMao,
            aoChegarNoPonto = _ =>
            {
                Disparar(mao, 0f, velocidadeDoTiroDaMao, danoDoTiroDaMao);
                TocarSom(somDoTiroDaMao);
            },
            fim = 0.85f
        });
        ComecarEtapas();
    }

    private void IniciarBarragem()
    {
        etapas.Enqueue(new Etapa
        {
            estado = Animator.StringToHash(estadoBarragem),
            pontos = pontosDasOndas,
            aoChegarNoPonto = DispararOnda,
            fim = 0.9f
        });
        ComecarEtapas();
    }

    // Leque de poucas esferas: os espaços entre elas aumentam com a distância.
    // Ondas seguidas saem deslocadas meio passo, para os espaços mudarem de lugar.
    private void DispararOnda(int indiceDaOnda)
    {
        TocarSom(somDaBarragem);

        Transform ombro = ombros.Length > 0 ? ombros[ombroDaVez++ % ombros.Length] : null;
        float passo = esferasPorOnda > 1 ? aberturaDoLeque / (esferasPorOnda - 1) : 0f;
        float deslocamento = (indiceDaOnda % 2 == 1) ? passo * 0.5f : 0f;

        for (int i = 0; i < esferasPorOnda; i++)
        {
            float angulo = -aberturaDoLeque * 0.5f + passo * i + deslocamento;
            Disparar(ombro, angulo, velocidadeDaBarragem, danoDaBarragem);
        }
    }

    // Esfera de energia saindo de um osso (ou do peito), mirando no jogador, girada "angulo" graus para o lado
    private void Disparar(Transform origem, float angulo, float velocidadeDaEsfera, float dano)
    {
        if (!AlvoEstaVivo()) return;

        Vector3 inicio = origem != null ? origem.position : transform.position + Vector3.up * 2.5f + transform.forward;
        Vector3 direcao = Quaternion.Euler(0f, angulo, 0f) * (CentroDoAlvo - inicio);
        ProjetilInimigo.Criar(inicio, direcao, velocidadeDaEsfera, dano, corDosTiros, tamanhoDosTiros, 6f, transform);
    }

    // =====================================================================
    // Máquina de etapas (anima e dispara eventos nos pontos certos da animação)
    // =====================================================================

    private void ComecarEtapas()
    {
        PararDeAndar();
        TrocarPara(Estado.Atacando);
        AvancarEtapa();
    }

    private void AvancarEtapa()
    {
        if (etapas.Count == 0)
        {
            etapaAtual = null;
            TerminarAtaque();
            return;
        }

        etapaAtual = etapas.Dequeue();
        pontosAnteriores.Clear();
        proximoPonto = 0;
        etapaComecou = false;
        tempoNoEstado = 0f;
        Tocar(etapaAtual.estado);
    }

    private void AtualizarEtapas()
    {
        if (etapaAtual == null) { TerminarAtaque(); return; }

        PararDeAndar();

        float progresso = ProgressoDoEstado(etapaAtual.estado, out bool noEstado);
        if (noEstado) etapaComecou = true;
        else if (etapaComecou) progresso = 1f;

        AjustarVelocidadeDaPreparacao(etapaAtual, progresso);
        if (etapaAtual.porLamina) VerificarLamina(etapaAtual, progresso);

        // Até o primeiro evento ele ainda acompanha e avança até o jogador; depois o golpe "se compromete"
        if (proximoPonto == 0)
        {
            bool aindaMirando = etapaAtual.pontos.Length == 0 || progresso < etapaAtual.pontos[0] * pontoDeCompromisso;
            if (aindaMirando) VirarParaAlvo();
            AvancarNoGolpe(etapaAtual, progresso);
        }
        TocarSomDaEspada(etapaAtual, progresso);

        while (proximoPonto < etapaAtual.pontos.Length && progresso >= etapaAtual.pontos[proximoPonto])
        {
            etapaAtual.aoChegarNoPonto?.Invoke(proximoPonto);
            proximoPonto++;
        }

        bool acabou = etapaComecou && progresso >= etapaAtual.fim;
        if (acabou || tempoNoEstado > 8f)
        {
            animator.speed = 1f;
            AvancarEtapa();
        }
    }

    private void TerminarAtaque()
    {
        proximoAtaquePermitido = Time.time + descansoEntreAtaques;
        TrocarPara(Estado.Perseguindo);
    }

    private void LimparAtaque()
    {
        etapas.Clear();
        etapaAtual = null;
        LigarJetpack(false);
        brilho.Desligar();
    }

    // =====================================================================
    // Investida (o especial)
    // =====================================================================

    // Aviso: parado, encarando, piscando laranja cada vez mais forte, com o som de "carregando"
    private void IniciarInvestida()
    {
        PararDeAndar();
        Tocar(estadoParado);
        TocarSom(somDoAviso);
        LigarJetpack(true);
        TrocarPara(Estado.Aviso);
    }

    private void AtualizarAviso()
    {
        PararDeAndar();
        VirarParaAlvo();

        float crescendo = Mathf.Clamp01(tempoNoEstado / Mathf.Max(tempoDeAvisoDaInvestida, 0.01f));
        brilho.Aplicar(Mathf.Lerp(0.3f, 1f, BrilhoDoCorpo.Piscar(tempoNoEstado, 5f)) * crescendo, corDoAviso);

        if (tempoNoEstado >= tempoDeAvisoDaInvestida)
            ComecarCorrida();
    }

    private void ComecarCorrida()
    {
        direcaoDaInvestida = DirecaoPlanaAteAlvo();
        distanciaParaPercorrer = DistanciaAteAlvo() + distanciaAlemDoAlvo;
        distanciaPercorrida = 0f;
        seguindoReto = false;
        investidaAcertou = false;

        Tocar(estadoInvestida);
        brilho.Aplicar(1f, corDoAviso);
        TrocarPara(Estado.Investindo);
    }

    private void AtualizarInvestindo(float deltaTime)
    {
        if (!seguindoReto && AlvoEstaVivo())
        {
            if (DistanciaAteAlvo() <= distanciaParaSeguirReto) seguindoReto = true;
            else direcaoDaInvestida = Vector3.RotateTowards(direcaoDaInvestida, DirecaoPlanaAteAlvo(),
                                                            correcaoDeRota * Mathf.Deg2Rad * deltaTime, 0f);
        }

        Vector3 passo = direcaoDaInvestida * (velocidadeDaInvestida * deltaTime);
        Vector3 antes = transform.position;
        agente.Move(passo); // respeita as bordas do NavMesh
        transform.rotation = Quaternion.LookRotation(direcaoDaInvestida);

        float andou = (transform.position - antes).magnitude;
        distanciaPercorrida += andou;

        TentarAcertarInvestida();

        bool bateu = andou < passo.magnitude * 0.2f && tempoNoEstado > 0.2f;
        // Acertou o jogador: o impacto freia a investida (ele não continua correndo por cima de você)
        if (investidaAcertou || distanciaPercorrida >= distanciaParaPercorrer || bateu || tempoNoEstado >= duracaoMaximaDaInvestida)
            FicarAtordoado();
    }

    // Acerta uma vez; se o jogador estiver invencível (mergulho), a Vida avisa "desviou" e ainda pode acertar depois
    private void TentarAcertarInvestida()
    {
        if (investidaAcertou || !AlvoEstaVivo() || DistanciaAteAlvo() > raioDaInvestida) return;

        bool desviou = alvoDanificavel is Vida vidaDoAlvo && vidaDoAlvo.EstaInvencivel();
        alvoDanificavel.ReceberDano(danoDaInvestida, CentroDoAlvo, direcaoDaInvestida);
        if (desviou) return;

        investidaAcertou = true;
        if (derrubaOJogador) DerrubarAlvo(direcaoDaInvestida);
    }

    // =====================================================================
    // Atordoado
    // =====================================================================

    private void FicarAtordoado()
    {
        LigarJetpack(false);
        Tocar(estadoAtordoado);
        TrocarPara(Estado.Atordoado);
    }

    // Ajoelhado, piscando azul: a hora de descarregar a arma
    private void AtualizarAtordoado()
    {
        PararDeAndar();
        brilho.Aplicar(Mathf.Lerp(0.25f, 0.7f, BrilhoDoCorpo.Piscar(tempoNoEstado, 3f)), corVulneravel);

        if (tempoNoEstado >= tempoAtordoado)
        {
            brilho.Desligar();
            proximoEspecialPermitido = Time.time + intervaloDeEspeciais;
            proximoAtaquePermitido = Time.time + descansoEntreAtaques;
            Tocar(estadoParado);
            TrocarPara(Estado.Perseguindo);
        }
    }

    // Tiros fora do atordoamento fazem um som de "armadura" (o dano é bem pequeno)
    private void AoReceberDano(float quantidade, Vector3 ponto, Vector3 direcao)
    {
        if (estado != Estado.Atordoado)
            TocarSom(somDeTiroBloqueado);
    }

    // =====================================================================
    // Animação, morte e utilidades
    // =====================================================================

    private void Tocar(string nomeDoEstado, float mistura = 0.12f) => Tocar(Animator.StringToHash(nomeDoEstado), mistura);

    private void Tocar(int hash, float mistura = 0.12f)
    {
        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(hash, mistura, 0, 0f);
    }

    // Parado, andando ou correndo (a corrida usa o mesmo estado/clip da investida: Mech_Run)
    protected override void AtualizarAnimacaoDeMovimento()
    {
        if (estado != Estado.Perseguindo && estado != Estado.Esperando) return;

        float rapidez = agente.enabled ? agente.velocity.magnitude : 0f;
        string nome = rapidez > velocidade * 1.4f ? estadoInvestida
                    : rapidez > 0.2f ? estadoAndar
                    : estadoParado;

        int desejado = Animator.StringToHash(nome);
        if (desejado == locomocaoAtual) return;

        locomocaoAtual = desejado;
        Tocar(desejado, 0.2f);
    }

    protected override void TocarAnimacaoDeMorte()
    {
        LimparAtaque();
        animator.speed = 1f;
        Tocar(estadoMorte, 0.15f);
    }

    private bool EstaDeFrenteParaAlvo(float tolerancia)
    {
        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;
        return Vector3.Angle(transform.forward, paraAlvo) < tolerancia;
    }

    private Vector3 DirecaoPlanaAteAlvo()
    {
        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        return direcao.sqrMagnitude > 0.001f ? direcao.normalized : transform.forward;
    }

    private Transform AcharOsso(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;

        Debug.LogWarning($"ChefeMecha: osso '{nome}' não encontrado. Os tiros vão sair do peito.", this);
        return null;
    }

    // O Mech é feito de várias peças, e as "caixas" que o Unity usa para saber se cada peça está na tela
    // foram calculadas na pose parada. Animado, uma peça sai da própria caixa e o Unity deixa de desenhá-la
    // (as peças "sumiam" de perto). Com isto, as caixas acompanham a animação.
    private void ManterPecasSempreVisiveis()
    {
        foreach (SkinnedMeshRenderer peca in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            peca.updateWhenOffscreen = true;
    }

    private void CriarFogoDoJetpack()
    {
        if (fogoDoJetpack == null) return;

        fogo = Instantiate(fogoDoJetpack, jetpack != null ? jetpack : transform);
        fogo.transform.localPosition = Vector3.zero;
        fogo.transform.localRotation = Quaternion.Euler(rotacaoDoFogo);
        fogo.SetActive(false);
    }

    // Fogo e som do jetpack ligam e desligam juntos
    private void LigarJetpack(bool ligar)
    {
        if (fogo != null) fogo.SetActive(ligar);
        if (somDoJetpack == null) return;

        if (fonteDoJetpack == null) fonteDoJetpack = CriarFonte(loop: true);
        if (ligar && !fonteDoJetpack.isPlaying)
        {
            fonteDoJetpack.clip = somDoJetpack;
            fonteDoJetpack.volume = volumeDosAtaques;
            fonteDoJetpack.Play();
        }
        else if (!ligar && fonteDoJetpack.isPlaying)
        {
            fonteDoJetpack.Stop();
        }
    }

    private void TocarSom(AudioClip som)
    {
        if (som == null) return;
        if (fonte == null) fonte = CriarFonte(loop: false);

        fonte.pitch = 1f + UnityEngine.Random.Range(-variacaoDeTom, variacaoDeTom);
        fonte.PlayOneShot(som, volumeDosAtaques);
    }

    private void TocarSorteado(AudioClip[] sons)
    {
        if (sons == null || sons.Length == 0) return;
        TocarSom(sons[UnityEngine.Random.Range(0, sons.Length)]);
    }

    // Som 3D (mais baixo de longe), mas não totalmente: o chefe é sempre "presente"
    private AudioSource CriarFonte(bool loop)
    {
        AudioSource nova = gameObject.AddComponent<AudioSource>();
        nova.playOnAwake = false;
        nova.loop = loop;
        nova.spatialBlend = 0.8f;
        nova.minDistance = 5f;
        return nova;
    }

    private IEnumerable<string> NomesDosEstados()
    {
        return new[]
        {
            estadoParado, estadoAndar, estadoCorteRapido, estadoEstocada, estadoCortePesado,
            estadoTiroMao, estadoBarragem, estadoInvestida, estadoAtordoado, estadoMorte
        };
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, distanciaParaGolpear);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceDoGolpe);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, distanciaParaAtirar);
    }
}
