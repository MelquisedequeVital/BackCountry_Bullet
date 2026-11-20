using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tudo que os inimigos têm em comum: encontrar o jogador, andar pelo NavMesh, virar para ele,
/// mandar a velocidade para o Animator e morrer quando a Vida zera.
/// Cada tipo de inimigo herda desta classe e só escreve o próprio comportamento em AtualizarIA().
/// Parâmetros que o Animator precisa ter: Velocidade (Float) e Morrer (Trigger),
/// mais os que cada inimigo pedir (ex.: Atacar).
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(Vida))]
public abstract class InimigoBase : MonoBehaviour
{
    [Header("Percepção")]
    [Tooltip("A que distância (metros) o robô percebe o jogador")]
    [Min(0f)]
    public float distanciaDeDeteccao = 15f;

    [Header("Movimento")]
    [Tooltip("Velocidade de caminhada (m/s)")]
    [Min(0.1f)]
    public float velocidade = 1.3f;

    [Tooltip("Rapidez com que o robô vira para o jogador (graus por segundo)")]
    [Min(10f)]
    public float velocidadeDeGiro = 360f;

    protected static readonly int ParamVelocidade = Animator.StringToHash("Velocidade");
    protected static readonly int ParamAtacar = Animator.StringToHash("Atacar");
    protected static readonly int ParamMorrer = Animator.StringToHash("Morrer");

    protected NavMeshAgent agente;
    protected Animator animator;
    protected Vida vida;

    protected Transform alvo;
    protected IDanificavel alvoDanificavel;
    private Collider colisorDoAlvo;

    /// <summary>Colisor do jogador (pode ser nulo). Útil para projéteis teleguiados mirarem no corpo.</summary>
    protected Collider ColisorDoAlvo => colisorDoAlvo;

    /// <summary>Centro do corpo do jogador (meio do colisor). Use para mirar.</summary>
    protected Vector3 CentroDoAlvo =>
        colisorDoAlvo != null && colisorDoAlvo.enabled ? colisorDoAlvo.bounds.center : alvo.position + Vector3.up;

    protected bool EstaMorto { get; private set; }

    // ---------- Ciclo de vida ----------

    protected virtual void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        vida = GetComponent<Vida>();

        agente.speed = velocidade;
        agente.angularSpeed = velocidadeDeGiro; // vira rápido ao mudar de direção (senão anda "de lado/de costas")
        animator.applyRootMotion = false;       // quem move o robô é o NavMeshAgent

