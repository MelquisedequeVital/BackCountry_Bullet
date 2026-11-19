using UnityEngine;

/// <summary>
/// IA do inimigo corpo a corpo (robô azul):
///   Ocioso → (vê o jogador) → Perseguindo → (chegou perto) → Preparando → Atacando → Perseguindo...
///   Qualquer estado → Morto (quando a Vida chega a zero; tratado no InimigoBase).
/// O "Preparando" é uma pausa de aviso antes do soco: o robô para e vira para o jogador,
/// dando tempo de reagir (é um tutorial).
/// O dano sai num momento fixo do soco ("Momento Do Golpe"), e só acerta se o jogador ainda
/// estiver perto e na frente do robô. A invencibilidade do mergulho é respeitada pela Vida do jogador.
/// </summary>
public class InimigoIA : InimigoBase
{
    private enum Estado { Ocioso, Perseguindo, Preparando, Atacando }

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

    private Estado estado = Estado.Ocioso;
    private float tempoNoEstado;
    private bool golpeJaAplicado;
    private float proximoAtaquePermitido;

    protected override void Awake()
    {
        base.Awake();
        agente.stoppingDistance = distanciaParaAtacar * 0.9f;
    }

    protected override string[] ParametrosNecessarios() => new[] { "Velocidade", "Atacar", "Morrer" };

    protected override void AtualizarIA(float deltaTime)
    {
        tempoNoEstado += deltaTime;

        if (!AlvoEstaVivo())
            TrocarPara(Estado.Ocioso);

        switch (estado)
        {
            case Estado.Ocioso: AtualizarOcioso(); break;
            case Estado.Perseguindo: AtualizarPerseguindo(); break;
            case Estado.Preparando: AtualizarPreparando(); break;
            case Estado.Atacando: AtualizarAtacando(); break;
        }
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

    // Desenha os alcances na aba Scene quando o robô está selecionado
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, distanciaParaAtacar);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceDoGolpe);
    }
}
