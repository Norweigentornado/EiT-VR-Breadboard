using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class TwoHandScaleBreadboard : MonoBehaviour
{
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 5f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor> interactors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>();

    private float initialDistance;
    private Vector3 initialScale;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        interactors.Add(args.interactorObject);
        Debug.Log($"Grabbed! Interactor count: {interactors.Count}");

        if (interactors.Count == 2)
        {
            Debug.Log("Two hands detected - scaling enabled");
            initialDistance = Vector3.Distance(
                interactors[0].transform.position,
                interactors[1].transform.position
            );
            initialScale = transform.localScale;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        interactors.Remove(args.interactorObject);
    }

    void Update()
    {
        if (interactors.Count == 2)
        {
            float currentDistance = Vector3.Distance(
                interactors[0].transform.position,
                interactors[1].transform.position
            );

            float scaleFactor = currentDistance / initialDistance;
            float newScale = Mathf.Clamp(initialScale.x * scaleFactor, minScale, maxScale);
            Debug.Log($"InitialDist: {initialDistance}, CurrentDist: {currentDistance}, ScaleFactor: {scaleFactor}, NewScale: {newScale}");
            transform.localScale = Vector3.one * newScale;
        }
    }
}