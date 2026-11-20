using StarterAssets;
using UnityEngine;

/// <summary>
/// Área invisível que começa uma conversa sozinha quando o jogador entra nela.
/// Ex.: a janela do bar (o Seu Zé continua falando) e a entrada da torre (o Coronel começa a falar).
/// Coloque num objeto com um Collider (ex.: Box Collider); ele vira "Is Trigger" sozinho.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZonaDeDialogo : MonoBehaviour
{
    public Dialogo dialogo;

    [Tooltip("Quem fala: dá o nome das falas e a câmera enquadra ele e o jogador. Vazio = a câmera não muda.")]
    public NPC quemFala;

    [Tooltip("Só começa depois que esta conversa já tiver acontecido (ex.: a da janela só depois da do balcão). Vazio = sem exigência.")]
    public Dialogo exigirConversa;

    private bool jaTocou;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void Awake() => GetComponent<Collider>().isTrigger = true;

    // "Stay" em vez de "Enter": se o jogador entrar caído ou esquivando, a conversa começa quando ele ficar livre
    void OnTriggerStay(Collider outro)
    {
        if (jaTocou || dialogo == null) return;
        if (outro.GetComponentInParent<IControleMovimento>() == null) return; // só o jogador ativa
        if (exigirConversa != null && !CaixaDeDialogo.JaTocou(exigirConversa)) return;

        CaixaDeDialogo caixa = CaixaDeDialogo.Instancia;
        if (!caixa.JogadorPodeConversar || CaixaDeDialogo.EstaAberta) return;

        jaTocou = true;
        caixa.Iniciar(dialogo, quemFala, null);
    }

    void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider caixa)) return;
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(caixa.center, caixa.size);
    }
}
