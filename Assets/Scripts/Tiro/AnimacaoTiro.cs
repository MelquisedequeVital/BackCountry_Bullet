using UnityEngine;

/// <summary>
/// Toca a animação de tiro no Animator, disparando o Trigger configurado.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimacaoTiro : MonoBehaviour, IReacaoAoTiro
{
    [Tooltip("Nome exato do parâmetro Trigger criado no Animator")]
    public string nomeTrigger = "Atirar";

    private Animator animator;
    private int hashTrigger;
    private bool triggerExiste;

    void Awake()
    {
        animator = GetComponent<Animator>();
        hashTrigger = Animator.StringToHash(nomeTrigger);
        triggerExiste = AnimatorTemTrigger(hashTrigger);

        if (!triggerExiste)
            Debug.LogWarning($"AnimacaoTiro: Trigger '{nomeTrigger}' não encontrado no Animator! Crie-o na aba Parameters.", this);
    }

    public void AoAtirar()
    {
        if (triggerExiste)
            animator.SetTrigger(hashTrigger);
    }

    private bool AnimatorTemTrigger(int hash)
    {
        foreach (AnimatorControllerParameter parametro in animator.parameters)
        {
            if (parametro.nameHash == hash && parametro.type == AnimatorControllerParameterType.Trigger)
                return true;
        }
        return false;
    }
}
