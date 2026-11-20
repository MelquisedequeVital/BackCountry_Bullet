using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Personagem com quem o jogador conversa apertando E.
/// As conversas são tocadas em ordem, uma de cada vez. Uma conversa pode ficar trancada até um inimigo morrer
/// (ex.: o Seu Zé só agradece depois que o robô azul cai) e pode mudar o NPC de lugar quando for liberada
/// (ex.: o Seu Zé aparece na porta do bar).
/// Coloque no objeto raiz do NPC (o mesmo do modelo).
/// </summary>
public class NPC : MonoBehaviour
{
    [Serializable]
    public class Etapa
    {
        public Dialogo dialogo;

        [Tooltip("A conversa só fica disponível depois que esta outra conversa já tiver acontecido " +
                 "(ex.: o agradecimento só depois do pedido da janela). Vazio = sem exigência.")]
        public Dialogo exigirConversa;

        [Tooltip("A conversa só fica disponível depois que TODOS estes inimigos morrerem " +
                 "(arraste os inimigos da cena). Lista vazia = disponível já.")]
        public List<Vida> liberarQuandoMorrerem = new List<Vida>();

        [Tooltip("Quando a conversa for liberada, o NPC vai para este ponto (posição e direção). Vazio = fica onde está.")]
        public Transform irParaOPonto;

        [Tooltip("Começa sozinha assim que for liberada, sem precisar apertar E")]
        public bool comecarSozinha;

        [Tooltip("Só para \"Comecar Sozinha\": o jogador precisa estar a esta distância (m) do NPC. " +
                 "0 = em qualquer lugar (ex.: a fala depois do boss morrer).")]
        [Min(0f)] public float distanciaParaComecar;
    }

    [Tooltip("Nome do personagem: aparece nas falas dele e no aviso \"[E] Falar com ...\". Mude aqui para renomear.")]
    public string nome = "NPC";

    [Header("Conversas")]
    [Tooltip("Tocadas em ordem: a primeira na primeira vez que o jogador apertar E, a segunda na próxima, e assim por diante")]
    public List<Etapa> conversas = new List<Etapa>();

    [Tooltip("Depois da última conversa, apertar E toca esta (ex.: uma fala curta). Vazio = não conversa mais.")]
    public Dialogo conversaRepetida;

    [Tooltip("Distância (m) para aparecer o \"[E] Falar\"")]
    [Min(0.5f)] public float distanciaParaFalar = 2.5f;

    [Tooltip("Vira para o jogador durante a conversa")]
    public bool virarParaOJogador = true;

    [Header("Chamado de longe")]
    [Tooltip("Fala solta (legenda) quando o jogador chega perto pela primeira vez, sem travar o jogador. Ex.: \"Ei, tu! Chega aqui!\"")]
    public Dialogo chamadoDeLonge;
    [Min(0f)] public float distanciaDoChamado = 10f;

    private static readonly List<NPC> todos = new List<NPC>();

    private int etapaAtual;
    private bool chamadoFeito;
    private bool jaFoiParaOPonto;

    void OnEnable() => todos.Add(this);
    void OnDisable() => todos.Remove(this);

    void Update()
    {
        CaixaDeDialogo caixa = CaixaDeDialogo.Instancia;
        if (caixa.Jogador == null) return;

        AtualizarEtapaLiberada(caixa);
        TentarChamarDeLonge(caixa);
    }

    // ---------- Conversa ----------

    /// <summary>A conversa que tocaria agora se o jogador apertasse E (nula se não há nenhuma disponível).</summary>
    public Dialogo ProximaConversa()
    {
        if (etapaAtual < conversas.Count)
        {
            Etapa etapa = conversas[etapaAtual];
            return EstaLiberada(etapa) ? etapa.dialogo : null;
        }
        return conversaRepetida;
    }

    public void Conversar()
    {
        Dialogo dialogo = ProximaConversa();
        if (dialogo == null) return;

        chamadoFeito = true; // já está conversando: não precisa mais chamar de longe
        bool ehUmaEtapa = etapaAtual < conversas.Count;

        CaixaDeDialogo.Instancia.Iniciar(dialogo, this, () =>
        {
            if (!ehUmaEtapa) return;
            etapaAtual++;
            jaFoiParaOPonto = false;
        });
    }

    private static bool EstaLiberada(Etapa etapa)
    {
        if (etapa.exigirConversa != null && !CaixaDeDialogo.JaTocou(etapa.exigirConversa)) return false;

        foreach (Vida inimigo in etapa.liberarQuandoMorrerem)
        {
            if (inimigo != null && inimigo.EstaVivo) return false; // ainda tem inimigo vivo
        }
        return true;
    }

    /// <summary>O NPC da cena que tem esta conversa na lista dele (ou nulo).</summary>
    public static NPC DonoDaConversa(Dialogo dialogo)
    {
        if (dialogo == null) return null;
        foreach (NPC npc in todos)
        {
            foreach (Etapa etapa in npc.conversas)
            {
                if (etapa.dialogo == dialogo) return npc;
            }
        }
        return null;
    }

    // Quando a próxima conversa é liberada: muda de lugar (se tiver ponto) e começa sozinha (se for o caso)
    private void AtualizarEtapaLiberada(CaixaDeDialogo caixa)
    {
        if (etapaAtual >= conversas.Count) return;
        Etapa etapa = conversas[etapaAtual];
        if (!EstaLiberada(etapa)) return;

        if (etapa.irParaOPonto != null && !jaFoiParaOPonto)
        {
            transform.SetPositionAndRotation(etapa.irParaOPonto.position, etapa.irParaOPonto.rotation);
            jaFoiParaOPonto = true;
        }

        bool pertoOBastante = etapa.distanciaParaComecar <= 0f ||
                              DistanciaAte(caixa.Jogador.position) <= etapa.distanciaParaComecar;
        if (etapa.comecarSozinha && pertoOBastante && caixa.JogadorPodeConversar && !CaixaDeDialogo.EstaAberta)
            Conversar();
    }

    private void TentarChamarDeLonge(CaixaDeDialogo caixa)
    {
        if (chamadoFeito || chamadoDeLonge == null || CaixaDeDialogo.EstaAberta) return;
        if (DistanciaAte(caixa.Jogador.position) > distanciaDoChamado) return;

        chamadoFeito = true;
        caixa.MostrarLegenda(chamadoDeLonge, this);
    }

    // ---------- Procura ----------

    /// <summary>O NPC mais próximo da posição que está perto o bastante e tem conversa disponível (ou nulo).</summary>
    public static NPC MaisProximoDisponivel(Vector3 posicao)
    {
        NPC melhor = null;
        float menorDistancia = float.MaxValue;

        foreach (NPC npc in todos)
        {
            float distancia = npc.DistanciaAte(posicao);
            if (distancia > npc.distanciaParaFalar || distancia >= menorDistancia) continue;
            if (npc.ProximaConversa() == null) continue;

            melhor = npc;
            menorDistancia = distancia;
        }
        return melhor;
    }

    private float DistanciaAte(Vector3 posicao)
    {
        Vector3 diferenca = posicao - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, distanciaParaFalar);
        if (chamadoDeLonge != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, distanciaDoChamado);
        }
    }
}
