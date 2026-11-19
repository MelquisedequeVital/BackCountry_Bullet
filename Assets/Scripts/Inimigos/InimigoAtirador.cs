using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA do inimigo que arremessa esferas de energia (robô roxo):
///   Ocioso → (vê o jogador) → Posicionando → (na distância certa e virado) → Arremessando → Posicionando...
/// - Posicionando: mantém distância. Se o jogador está longe, chega perto; se está perto demais, recua.
/// - Arremessando: a esfera aparece na mão e cresce enquanto o braço vai para trás (o aviso),
///   e é solta no "Ponto Do Arremesso" da animação, na direção em que o jogador está naquele instante.
///   O tempo segue a própria animação: mudar a velocidade do estado no Animator não dessincroniza nada.
/// A esfera é lenta e vai em linha reta: andar de lado (mesmo armado) ou esquivar desvia dela.
/// Parâmetros do Animator: Velocidade (Float), Atacar (Trigger) e Morrer (Trigger).
/// </summary>
public class InimigoAtirador : InimigoBase
{
    private enum Estado { Ocioso, Posicionando, Arremessando }

    [Header("Distância do jogador")]
    [Tooltip("Se o jogador chegar mais perto que isso, o robô recua")]
    [Min(0f)]
    public float distanciaMinima = 7f;

    [Tooltip("Se o jogador estiver mais longe que isso, o robô se aproxima")]
    [Min(0f)]
    public float distanciaMaxima = 12f;

    [Header("Arremesso (sincronizado com a animação)")]
    [Tooltip("Nome exato do estado de arremesso no Animator")]
    public string estadoDeArremesso = "Arremesso";

    [Tooltip("Em que ponto da animação (0 = início, 1 = fim) a esfera é solta. " +
             "No 'toss grenade' o braço vem para a frente em cerca de 0.6.")]
    [Range(0f, 1f)]
    public float pontoDoArremesso = 0.6f;

    [Tooltip("Em que ponto da animação o robô volta a se mover")]
    [Range(0f, 1f)]
    public float pontoDeFim = 0.9f;

    [Tooltip("Descanso entre um arremesso e outro (segundos)")]
    [Min(0f)]
    public float tempoEntreArremessos = 1.5f;

    [Header("Esfera de energia")]
    public Color corDaEsfera = new Color(0.7f, 0.2f, 1f);

    [Tooltip("Diâmetro da esfera em metros")]
    [Range(0.1f, 1f)]
    public float tamanhoDaEsfera = 0.35f;

    [Tooltip("Velocidade da esfera (m/s). Lenta = mais fácil de desviar")]
    [Min(0.5f)]
    public float velocidadeDaEsfera = 10f;

    [Tooltip("Dano de cada esfera")]
    [Min(0f)]
    public float dano = 10f;

    [Tooltip("Segundos até a esfera sumir se não acertar nada")]
    [Min(0.5f)]
    public float tempoDeVidaDaEsfera = 5f;

    [Tooltip("Ajuste fino da mira em metros: a esfera vai no centro do corpo do jogador mais este valor " +
             "(positivo = mais alto, negativo = mais baixo)")]
    [Range(-1f, 1f)]
    public float ajusteDaMira = 0f;

    // Segurança: se o estado do Animator nunca for encontrado, o arremesso termina depois disso
    private const float TempoMaximoDoArremesso = 5f;

    private Estado estado = Estado.Ocioso;
    private float tempoNoEstado;
    private float proximoArremessoPermitido;
    private bool esferaJaSolta;
    private bool animacaoComecou;
    private int hashDoEstadoDeArremesso;

    private Transform mao;
    private GameObject esferaNaMao;

    protected override void Awake()
    {
        base.Awake();
        agente.stoppingDistance = 0.5f;
        mao = animator.GetBoneTransform(HumanBodyBones.RightHand);
        hashDoEstadoDeArremesso = Animator.StringToHash(estadoDeArremesso);

        if (!animator.HasState(0, hashDoEstadoDeArremesso))
            Debug.LogWarning($"InimigoAtirador: estado '{estadoDeArremesso}' não encontrado na Base Layer do Animator.", this);

        if (mao == null)
            Debug.LogWarning("InimigoAtirador: mão direita não encontrada (o robô precisa ser Humanoid). " +
                             "A esfera vai sair do peito.", this);
    }

    protected override string[] ParametrosNecessarios() => new[] { "Velocidade", "Atacar", "Morrer" };

    protected override void AtualizarIA(float deltaTime)
    {
        tempoNoEstado += deltaTime;

        if (!AlvoEstaVivo() && estado != Estado.Ocioso)
            TrocarPara(Estado.Ocioso);

        switch (estado)
        {
            case Estado.Ocioso: AtualizarOcioso(); break;
            case Estado.Posicionando: AtualizarPosicionando(); break;
            case Estado.Arremessando: AtualizarArremessando(); break;
        }
    }

    protected override void Morrer()
    {
        ApagarEsferaNaMao();
        base.Morrer();
    }

    // ---------- Estados ----------

    private void AtualizarOcioso()
    {
        PararDeAndar();

        if (AlvoEstaVivo() && DistanciaAteAlvo() <= distanciaDeDeteccao)
            TrocarPara(Estado.Posicionando);
    }

