using UnityEngine;

/// <summary>
/// IA do robô grande (laranja). Ensina a guardar a arma para esquivar.
///   Ocioso → Perseguindo ─(perto)─▶ Golpeando (Soco rápido ou Garra pesada) ─▶ Perseguindo
///                       └(investida pronta e jogador a média distância)─▶ AvisoDeInvestida ─▶ Investindo ─▶ Cansado ─▶ Perseguindo
/// - AvisoDeInvestida: para, ruge e o corpo brilha (o sinal para o jogador guardar a arma).
/// - Investindo: corre muito rápido, corrigindo a rota até chegar perto. Acerta numa área larga,
///   então andar de lado ou a esquiva armada não escapam: só o mergulho desarmado (invencível) atravessa.
/// - Cansado: depois da investida fica parado e vulnerável por um tempo (a hora de sacar a arma e atirar).
/// - Armadura: fora do "Cansado" leva só uma fração do dano; cansado, leva dano extra (o ponto fraco).
/// Os golpes acompanham a animação (como no robô roxo): mudar a velocidade dos estados não dessincroniza.
/// Animator: Velocidade (Float), Soco (Trigger), Garra (Trigger), Rugir (Trigger), Investindo (Bool), Morrer (Trigger).
/// </summary>
public class InimigoBrutamontes : InimigoBase, IModificadorDeDano
{
    [Header("Armadura e ponto fraco")]
    [Tooltip("Fração do dano recebida normalmente (0.15 = só 15%: atirar a qualquer hora quase não adianta)")]
    [Range(0f, 1f)]
    public float danoComArmadura = 0.15f;

    [Tooltip("Multiplicador do dano enquanto está cansado depois da investida (1.5 = 50% a mais)")]
    [Min(0f)]
    public float danoQuandoCansado = 1.5f;

    [Tooltip("Cor que pisca enquanto ele está vulnerável (cansado)")]
    public Color corVulneravel = new Color(0.3f, 0.8f, 1f);

    private enum Estado { Ocioso, Perseguindo, Golpeando, AvisoDeInvestida, Investindo, Cansado }

    private enum Golpe { Soco, Garra }

    [Header("Corpo a corpo")]
    [Tooltip("Distância em que o robô para e começa um golpe")]
    [Min(0.1f)]
    public float distanciaParaAtacar = 2.4f;

    [Tooltip("Até onde os golpes alcançam (um pouco mais que a distância para atacar)")]
    [Min(0.1f)]
    public float alcanceDoGolpe = 3f;

    [Tooltip("Largura dos golpes à frente do robô (graus)")]
    [Range(10f, 180f)]
    public float anguloDoGolpe = 100f;

    [Tooltip("Descanso entre um golpe e outro (segundos)")]
    [Min(0f)]
    public float tempoEntreGolpes = 0.8f;

    [Tooltip("A cada quantos socos rápidos vem uma garra pesada")]
    [Min(1)]
    public int socosAntesDaGarra = 2;

    [Header("Soco rápido")]
    public string estadoDoSoco = "Soco";
    [Min(0f)] public float danoDoSoco = 15f;
    [Tooltip("Ponto da animação (0 a 1) em que o soco acerta. No 'mutant punch' é cerca de 0.25")]
    [Range(0f, 1f)] public float pontoDoSoco = 0.25f;

    [Header("Garra pesada")]
    public string estadoDaGarra = "Garra";
    [Min(0f)] public float danoDaGarra = 25f;
    [Tooltip("Ponto da animação (0 a 1) em que a garra acerta. No 'mutant swiping' é cerca de 0.54")]
    [Range(0f, 1f)] public float pontoDaGarra = 0.54f;

    [Tooltip("Ponto da animação em que os golpes terminam e o robô volta a andar (igual ao Exit Time no Animator)")]
    [Range(0f, 1f)] public float pontoDeFimDoGolpe = 0.9f;

    [Header("Investida")]
    [Tooltip("Tempo entre uma investida e outra (segundos)")]
    [Min(0f)] public float intervaloEntreInvestidas = 4.5f;

    [Tooltip("Segundos depois de te ver até a primeira investida")]
    [Min(0f)] public float atrasoDaPrimeiraInvestida = 2f;

    [Tooltip("A investida só começa com o jogador a pelo menos esta distância")]
    [Min(0f)] public float distanciaMinimaDaInvestida = 3.5f;

    [Tooltip("...e no máximo esta distância (alto = se você fugir para longe, ele vem correndo)")]
    [Min(0f)] public float distanciaMaximaDaInvestida = 60f;

    [Tooltip("Chance (0 a 1) de emendar uma investida logo depois da garra, mesmo de perto")]
    [Range(0f, 1f)] public float chanceDeInvestidaAposGarra = 0.5f;

