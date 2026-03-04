using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TwoHandScaleBreadboard : MonoBehaviour
{
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 5f;
    [SerializeField] private float scaleSpeed = 2f;
    [SerializeField] private InputActionReference thumbstickAction;

    private XRGrabInteractable grabInteractable;
    private List<IXRSelectInteractor> interactors = new List<IXRSelectInteractor>();

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable == null)
            grabInteractable = GetComponentInParent<XRGrabInteractable>();

        if (grabInteractable == null)
        {
            Debug.LogError("No XRGrabInteractable found on " + gameObject.name);
            return;
        }

        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        interactors.Add(args.interactorObject);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        interactors.Remove(args.interactorObject);
    }

    void Update()
    {
        if (interactors.Count >= 1)
        {
            float scaleInput = 0f;

            if (thumbstickAction != null)
            {
                Vector2 thumbstick = thumbstickAction.action.ReadValue<Vector2>();
                scaleInput = thumbstick.y;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.uKey.isPressed)
                {
                    scaleInput = 1f;
                    Debug.Log("Scaling UP");
                }
                if (Keyboard.current.jKey.isPressed)
                {
                    scaleInput = -1f;
                    Debug.Log("Scaling DOWN");
                }
            }
            else
            {
                Debug.LogWarning("Keyboard.current is null!");
            }

            Debug.Log($"Interactors: {interactors.Count}, ScaleInput: {scaleInput}, CurrentScale: {transform.localScale.x}");

            if (Mathf.Abs(scaleInput) > 0.1f)
            {
                float newScale = Mathf.Clamp(
                    transform.localScale.x + scaleInput * scaleSpeed * Time.deltaTime,
                    minScale,
                    maxScale
                );
                Debug.Log($"Applying new scale: {newScale}");
                transform.localScale = Vector3.one * newScale;
            }
        }
        else
        {
            Debug.Log("No interactors - board not grabbed");
        }
    }
}