    private void AtualizarPosicionando()
    {
        float distancia = DistanciaAteAlvo();

        if (distancia > distanciaMaxima)
        {
            AndarAte(alvo.position);
        }
        else if (distancia < distanciaMinima)
        {
            AndarAte(PontoParaRecuar());
        }
        else
        {
            PararDeAndar();
            VirarParaAlvo();

            if (Time.time >= proximoArremessoPermitido && EstaOlhandoParaAlvo())
                IniciarArremesso();
        }
    }

    // A esfera cresce na mão enquanto o braço vai para trás, e é solta quando a ANIMAÇÃO
    // chega no ponto do arremesso (e não depois de um tempo fixo)
    private void AtualizarArremessando()
    {
        PararDeAndar();

        float progresso = ProgressoDaAnimacao(out bool estaNoEstado);
        if (estaNoEstado) animacaoComecou = true;

        if (!esferaJaSolta)
        {
            VirarParaAlvo();
            CrescerEsferaNaMao(progresso / Mathf.Max(pontoDoArremesso, 0.01f));

            if (progresso >= pontoDoArremesso)
                SoltarEsfera();
        }

        bool animacaoAcabou = animacaoComecou && (!estaNoEstado || progresso >= pontoDeFim);
        if (animacaoAcabou || tempoNoEstado >= TempoMaximoDoArremesso)
        {
            if (!esferaJaSolta) SoltarEsfera(); // segurança: nunca termina com a esfera presa na mão
            proximoArremessoPermitido = Time.time + tempoEntreArremessos;
            TrocarPara(Estado.Posicionando);
        }
    }

    // Quanto da animação de arremesso já tocou (0 a 1). Enquanto ela ainda não começou, devolve 0.
    private float ProgressoDaAnimacao(out bool estaNoEstado)
    {
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        estaNoEstado = info.shortNameHash == hashDoEstadoDeArremesso;
        return estaNoEstado ? Mathf.Clamp01(info.normalizedTime) : (animacaoComecou ? 1f : 0f);
    }

    private void TrocarPara(Estado novoEstado)
    {
        if (estado == novoEstado) return;

        if (estado == Estado.Arremessando)
            ApagarEsferaNaMao(); // interrompido no meio: a esfera não fica presa na mão

        estado = novoEstado;
        tempoNoEstado = 0f;
    }

    // ---------- Arremesso ----------

    private void IniciarArremesso()
    {
        animator.SetTrigger(ParamAtacar);
        esferaJaSolta = false;
        animacaoComecou = false;
        CriarEsferaNaMao();
        TrocarPara(Estado.Arremessando);
    }

    private void CriarEsferaNaMao()
    {
        esferaNaMao = ProjetilInimigo.CriarVisual(corDaEsfera, tamanhoDaEsfera, comRastro: false);
        esferaNaMao.transform.SetParent(mao != null ? mao : transform, false);
        esferaNaMao.transform.localPosition = mao != null ? Vector3.zero : new Vector3(0f, 1.4f, 0.4f);
        esferaNaMao.transform.localScale = Vector3.zero;
    }

    // Cresce de 0 até o tamanho final (o tamanho é corrigido pela escala da mão)
    private void CrescerEsferaNaMao(float progresso)
    {
        if (esferaNaMao == null) return;

        float escalaDoPai = esferaNaMao.transform.parent.lossyScale.x;
        float tamanho = tamanhoDaEsfera * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progresso)) / Mathf.Max(escalaDoPai, 0.0001f);
        esferaNaMao.transform.localScale = Vector3.one * tamanho;
    }

    private void SoltarEsfera()
    {
        esferaJaSolta = true;
        Vector3 origem = esferaNaMao != null ? esferaNaMao.transform.position : transform.position + Vector3.up * 1.4f;
        ApagarEsferaNaMao();

        if (!AlvoEstaVivo()) return;

        Vector3 mira = CentroDoAlvo + Vector3.up * ajusteDaMira;
        ProjetilInimigo.Criar(origem, mira - origem, velocidadeDaEsfera, dano,
                              corDaEsfera, tamanhoDaEsfera, tempoDeVidaDaEsfera, transform);
    }

    private void ApagarEsferaNaMao()
    {
        if (esferaNaMao != null)
            Destroy(esferaNaMao);
        esferaNaMao = null;
    }

    // ---------- Movimento ----------

    private bool EstaOlhandoParaAlvo()
    {
        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;
        return Vector3.Angle(transform.forward, paraAlvo) < 15f;
    }

    // Um ponto alguns metros para longe do jogador, que esteja dentro do NavMesh
    private Vector3 PontoParaRecuar()
    {
        Vector3 paraLonge = transform.position - alvo.position;
        paraLonge.y = 0f;
        Vector3 desejado = transform.position + paraLonge.normalized * 3f;

        return NavMesh.SamplePosition(desejado, out NavMeshHit ponto, 3f, NavMesh.AllAreas)
            ? ponto.position
            : transform.position;
    }

    // Desenha as distâncias na aba Scene quando o robô está selecionado
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(0.7f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, distanciaMinima);
        Gizmos.DrawWireSphere(transform.position, distanciaMaxima);
    }
}
