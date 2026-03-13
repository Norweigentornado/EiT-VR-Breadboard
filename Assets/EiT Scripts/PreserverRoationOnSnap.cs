using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PreserveRotationOnSnap : MonoBehaviour
{
    private XRSocketInteractor socket;
    private Quaternion rotationBeforeSnap;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.hoverEntered.AddListener(OnHoverEnter);
        socket.selectEntered.AddListener(OnSnapped);
    }

    // Capture rotation just before the snap occurs
    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        rotationBeforeSnap = args.interactableObject.transform.rotation;
    }

    // Restore the captured rotation after snapping
    private void OnSnapped(SelectEnterEventArgs args)
    {
        // Wait one frame so XRI has finished positioning the object
        StartCoroutine(ApplyRotationNextFrame(args.interactableObject.transform));
    }

    private System.Collections.IEnumerator ApplyRotationNextFrame(Transform snappedObject)
    {
        yield return null;
        snappedObject.rotation = rotationBeforeSnap;
    }

    void OnDestroy()
    {
        if (socket != null)
        {
            socket.hoverEntered.RemoveListener(OnHoverEnter);
            socket.selectEntered.RemoveListener(OnSnapped);
        }
    }
}