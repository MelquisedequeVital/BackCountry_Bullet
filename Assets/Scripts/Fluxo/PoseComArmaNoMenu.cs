using UnityEngine;

/// <summary>
/// Pose do Severino no menu principal: depois da animação de mira, gira o peito igual ao EquiparArma
/// faz no jogo (correção Mixamo), para a arma apontar para frente. Vai no mesmo objeto do Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PoseComArmaNoMenu : MonoBehaviour
{
    [Tooltip("Corrige a arma apontando para a esquerda/direita (mesmo valor do EquiparArma)")]
    [Range(-90f, 90f)]
    public float ajusteHorizontal = 40f;

    [Tooltip("Corrige o cano da arma apontando muito para cima ou para baixo (mesmo valor do EquiparArma)")]
    [Range(-60f, 60f)]
    public float ajusteVertical = 15f;

    private Transform ossoPeito;

    void Start()
    {
        ossoPeito = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);
    }

    // Roda após o Animator calcular a pose do frame
    void LateUpdate()
    {
        if (ossoPeito == null) return;
        ossoPeito.Rotate(transform.up, ajusteHorizontal, Space.World);
        ossoPeito.Rotate(transform.right, ajusteVertical, Space.World);
    }
}