        AvisarSeFaltarParametros(ParametrosNecessarios());
    }

    protected virtual void Start()
    {
        EncontrarJogador();
    }

    protected virtual void OnEnable() => vida.AoMorrer += Morrer;
    protected virtual void OnDisable() => vida.AoMorrer -= Morrer;

    void Update()
    {
        if (EstaMorto) return;

        AtualizarIA(Time.deltaTime);
        AtualizarAnimacaoDeMovimento();
    }

    /// <summary>O comportamento de cada inimigo, chamado todo quadro enquanto ele está vivo.</summary>
    protected abstract void AtualizarIA(float deltaTime);

    /// <summary>Nomes dos parâmetros que o Animator deste inimigo precisa ter.</summary>
    protected virtual string[] ParametrosNecessarios() => new[] { "Velocidade", "Morrer" };

    // ---------- Morte ----------

    protected virtual void Morrer()
    {
        EstaMorto = true;
        PararDeAndar();
        TocarAnimacaoDeMorte();
        agente.enabled = false; // o corpo não empurra mais ninguém nem segue o NavMesh
    }

    /// <summary>Padrão: parâmetros do Animator. Inimigos que controlam estados por código sobrescrevem.</summary>
    protected virtual void TocarAnimacaoDeMorte()
    {
        animator.SetFloat(ParamVelocidade, 0f);
        animator.SetTrigger(ParamMorrer);
    }

    // ---------- Movimento ----------

    protected void AndarAte(Vector3 destino)
    {
        if (!agente.enabled || !agente.isOnNavMesh) return;
        agente.isStopped = false;
        agente.SetDestination(destino);
    }

    protected void PararDeAndar()
    {
        if (agente.enabled && agente.isOnNavMesh)
            agente.isStopped = true;
    }

    protected void VirarParaAlvo()
    {
        if (alvo == null) return;

        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.001f) return;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, velocidadeDeGiro * Time.deltaTime);
    }

    /// <summary>Padrão: manda a velocidade para o parâmetro "Velocidade". O chefe sobrescreve.</summary>
    protected virtual void AtualizarAnimacaoDeMovimento()
    {
        float velocidadeAtual = agente.enabled ? agente.velocity.magnitude : 0f;
        animator.SetFloat(ParamVelocidade, velocidadeAtual, 0.1f, Time.deltaTime);
    }

    // ---------- Alvo ----------

    // O jogador é encontrado pela tag "Player". A tag pode estar num objeto de dentro do modelo,
    // então o alvo de verdade passa a ser o objeto que tem a Vida (o personagem em si, nos pés).
    private void EncontrarJogador()
    {
        GameObject jogador = GameObject.FindGameObjectWithTag("Player");
        if (jogador == null)
        {
            Debug.LogWarning($"{GetType().Name}: nenhum objeto com a tag 'Player' encontrado.", this);
            return;
        }

        alvoDanificavel = jogador.GetComponentInParent<IDanificavel>();
        if (alvoDanificavel == null)
            alvoDanificavel = jogador.GetComponentInChildren<IDanificavel>();

        if (alvoDanificavel is Component componenteDaVida)
        {
            alvo = componenteDaVida.transform;
            colisorDoAlvo = componenteDaVida.GetComponent<Collider>(); // o CharacterController também é um Collider
        }
        else
        {
            alvo = jogador.transform;
            Debug.LogWarning($"{GetType().Name}: o jogador não tem o componente Vida, então não vai levar dano.", this);
        }
    }

    /// <summary>
    /// Derruba o jogador (se ele puder ser derrubado e ainda estiver vivo). Ele é arremessado PARA LONGE deste
    /// inimigo: mistura a direção do golpe com a direção "inimigo → jogador", para não cair em cima do inimigo.
    /// </summary>
    protected void DerrubarAlvo(Vector3 direcaoDoGolpe)
    {
        if (!AlvoEstaVivo()) return;
        if (!(alvoDanificavel is Component componente) || !componente.TryGetComponent(out IDerrubavel derrubavel)) return;

        Vector3 paraLonge = alvo.position - transform.position;
        paraLonge.y = 0f;
        direcaoDoGolpe.y = 0f;
        Vector3 direcao = paraLonge.normalized + direcaoDoGolpe.normalized;
        derrubavel.Derrubar(direcao.sqrMagnitude > 0.01f ? direcao : direcaoDoGolpe);
    }

    protected bool AlvoEstaVivo()
    {
        return alvo != null && alvoDanificavel != null && alvoDanificavel.EstaVivo;
    }

    /// <summary>Distância até o jogador no chão (ignora a altura).</summary>
    protected float DistanciaAteAlvo()
    {
        if (alvo == null) return float.MaxValue;

        Vector3 diferenca = alvo.position - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    // ---------- Animação ----------

    /// <summary>
    /// Quanto do estado do Animator já tocou (0 a 1), considerando também a transição para ele.
    /// "estaNoEstado" diz se o Animator está (ou entrando) nesse estado agora.
    /// Usar isto em vez de tempo fixo mantém golpes e disparos sincronizados com a animação,
    /// mesmo mudando a velocidade do estado no Animator.
    /// </summary>
    protected float ProgressoDoEstado(int hashDoEstado, out bool estaNoEstado)
    {
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        estaNoEstado = info.shortNameHash == hashDoEstado;
        return estaNoEstado ? Mathf.Clamp01(info.normalizedTime) : 0f;
    }

    protected void AvisarSeFaltarEstado(string nomeDoEstado)
    {
        if (!animator.HasState(0, Animator.StringToHash(nomeDoEstado)))
            Debug.LogWarning($"{GetType().Name}: estado '{nomeDoEstado}' não encontrado na Base Layer do Animator.", this);
    }

    // ---------- Ajuda ----------

    private void AvisarSeFaltarParametros(string[] nomes)
    {
        foreach (string nome in nomes)
        {
            bool existe = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                existe |= p.name == nome;

            if (!existe)
                Debug.LogWarning($"{GetType().Name}: o Animator do robô não tem o parâmetro '{nome}'.", this);
        }
    }

    // Desenha o alcance de percepção na aba Scene quando o robô está selecionado
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaDeDeteccao);
    }
}
