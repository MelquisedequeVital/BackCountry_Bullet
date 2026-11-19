using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Qualquer coisa que pode levar dano (robôs, jogador, barris...).
/// Quem causa dano (tiro, soco do robô) só conhece esta interface, não o script Vida.
/// </summary>
public interface IDanificavel
{
    bool EstaVivo { get; }

    /// <param name="quantidade">Quanto de vida tirar.</param>
    /// <param name="ponto">Onde o golpe acertou (para efeitos, como faísca).</param>
    /// <param name="direcao">De onde o golpe veio (para reações, como empurrão).</param>
    void ReceberDano(float quantidade, Vector3 ponto, Vector3 direcao);
}

/// <summary>
/// Qualquer componente que pode deixar o personagem invencível por um tempo
/// (ex.: a Esquiva durante o mergulho). A Vida consulta todos antes de aplicar dano.
/// </summary>
public interface IFonteDeInvencibilidade
{
    bool EstaInvencivel { get; }
}

/// <summary>
/// Qualquer componente que muda o dano recebido (ex.: armadura que reduz, ponto fraco que aumenta).
/// O dano final é multiplicado por todos os modificadores do personagem.
/// </summary>
public interface IModificadorDeDano
{
    /// <summary>1 = dano normal, 0.15 = só 15%, 1.5 = 50% a mais.</summary>
    float MultiplicadorDeDano { get; }
}

/// <summary>
/// Vida de um personagem: recebe dano, cura e avisa quando morre.
/// Serve para os robôs e para o jogador. Outros scripts (IA, interface, sons)
/// reagem pelos eventos, sem que este script precise conhecê-los.
/// </summary>
public class Vida : MonoBehaviour, IDanificavel
{
    [Header("Vida")]
    [Tooltip("Vida com que o personagem começa")]
    [Min(1f)]
    public float vidaMaxima = 100f;

    [Header("Morte")]
    [Tooltip("Remove o objeto da cena depois de morrer (bom para inimigos; desmarque no jogador)")]
    public bool destruirAoMorrer = true;

    [Tooltip("Segundos entre morrer e sumir (tempo para a animação de morte tocar)")]
    [Min(0f)]
    public float atrasoParaDestruir = 3f;

    [Tooltip("Desliga os colisores ao morrer, para os tiros atravessarem o corpo")]
    public bool desligarColisoresAoMorrer = true;

    [Header("Eventos (opcional, pelo Inspector)")]
    public UnityEvent aoMorrer;

    [Header("Teste")]
    [Tooltip("Mostra no Console cada dano recebido")]
    public bool mostrarNoConsole = true;

    /// <summary>Disparado a cada dano: (quantidade, ponto, direção).</summary>
    public event Action<float, Vector3, Vector3> AoReceberDano;

    /// <summary>Disparado uma única vez, quando a vida chega a zero.</summary>
    public event Action AoMorrer;

    /// <summary>Disparado quando um golpe é ignorado por invencibilidade (ex.: esquivou no tempo certo).</summary>
    public event Action AoEvitarDano;

    public float VidaAtual { get; private set; }
    public float Porcentagem => VidaAtual / vidaMaxima;
    public bool EstaVivo => VidaAtual > 0f;

    private IFonteDeInvencibilidade[] fontesDeInvencibilidade;
    private IModificadorDeDano[] modificadoresDeDano;

    void Awake()
    {
        VidaAtual = vidaMaxima;
        fontesDeInvencibilidade = GetComponents<IFonteDeInvencibilidade>();
        modificadoresDeDano = GetComponents<IModificadorDeDano>();
    }

    private float AplicarModificadores(float quantidade)
    {
        foreach (IModificadorDeDano modificador in modificadoresDeDano)
            quantidade *= modificador.MultiplicadorDeDano;
        return quantidade;
    }

    public bool EstaInvencivel()
    {
        foreach (IFonteDeInvencibilidade fonte in fontesDeInvencibilidade)
        {
            if (fonte.EstaInvencivel) return true;
        }
        return false;
    }

    public void ReceberDano(float quantidade, Vector3 ponto, Vector3 direcao)
    {
        if (!EstaVivo || quantidade <= 0f) return;

        if (EstaInvencivel())
        {
            if (mostrarNoConsole) Debug.Log($"{name} desviou do golpe (invencível)!", this);
            AoEvitarDano?.Invoke();
            return;
        }

        quantidade = AplicarModificadores(quantidade); // ex.: armadura ou ponto fraco
        VidaAtual = Mathf.Max(0f, VidaAtual - quantidade);
        RegistrarNoConsole(quantidade);
        AoReceberDano?.Invoke(quantidade, ponto, direcao);

        if (!EstaVivo)
            Morrer();
    }

    public void Curar(float quantidade)
    {
        if (!EstaVivo || quantidade <= 0f) return;
        VidaAtual = Mathf.Min(vidaMaxima, VidaAtual + quantidade);
    }

    private void Morrer()
    {
        AoMorrer?.Invoke();
        aoMorrer?.Invoke();

        if (desligarColisoresAoMorrer)
            DesligarColisores();

        if (destruirAoMorrer)
            Destroy(gameObject, atrasoParaDestruir);
    }

    private void DesligarColisores()
    {
        foreach (Collider colisor in GetComponentsInChildren<Collider>())
        {
            // O CharacterController do jogador também é um colisor; desligá-lo trava o movimento
            if (colisor is CharacterController) continue;
            colisor.enabled = false;
        }
    }

    private void RegistrarNoConsole(float quantidade)
    {
        if (!mostrarNoConsole) return;

        string estado = EstaVivo ? $"{VidaAtual:0}/{vidaMaxima:0}" : "MORREU";
        Debug.Log($"{name} levou {quantidade:0} de dano → {estado}", this);
    }
}
