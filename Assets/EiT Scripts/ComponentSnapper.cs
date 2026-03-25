using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ComponentSnapper : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        [Tooltip("The tip transform of this leg")]
        public Transform tip;
        [HideInInspector] public BreadboardSocket hoveredSocket;
    }

    [Header("Legs")]
    public List<Leg> legs = new List<Leg>();

    [Header("Distances")]
    public float hoverRadius = 0.012f;
    public float snapRadius = 0.006f;

    [Header("Highlight colours")]
    public Color hoverColor = Color.cyan;
    public Color defaultColor = Color.white;

    private XRGrabInteractable _grab;
    private bool _isHeld = false;
    private bool _isSnapped = false;

    // Saved world rotation at the moment the player grabs the object
    private Quaternion _rotationWhenGrabbed;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
        _isSnapped = false;
        _rotationWhenGrabbed = transform.rotation;

        // Reset all electrical components on this object
        foreach (var comp in GetComponents<ResistorComponent>())
            comp.ResetSnap();
    }

    void OnReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        TrySnap();
        ClearAllHighlights();
    }

    void Update()
    {
        if (!_isHeld) return;

        foreach (var leg in legs)
        {
            BreadboardSocket nearest = FindNearest(leg.tip, hoverRadius);

            if (nearest != leg.hoveredSocket)
            {
                if (leg.hoveredSocket != null)
                    SetSocketColor(leg.hoveredSocket, defaultColor);

                leg.hoveredSocket = nearest;

                if (leg.hoveredSocket != null)
                    SetSocketColor(leg.hoveredSocket, hoverColor);
            }
        }
    }

    void TrySnap()
    {
        foreach (var leg in legs)
        {
            if (FindNearest(leg.tip, snapRadius) == null)
                return;
        }

        Vector3 totalTargetPosition = Vector3.zero;

        foreach (var leg in legs)
        {
            BreadboardSocket target = FindNearest(leg.tip, snapRadius);
            if (target == null) return;

            Transform attachChild = target.transform.Find("Attach");
            Vector3 socketPoint = attachChild != null ? attachChild.position : target.transform.position;

            // Where should the root be so THIS leg tip sits at the socket point?
            Vector3 rootTarget = socketPoint + (transform.position - leg.tip.position);

            if (float.IsNaN(rootTarget.x) || float.IsNaN(rootTarget.y) || float.IsNaN(rootTarget.z)) return;

            totalTargetPosition += rootTarget;
        }

        Vector3 averageTarget = totalTargetPosition / legs.Count;

        if (float.IsNaN(averageTarget.x) || float.IsNaN(averageTarget.y) || float.IsNaN(averageTarget.z))
        {
            Debug.LogWarning($"[ComponentSnapper] NaN position detected on '{gameObject.name}', aborting snap.");
            return;
        }

        transform.position = averageTarget;
        transform.rotation = _rotationWhenGrabbed;

        _isSnapped = true;
        Debug.Log($"[ComponentSnapper] Snapped '{gameObject.name}' into board.");
    }

    BreadboardSocket FindNearest(Transform tip, float radius)
    {
        if (tip == null) return null;

        Collider[] hits = Physics.OverlapSphere(tip.position, radius);
        BreadboardSocket nearest = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            BreadboardSocket socket = hit.GetComponentInParent<BreadboardSocket>();
            if (socket == null) continue;

            float dist = Vector3.Distance(tip.position, socket.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = socket;
            }
        }

        return nearest;
    }

    void SetSocketColor(BreadboardSocket socket, Color color)
    {
        Renderer r = socket.GetComponentInChildren<Renderer>();
        if (r == null) return;

        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        r.SetPropertyBlock(block);
    }

    void ClearAllHighlights()
    {
        foreach (var leg in legs)
        {
            if (leg.hoveredSocket != null)
            {
                SetSocketColor(leg.hoveredSocket, defaultColor);
                leg.hoveredSocket = null;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        foreach (var leg in legs)
        {
            if (leg.tip == null) continue;
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(leg.tip.position, hoverRadius);
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(leg.tip.position, snapRadius);
        }
    }

}