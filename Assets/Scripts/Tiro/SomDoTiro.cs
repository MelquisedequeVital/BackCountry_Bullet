using UnityEngine;

/// <summary>
/// Toca o som do disparo, com uma pequena variação de tom para os tiros não soarem todos iguais.
/// (O clarão do cano fica no componente ClaraoDoCano.)
/// </summary>
public class SomDoTiro : MonoBehaviour, IReacaoAoTiro
{
    [Tooltip("AudioSource com o som do tiro no campo AudioClip (pode deixar vazio)")]
    public AudioSource somDoTiro;

    [Tooltip("Variação aleatória do tom do som")]
    [Range(0f, 0.3f)]
    public float variacaoDeTom = 0.05f;

    public void AoAtirar()
    {
        if (somDoTiro == null || somDoTiro.clip == null) return;

        somDoTiro.pitch = 1f + Random.Range(-variacaoDeTom, variacaoDeTom);

        // PlayOneShot permite que um tiro não corte o som do anterior
        somDoTiro.PlayOneShot(somDoTiro.clip);
    }
}
