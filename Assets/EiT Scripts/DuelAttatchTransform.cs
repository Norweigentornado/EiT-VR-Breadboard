using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DualAttachTransform : MonoBehaviour
{
    [Tooltip("Used when grabbed by hand - e.g. center of component body")]
    public Transform handAttach;

    [Tooltip("Used when snapping to socket - e.g. midpoint between leg tips")]
    public Transform socketAttach;

    private XRGrabInteractable _grab;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        // Default to hand attach
        _grab.attachTransform = handAttach;

        _grab.selectEntered.AddListener(OnSelectEntered);
        _grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnSelectEntered);
        _grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            _grab.attachTransform = socketAttach;
        else
            _grab.attachTransform = handAttach;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        // When leaving a socket, reset to hand attach ready for next grab
        if (args.interactorObject is XRSocketInteractor)
            _grab.attachTransform = handAttach;
    }
}