    [Tooltip("Duração do aviso (rugido + brilho) antes de correr")]
    [Min(0f)] public float tempoDeAviso = 1.2f;

    [Tooltip("Velocidade da corrida (m/s)")]
    [Min(1f)] public float velocidadeDaInvestida = 9f;

    [Tooltip("Quanto o robô consegue corrigir a direção durante a corrida (graus por segundo)")]
    [Min(0f)] public float correcaoDeRota = 120f;

    [Tooltip("Abaixo desta distância ele para de corrigir a rota e segue reto (dá a chance do mergulho)")]
    [Min(0f)] public float distanciaParaSeguirReto = 3f;

    [Tooltip("Raio da área atingida pela investida (largo: a esquiva armada não escapa)")]
    [Min(0.1f)] public float raioDaInvestida = 1.6f;

    [Min(0f)] public float danoDaInvestida = 35f;

    [Tooltip("Quantos metros ele continua correndo depois de passar por onde o jogador estava")]
    [Min(0f)] public float distanciaAlemDoAlvo = 4f;

    [Tooltip("Tempo máximo correndo (segurança)")]
    [Min(0.1f)] public float duracaoMaximaDaInvestida = 2.5f;

    [Tooltip("A investida derruba o jogador (empurrão + queda + levantar)")]
    public bool derrubaOJogador = true;

    [Tooltip("Tempo parado e vulnerável depois da investida (dá para sacar a arma e dar uns 5 tiros)")]
    [Min(0f)] public float tempoCansado = 2.5f;

    [Header("Brilho de aviso")]
    public Color corDoBrilho = new Color(1f, 0.35f, 0.05f);
    [Range(0f, 1f)] public float forcaDoBrilho = 0.75f;
    [Tooltip("Piscadas por segundo durante o aviso")]
    [Min(0.1f)] public float velocidadeDoBrilho = 4f;

    private static readonly int ParamSoco = Animator.StringToHash("Soco");
    private static readonly int ParamGarra = Animator.StringToHash("Garra");
    private static readonly int ParamRugir = Animator.StringToHash("Rugir");
    private static readonly int ParamInvestindo = Animator.StringToHash("Investindo");

    private Estado estado = Estado.Ocioso;
    private float tempoNoEstado;

    // Golpes
    private Golpe golpeAtual;
    private int socosSeguidos;
    private bool golpeJaAplicado;
    private bool animacaoDoGolpeComecou;
    private float proximoGolpePermitido;
    private int hashDoSoco;
    private int hashDaGarra;

    // Investida
    private float proximaInvestidaPermitida;
    private Vector3 direcaoDaInvestida;
    private float distanciaParaPercorrer;
    private float distanciaPercorrida;
    private bool seguindoReto;
    private bool investidaAcertou;

    // Brilho
    private BrilhoDoCorpo brilho;

    // ---------- Ciclo de vida ----------

    protected override void Awake()
    {
        base.Awake();
        agente.stoppingDistance = distanciaParaAtacar * 0.9f;

        hashDoSoco = Animator.StringToHash(estadoDoSoco);
        hashDaGarra = Animator.StringToHash(estadoDaGarra);
        AvisarSeFaltarEstado(estadoDoSoco);
        AvisarSeFaltarEstado(estadoDaGarra);

        PrepararBrilho();
    }

    /// <summary>Armadura: pouco dano normalmente; dano extra quando está cansado (ponto fraco).</summary>
    public float MultiplicadorDeDano => estado == Estado.Cansado ? danoQuandoCansado : danoComArmadura;

    protected override string[] ParametrosNecessarios() =>
        new[] { "Velocidade", "Soco", "Garra", "Rugir", "Investindo", "Morrer" };

    protected override void Morrer()
    {
        animator.SetBool(ParamInvestindo, false);
        AplicarBrilho(0f);
        base.Morrer();
    }

    protected override void AtualizarIA(float deltaTime)
    {
        tempoNoEstado += deltaTime;

        if (!AlvoEstaVivo() && estado != Estado.Ocioso)
            TrocarPara(Estado.Ocioso);

        switch (estado)
        {
            case Estado.Ocioso: AtualizarOcioso(); break;
            case Estado.Perseguindo: AtualizarPerseguindo(); break;
            case Estado.Golpeando: AtualizarGolpeando(); break;
            case Estado.AvisoDeInvestida: AtualizarAvisoDeInvestida(); break;
            case Estado.Investindo: AtualizarInvestindo(deltaTime); break;
            case Estado.Cansado: AtualizarCansado(); break;
        }
    }

