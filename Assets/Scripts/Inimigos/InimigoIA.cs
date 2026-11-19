using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA do inimigo corpo a corpo (robô azul):
///   Ocioso → (vê o jogador) → Perseguindo → (chegou perto) → Preparando → Atacando → Perseguindo...
///   Qualquer estado → Morto (quando a Vida chega a zero).
/// O "Preparando" é uma pausa de aviso antes do soco: o robô para e vira para o jogador,
/// dando tempo de reagir (é um tutorial).
/// O dano sai num momento fixo do soco ("Momento Do Golpe"), e só acerta se o jogador ainda
/// estiver perto e na frente do robô. A invencibilidade do mergulho é respeitada pela Vida do jogador.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(Vida))]
public class InimigoIA : MonoBehaviour
{
    private enum Estado { Ocioso, Perseguindo, Preparando, Atacando, Morto }

    [Header("Percepção")]
    [Tooltip("A que distância (metros) o robô percebe o jogador e começa a persegui-lo")]
    [Min(0f)]
    public float distanciaDeDeteccao = 15f;

    [Header("Movimento")]
    [Tooltip("Velocidade de caminhada (m/s)")]
    [Min(0.1f)]
    public float velocidade = 1.3f;

    [Tooltip("Rapidez com que o robô vira para o jogador (graus por segundo)")]
    [Min(10f)]
    public float velocidadeDeGiro = 360f;

    [Header("Ataque")]
    [Tooltip("Distância em que o robô para de andar e começa o ataque")]
    [Min(0.1f)]
    public float distanciaParaAtacar = 1.6f;

    [Tooltip("Até onde o soco alcança no momento do golpe (um pouco maior que a distância para atacar)")]
    [Min(0.1f)]
    public float alcanceDoGolpe = 2.2f;

    [Tooltip("Largura do golpe à frente do robô (graus). Fora desse cone, o soco erra.")]
    [Range(10f, 180f)]
    public float anguloDoGolpe = 90f;

    [Tooltip("Dano de cada soco")]
    [Min(0f)]
    public float dano = 15f;

    [Header("Tempo do Ataque")]
    [Tooltip("Pausa de aviso antes do soco (o robô para e encara o jogador)")]
    [Min(0f)]
    public float tempoDePreparacao = 0.6f;

    [Tooltip("Segundos depois do início da animação em que o soco acerta (no 'mutant punch' é cerca de 0,27 s)")]
    [Min(0f)]
    public float momentoDoGolpe = 0.27f;

    [Tooltip("Duração total do ataque antes de voltar a andar (a animação de soco tem 1,1 s)")]
    [Min(0.1f)]
    public float duracaoDoAtaque = 1.1f;

    [Tooltip("Descanso entre um ataque e outro (segundos)")]
    [Min(0f)]
    public float tempoEntreAtaques = 1f;

    // Parâmetros que precisam existir no Animator do robô
    private static readonly int ParamVelocidade = Animator.StringToHash("Velocidade");
    private static readonly int ParamAtacar = Animator.StringToHash("Atacar");
    private static readonly int ParamMorrer = Animator.StringToHash("Morrer");

    private NavMeshAgent agente;
    private Animator animator;
    private Vida vida;

    private Transform alvo;
    private IDanificavel alvoDanificavel;

    private Estado estado = Estado.Ocioso;
    private float tempoNoEstado;
    private bool golpeJaAplicado;
    private float proximoAtaquePermitido;

    // ---------- Ciclo de vida ----------

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        vida = GetComponent<Vida>();

        agente.speed = velocidade;
        agente.stoppingDistance = distanciaParaAtacar * 0.9f;
        animator.applyRootMotion = false; // quem move o robô é o NavMeshAgent

