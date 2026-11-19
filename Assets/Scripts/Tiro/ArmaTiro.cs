using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Decide QUANDO a arma dispara: lê o botão do mouse, confere se o personagem está armado
/// e respeita a cadência. Ele não sabe o que acontece no tiro: só avisa os componentes
/// IReacaoAoTiro do mesmo objeto (AnimacaoTiro, RecuoArma, DisparoRaycast, EfeitosTiro).
/// </summary>
public class ArmaTiro : MonoBehaviour
{
    [Header("Cadência")]
    [Tooltip("Tempo mínimo (em segundos) entre um tiro e outro")]
    [Min(0f)]
    public float tempoEntreTiros = 0.3f;

    [Tooltip("Marque para atirar sem parar enquanto segura o botão (metralhadora)")]
    public bool tiroAutomatico = false;

    private IEstadoArma estadoArma;
    private IReacaoAoTiro[] reacoes;
    private float proximoTiroPermitido;

    void Awake()
    {
        BuscarDependencias();
        AvisarSeFaltarDependencias();
    }

    void Update()
    {
        if (JogadorQuerAtirar() && PodeAtirar())
        {
            Atirar();
        }
    }

    // ---------- Configuração ----------

    private void BuscarDependencias()
    {
        estadoArma = GetComponent<IEstadoArma>();
        reacoes = GetComponents<IReacaoAoTiro>();
    }

    private void AvisarSeFaltarDependencias()
    {
        if (estadoArma == null)
            Debug.LogWarning("ArmaTiro: nenhum componente de estado da arma (ex.: EquiparArma) encontrado neste objeto.", this);

        if (reacoes.Length == 0)
            Debug.LogWarning("ArmaTiro: nenhuma reação ao tiro encontrada. Adicione AnimacaoTiro, RecuoArma, DisparoRaycast ou EfeitosTiro.", this);
    }

    // ---------- Regras do tiro ----------

    private bool JogadorQuerAtirar()
    {
        if (Mouse.current == null) return false;

        return tiroAutomatico
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool PodeAtirar()
    {
        return EstaArmado() && CadenciaLiberada();
    }

    private bool EstaArmado()
    {
        return estadoArma != null && estadoArma.EstaArmado;
    }

    private bool CadenciaLiberada()
    {
        return Time.time >= proximoTiroPermitido;
    }

    // ---------- Disparo ----------

    private void Atirar()
    {
        ReiniciarCadencia();
        NotificarReacoes();
    }

    private void ReiniciarCadencia()
    {
        proximoTiroPermitido = Time.time + tempoEntreTiros;
    }

    private void NotificarReacoes()
    {
        foreach (IReacaoAoTiro reacao in reacoes)
        {
            if (EstaAtiva(reacao))
                reacao.AoAtirar();
        }
    }

    // Componentes desligados (checkbox desmarcado no Inspector) não reagem ao tiro
    private static bool EstaAtiva(IReacaoAoTiro reacao)
    {
        return !(reacao is Behaviour componente) || componente.isActiveAndEnabled;
    }
}
