using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    private List<Camera> _cameras = new List<Camera>();
    private int _currentIndex = 0;

    void Start()
    {
        _cameras = GetComponentsInChildren<Camera>(includeInactive: true)
            .OrderBy(c => c.gameObject.name)
            .ToList();

        if (_cameras.Count == 0)
        {
            Debug.LogError("CameraSwitcher: No cameras found in children.");
            return;
        }

        Debug.Log($"CameraSwitcher: Found {_cameras.Count} camera(s):");
        for (int i = 0; i < _cameras.Count; i++)
            Debug.Log($"  [{i}] {_cameras[i].gameObject.name}");

        for (int i = 0; i < _cameras.Count; i++)
            _cameras[i].gameObject.SetActive(i == 0);

        Debug.Log($"CameraSwitcher: Ready. Active camera: '{_cameras[0].gameObject.name}'");
    }

    void Update()
    {
        if (_cameras.Count == 0) return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("CameraSwitcher: Space detected");
            SwitchToNext();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("CameraSwitcher: Left click detected");
            SwitchToNext();
        }
    }

    void SwitchToNext()
    {
        _cameras[_currentIndex].gameObject.SetActive(false);
        _currentIndex = (_currentIndex + 1) % _cameras.Count;
        _cameras[_currentIndex].gameObject.SetActive(true);

        Debug.Log($"CameraSwitcher: Switched to '{_cameras[_currentIndex].gameObject.name}' ({_currentIndex + 1}/{_cameras.Count})");
    }
}