        AvisarSeFaltarParametros();
    }

    void Start()
    {
        EncontrarJogador();
    }

    void OnEnable() => vida.AoMorrer += Morrer;
    void OnDisable() => vida.AoMorrer -= Morrer;

    void Update()
    {
        if (estado == Estado.Morto) return;

        tempoNoEstado += Time.deltaTime;

        if (!AlvoEstaVivo())
            TrocarPara(Estado.Ocioso);

        switch (estado)
        {
            case Estado.Ocioso: AtualizarOcioso(); break;
            case Estado.Perseguindo: AtualizarPerseguindo(); break;
            case Estado.Preparando: AtualizarPreparando(); break;
            case Estado.Atacando: AtualizarAtacando(); break;
        }

        AtualizarAnimacaoDeMovimento();
    }

    // ---------- Estados ----------

    private void AtualizarOcioso()
    {
        PararDeAndar();

        if (AlvoEstaVivo() && DistanciaAteAlvo() <= distanciaDeDeteccao)
            TrocarPara(Estado.Perseguindo);
    }

    private void AtualizarPerseguindo()
    {
        AndarAte(alvo.position);

        if (DistanciaAteAlvo() <= distanciaParaAtacar && Time.time >= proximoAtaquePermitido)
            TrocarPara(Estado.Preparando);
    }

    // Aviso para o jogador: o robô para e encara antes de socar
    private void AtualizarPreparando()
    {
        PararDeAndar();
        VirarParaAlvo();

        if (tempoNoEstado >= tempoDePreparacao)
        {
            animator.SetTrigger(ParamAtacar);
            golpeJaAplicado = false;
            TrocarPara(Estado.Atacando);
        }
    }

    private void AtualizarAtacando()
    {
        PararDeAndar();

        if (!golpeJaAplicado && tempoNoEstado >= momentoDoGolpe)
        {
            AplicarGolpe();
            golpeJaAplicado = true;
        }

        if (tempoNoEstado >= duracaoDoAtaque)
        {
            proximoAtaquePermitido = Time.time + tempoEntreAtaques;
            TrocarPara(Estado.Perseguindo);
        }
    }

    private void Morrer()
    {
        TrocarPara(Estado.Morto);
        PararDeAndar();
        animator.SetFloat(ParamVelocidade, 0f);
        animator.SetTrigger(ParamMorrer);
        agente.enabled = false; // o corpo não empurra mais ninguém nem segue o NavMesh
    }

    private void TrocarPara(Estado novoEstado)
    {
        if (estado == novoEstado) return;
        estado = novoEstado;
        tempoNoEstado = 0f;
    }

    // ---------- Ataque ----------

    // O soco só acerta se o jogador ainda estiver perto e na frente no momento do golpe:
    // assim, se afastar ou esquivar no tempo certo funciona.
    private void AplicarGolpe()
    {
        if (!AlvoEstaVivo()) return;

        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;

        bool pertoOBastante = paraAlvo.magnitude <= alcanceDoGolpe;
        bool naFrente = Vector3.Angle(transform.forward, paraAlvo) <= anguloDoGolpe * 0.5f;

        if (pertoOBastante && naFrente)
            alvoDanificavel.ReceberDano(dano, alvo.position + Vector3.up, paraAlvo.normalized);
    }

    // ---------- Movimento ----------

    private void AndarAte(Vector3 destino)
    {
        if (!agente.isOnNavMesh) return;
        agente.isStopped = false;
        agente.SetDestination(destino);
    }

    private void PararDeAndar()
    {
        if (agente.enabled && agente.isOnNavMesh)
            agente.isStopped = true;
    }

    private void VirarParaAlvo()
    {
        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.001f) return;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, velocidadeDeGiro * Time.deltaTime);
    }

    private void AtualizarAnimacaoDeMovimento()
    {
        float velocidadeAtual = agente.enabled ? agente.velocity.magnitude : 0f;
        animator.SetFloat(ParamVelocidade, velocidadeAtual, 0.1f, Time.deltaTime);
    }

    // ---------- Alvo ----------

    // O jogador é encontrado pela tag "Player"; a Vida pode estar no mesmo objeto ou num pai/filho
    private void EncontrarJogador()
    {
        GameObject jogador = GameObject.FindGameObjectWithTag("Player");
        if (jogador == null)
        {
            Debug.LogWarning("InimigoIA: nenhum objeto com a tag 'Player' encontrado.", this);
            return;
        }

        alvo = jogador.transform;
        alvoDanificavel = jogador.GetComponentInParent<IDanificavel>();
        if (alvoDanificavel == null)
            alvoDanificavel = jogador.GetComponentInChildren<IDanificavel>();

        if (alvoDanificavel == null)
            Debug.LogWarning("InimigoIA: o jogador não tem o componente Vida, então não vai levar dano.", this);
    }

    private bool AlvoEstaVivo()
    {
        return alvo != null && alvoDanificavel != null && alvoDanificavel.EstaVivo;
    }

    private float DistanciaAteAlvo()
    {
        Vector3 diferenca = alvo.position - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    // ---------- Ajuda ----------

    private void AvisarSeFaltarParametros()
    {
        foreach (string nome in new[] { "Velocidade", "Atacar", "Morrer" })
        {
            bool existe = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                existe |= p.name == nome;

            if (!existe)
                Debug.LogWarning($"InimigoIA: o Animator do robô não tem o parâmetro '{nome}'.", this);
        }
    }

    // Desenha os alcances na aba Scene quando o robô está selecionado
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaDeDeteccao);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, distanciaParaAtacar);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceDoGolpe);
    }
}
