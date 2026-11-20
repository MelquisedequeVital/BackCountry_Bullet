using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Cria um EventSystem (com o módulo do Input System novo) se a cena não tiver nenhum.
/// Sem EventSystem os botões das telas não recebem clique. Fica no prefab FluxoDoJogo,
/// assim as telas de pausa, morte e vitória funcionam em qualquer cena de jogo.
/// </summary>
public class GarantirEventSystem : MonoBehaviour
{
    void Awake()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
