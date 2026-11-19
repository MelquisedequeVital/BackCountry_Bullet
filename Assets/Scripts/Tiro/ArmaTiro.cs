using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Decide QUANDO a arma dispara: lê o botão do mouse, confere se o personagem está armado
/// e respeita a cadência. Ele não sabe o que acontece no tiro: só avisa os componentes
/// IReacaoAoTiro do mesmo objeto (AnimacaoTiro, RecuoArma, RecuoCamera, DisparoRaycast, ClaraoDoCano, SomDoTiro...).
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
    private IEstadoEsquiva estadoEsquiva;
    private IReacaoAoTiro[] reacoes;
    private IPermissaoDeTiro[] permissoes; // ex.: CargaDaArma (bloqueia enquanto recarrega)
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
        estadoEsquiva = GetComponent<IEstadoEsquiva>(); // opcional: sem esquiva, o tiro funciona igual
        reacoes = GetComponents<IReacaoAoTiro>();
        permissoes = GetComponents<IPermissaoDeTiro>();
    }

    private void AvisarSeFaltarDependencias()
    {
        if (estadoArma == null)
            Debug.LogWarning("ArmaTiro: nenhum componente de estado da arma (ex.: EquiparArma) encontrado neste objeto.", this);

        if (reacoes.Length == 0)
            Debug.LogWarning("ArmaTiro: nenhuma reação ao tiro encontrada. Adicione AnimacaoTiro, RecuoArma, DisparoRaycast, ClaraoDoCano etc.", this);
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
        return EstaArmado() && !EstaEsquivando() && CadenciaLiberada() && TodosPermitem();
    }

    // Pergunta a cada "permissão" (ex.: a carga da arma) se pode atirar agora
    private bool TodosPermitem()
    {
        foreach (IPermissaoDeTiro permissao in permissoes)
        {
            if (permissao is Behaviour componente && !componente.isActiveAndEnabled) continue;
            if (!permissao.PodeAtirar) return false;
        }
        return true;
    }

    private bool EstaEsquivando()
    {
        return estadoEsquiva != null && estadoEsquiva.EstaEsquivando;
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
