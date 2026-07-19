using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DesaturateInputNotifier : MonoBehaviour
{
    public static event Action<float> FadeRequested;

    [SerializeField] private InputActionReference fadeAction;
    [SerializeField, Range(0f, 1f)] private float fadedSaturation = 0f;

    private void OnEnable()
    {
        if (fadeAction == null)
            return;

        fadeAction.action.performed += OnFadePerformed;
        fadeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (fadeAction == null)
            return;

        fadeAction.action.performed -= OnFadePerformed;
        fadeAction.action.Disable();
    }

    private void OnFadePerformed(InputAction.CallbackContext context)
    {
        FadeRequested?.Invoke(fadedSaturation);
    }
}