    private void TrocarPara(Estado novoEstado)
    {
        if (estado == novoEstado) return;

        // Ao sair da investida (por qualquer motivo) desliga a corrida e o brilho
        if (estado == Estado.Investindo || estado == Estado.AvisoDeInvestida)
            animator.SetBool(ParamInvestindo, false);

        if (estado == Estado.Investindo || estado == Estado.AvisoDeInvestida || estado == Estado.Cansado)
            AplicarBrilho(0f);

        estado = novoEstado;
        tempoNoEstado = 0f;
    }

    // ---------- Estados ----------

    private void AtualizarOcioso()
    {
        PararDeAndar();

        if (AlvoEstaVivo() && DistanciaAteAlvo() <= distanciaDeDeteccao)
        {
            proximaInvestidaPermitida = Time.time + atrasoDaPrimeiraInvestida;
            TrocarPara(Estado.Perseguindo);
        }
    }

    private void AtualizarPerseguindo()
    {
        float distancia = DistanciaAteAlvo();

        if (PodeInvestir(distancia))
        {
            IniciarAvisoDeInvestida();
            return;
        }

        if (distancia <= distanciaParaAtacar)
        {
            PararDeAndar();
            VirarParaAlvo();

            if (Time.time >= proximoGolpePermitido)
                IniciarGolpe();
        }
        else
        {
            AndarAte(alvo.position);
        }
    }

    // ---------- Golpes (soco e garra) ----------

    // Alterna: alguns socos rápidos, depois uma garra pesada
    private void IniciarGolpe()
    {
        golpeAtual = socosSeguidos >= socosAntesDaGarra ? Golpe.Garra : Golpe.Soco;
        socosSeguidos = golpeAtual == Golpe.Garra ? 0 : socosSeguidos + 1;

        animator.SetTrigger(golpeAtual == Golpe.Soco ? ParamSoco : ParamGarra);
        golpeJaAplicado = false;
        animacaoDoGolpeComecou = false;
        TrocarPara(Estado.Golpeando);
    }

    private void AtualizarGolpeando()
    {
        PararDeAndar();

        int hash = golpeAtual == Golpe.Soco ? hashDoSoco : hashDaGarra;
        float progresso = ProgressoDoEstado(hash, out bool estaNoEstado);
        if (estaNoEstado) animacaoDoGolpeComecou = true;
        else if (animacaoDoGolpeComecou) progresso = 1f;

        // Antes do impacto o robô ainda acompanha o jogador; depois, o golpe "se compromete"
        float pontoDoImpacto = golpeAtual == Golpe.Soco ? pontoDoSoco : pontoDaGarra;
        if (!golpeJaAplicado)
        {
            VirarParaAlvo();

            if (progresso >= pontoDoImpacto)
            {
                AplicarGolpe(golpeAtual == Golpe.Soco ? danoDoSoco : danoDaGarra);
                golpeJaAplicado = true;
            }
        }

        bool terminou = animacaoDoGolpeComecou && (!estaNoEstado || progresso >= pontoDeFimDoGolpe);
        if (terminou || tempoNoEstado > 5f)
        {
            proximoGolpePermitido = Time.time + tempoEntreGolpes;

            // Depois da garra às vezes emenda a investida: pega quem fica colado nele
            if (golpeAtual == Golpe.Garra && Random.value < chanceDeInvestidaAposGarra)
                IniciarAvisoDeInvestida();
            else
                TrocarPara(Estado.Perseguindo);
        }
    }

    private void AplicarGolpe(float dano)
    {
        if (!AlvoEstaVivo()) return;

        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;

        bool pertoOBastante = paraAlvo.magnitude <= alcanceDoGolpe;
        bool naFrente = Vector3.Angle(transform.forward, paraAlvo) <= anguloDoGolpe * 0.5f;

        if (pertoOBastante && naFrente)
            alvoDanificavel.ReceberDano(dano, CentroDoAlvo, paraAlvo.normalized);
    }

    // ---------- Investida ----------

    private bool PodeInvestir(float distancia)
    {
        return Time.time >= proximaInvestidaPermitida
            && distancia >= distanciaMinimaDaInvestida
            && distancia <= distanciaMaximaDaInvestida;
    }

    // O aviso: parado, encarando, rugindo e piscando o brilho
    private void IniciarAvisoDeInvestida()
    {
        animator.SetTrigger(ParamRugir);
        TrocarPara(Estado.AvisoDeInvestida);
    }

    private void AtualizarAvisoDeInvestida()
    {
        PararDeAndar();
        VirarParaAlvo();

        float pisca = (Mathf.Sin(tempoNoEstado * velocidadeDoBrilho * Mathf.PI * 2f) + 1f) * 0.5f;
        float crescendo = Mathf.Clamp01(tempoNoEstado / Mathf.Max(tempoDeAviso, 0.01f));
        AplicarBrilho(Mathf.Lerp(0.3f, 1f, pisca) * crescendo);

        if (tempoNoEstado >= tempoDeAviso)
            ComecarCorrida();
    }

