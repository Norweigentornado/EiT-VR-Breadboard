using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InteractionModeToggle : MonoBehaviour
{
    [Header("Left Hand")]
    public XRRayInteractor leftRayInteractor;
    public XRDirectInteractor leftDirectInteractor;

    [Header("Right Hand")]
    public XRRayInteractor rightRayInteractor;
    public XRDirectInteractor rightDirectInteractor;

    [Header("Input")]
    public InputActionReference toggleAction; // e.g. Primary Button (X/A)

    private bool _useRay = true;

    void OnEnable()
    {
        if (toggleAction != null)
            toggleAction.action.performed += OnToggle;
    }

    void OnDisable()
    {
        if (toggleAction != null)
            toggleAction.action.performed -= OnToggle;
    }

    void Start() => ApplyMode();

    private void OnToggle(InputAction.CallbackContext ctx)
    {
        _useRay = !_useRay;
        ApplyMode();
        Debug.Log($"Interaction mode: {(_useRay ? "Ray Beam" : "Direct Hand")}");
    }

    private void ApplyMode()
    {
        // Ray interactors — active only in ray mode
        if (leftRayInteractor) leftRayInteractor.enabled = _useRay;
        if (rightRayInteractor) rightRayInteractor.enabled = _useRay;

        // Direct interactors — active only in hand mode
        if (leftDirectInteractor) leftDirectInteractor.enabled = !_useRay;
        if (rightDirectInteractor) rightDirectInteractor.enabled = !_useRay;
    }
}