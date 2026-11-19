using UnityEngine;

/// <summary>
/// Efeitos visuais e sonoros do disparo: clarão no cano e som do tiro.
/// Os dois campos são opcionais; o que ficar vazio é simplesmente ignorado.
/// </summary>
public class EfeitosTiro : MonoBehaviour, IReacaoAoTiro
{
    [Tooltip("Partícula de clarão na ponta do cano (pode deixar vazio)")]
    public ParticleSystem claraoDoCano;

    [Tooltip("AudioSource com o som do tiro no campo AudioClip (pode deixar vazio)")]
    public AudioSource somDoTiro;

    [Tooltip("Variação aleatória do tom do som, para os tiros não soarem todos iguais")]
    [Range(0f, 0.3f)]
    public float variacaoDeTom = 0.05f;

    public void AoAtirar()
    {
        TocarClarao();
        TocarSom();
    }

    private void TocarClarao()
    {
        if (claraoDoCano != null)
            claraoDoCano.Play();
    }

    // PlayOneShot permite que um tiro não corte o som do anterior
    private void TocarSom()
    {
        if (somDoTiro == null || somDoTiro.clip == null) return;

        somDoTiro.pitch = 1f + Random.Range(-variacaoDeTom, variacaoDeTom);
        somDoTiro.PlayOneShot(somDoTiro.clip);
    }
}
