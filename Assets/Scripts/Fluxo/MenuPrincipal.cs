using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Botões da cena do menu principal: Jogar e Sair.
/// Coloque num objeto da cena do menu e ligue os botões em Jogar() e Sair() (OnClick).
/// Ao abrir o menu, solta o cursor e garante que o tempo não ficou pausado.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Tooltip("Nome da cena do jogo (precisa estar na lista de cenas do Build Profiles)")]
    public string cenaDoJogo = "SampleScene";

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Começa o jogo.</summary>
    public void Jogar()
    {
        if (!Application.CanStreamedLevelBeLoaded(cenaDoJogo))
        {
            Debug.LogWarning($"MenuPrincipal: a cena '{cenaDoJogo}' não existe ou não está no Build Profiles " +
                             "(File → Build Profiles → Scene List).", this);
            return;
        }
        SceneManager.LoadScene(cenaDoJogo);
    }

    /// <summary>Fecha o jogo (no Editor, só para o Play).</summary>
    public void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
