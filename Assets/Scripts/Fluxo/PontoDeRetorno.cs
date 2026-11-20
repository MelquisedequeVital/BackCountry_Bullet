using StarterAssets;
using UnityEngine;

/// <summary>
/// Ponto de retorno (checkpoint): quando o jogador passa por aqui, é aqui que ele renasce se morrer depois.
/// Ele renasce na posição deste objeto, olhando para a frente dele (a seta azul na aba Scene).
/// Coloque no começo de cada parte (bar, praça, portão, torre), num lugar seguro, longe dos inimigos.
/// Precisa de um Collider (ex.: Box Collider); ele vira "Is Trigger" sozinho.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PontoDeRetorno : MonoBehaviour
{
    private bool ativado;

    void Reset() => GetComponent<Collider>().isTrigger = true;
    void Awake() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider outro)
    {
        if (ativado || FluxoDoJogo.Instancia == null) return;
        if (outro.GetComponentInParent<IControleMovimento>() == null) return; // só o jogador ativa

        ativado = true;
        FluxoDoJogo.Instancia.DefinirPontoDeRetorno(transform.position, transform.rotation);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.3f);
        if (TryGetComponent(out BoxCollider caixa))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(caixa.center, caixa.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        // Seta mostrando para onde o jogador vai olhar ao renascer
        Gizmos.color = Color.blue;
        Vector3 inicio = transform.position + Vector3.up;
        Gizmos.DrawLine(inicio, inicio + transform.forward * 1.5f);
        Gizmos.DrawSphere(inicio + transform.forward * 1.5f, 0.1f);
    }
}