    private void ComecarCorrida()
    {
        direcaoDaInvestida = DirecaoPlanaAteAlvo();
        distanciaParaPercorrer = DistanciaAteAlvo() + distanciaAlemDoAlvo;
        distanciaPercorrida = 0f;
        seguindoReto = false;
        investidaAcertou = false;

        PararDeAndar(); // o NavMeshAgent não planeja rota: a corrida é controlada aqui

        // Primeiro troca de estado (isso desliga o aviso), depois liga a corrida e o brilho máximo
        TrocarPara(Estado.Investindo);
        animator.SetBool(ParamInvestindo, true);
        AplicarBrilho(1f);
    }

    private void AtualizarInvestindo(float deltaTime)
    {
        CorrigirRota(deltaTime);

        Vector3 passo = direcaoDaInvestida * (velocidadeDaInvestida * deltaTime);
        Vector3 antes = transform.position;

        // agente.Move respeita as bordas do NavMesh (não atravessa paredes nem sai do mapa)
        agente.Move(passo);
        transform.rotation = Quaternion.LookRotation(direcaoDaInvestida);

        float andou = (transform.position - antes).magnitude;
        distanciaPercorrida += andou;

        TentarAcertarInvestida();

        bool chegouAoFim = distanciaPercorrida >= distanciaParaPercorrer;
        bool bateuEmAlgo = andou < passo.magnitude * 0.2f && tempoNoEstado > 0.2f;
        // Acertou o jogador: o impacto freia a investida (ele não continua correndo por cima de você)
        if (investidaAcertou || chegouAoFim || bateuEmAlgo || tempoNoEstado >= duracaoMaximaDaInvestida)
            TrocarPara(Estado.Cansado);
    }

    // Persegue o jogador até ficar perto; daí segue reto (é aí que o mergulho funciona)
    private void CorrigirRota(float deltaTime)
    {
        if (seguindoReto || !AlvoEstaVivo()) return;

        if (DistanciaAteAlvo() <= distanciaParaSeguirReto)
        {
            seguindoReto = true;
            return;
        }

        Vector3 desejada = DirecaoPlanaAteAlvo();
        direcaoDaInvestida = Vector3.RotateTowards(direcaoDaInvestida, desejada,
                                                   correcaoDeRota * Mathf.Deg2Rad * deltaTime, 0f);
    }

    // Acerta uma vez; se o jogador estiver invencível (mergulho), não conta e ele passa por dentro
    private void TentarAcertarInvestida()
    {
        if (investidaAcertou || !AlvoEstaVivo() || DistanciaAteAlvo() > raioDaInvestida) return;

        // Invencível: a Vida ignora o dano e avisa "desviou" (feedback); a investida ainda pode acertar depois
        bool desviou = alvoDanificavel is Vida vidaDoAlvo && vidaDoAlvo.EstaInvencivel();
        alvoDanificavel.ReceberDano(danoDaInvestida, CentroDoAlvo, direcaoDaInvestida);
        if (desviou) return;

        investidaAcertou = true;
        if (derrubaOJogador) DerrubarAlvo(direcaoDaInvestida);
    }

    // Depois da investida: parado e vulnerável, piscando na cor de "ponto fraco"
    private void AtualizarCansado()
    {
        PararDeAndar();

        float pisca = (Mathf.Sin(tempoNoEstado * velocidadeDoBrilho * Mathf.PI * 2f) + 1f) * 0.5f;
        AplicarBrilho(Mathf.Lerp(0.25f, 0.7f, pisca), corVulneravel);

        if (tempoNoEstado >= tempoCansado)
        {
            proximaInvestidaPermitida = Time.time + intervaloEntreInvestidas;
            AplicarBrilho(0f);
            TrocarPara(Estado.Perseguindo);
        }
    }

    private Vector3 DirecaoPlanaAteAlvo()
    {
        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        return direcao.sqrMagnitude > 0.001f ? direcao.normalized : transform.forward;
    }

    // ---------- Brilho de aviso ----------

    private void PrepararBrilho()
    {
        brilho = new BrilhoDoCorpo(transform, new Vector3(0f, 1.5f, 0.6f));
    }

    // 0 = cores normais, 1 = brilho máximo (na cor do aviso, se nenhuma for informada)
    private void AplicarBrilho(float intensidade) => AplicarBrilho(intensidade, corDoBrilho);

    private void AplicarBrilho(float intensidade, Color corDoEfeito)
    {
        brilho?.Aplicar(intensidade, corDoEfeito, forcaDoBrilho);
    }

    // ---------- Ajuda ----------

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, distanciaParaAtacar);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceDoGolpe);
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, distanciaMaximaDaInvestida);
    }
}
