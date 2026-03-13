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
        _isSnapped = false; // picking it up un-snaps it
        _rotationWhenGrabbed = transform.rotation; // save rotation at grab time
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
        // Check every leg has a socket candidate in snap range
        foreach (var leg in legs)
        {
            if (FindNearest(leg.tip, snapRadius) == null)
                return;
        }

        // Compute average position offset from all legs to their nearest sockets
        Vector3 totalOffset = Vector3.zero;

        foreach (var leg in legs)
        {
            BreadboardSocket target = FindNearest(leg.tip, snapRadius);

            // Guard against null or NaN socket positions
            if (target == null) return;
            Vector3 offset = target.transform.position - leg.tip.position;
            if (float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z)) return;

            totalOffset += offset;
        }

        Vector3 averageOffset = totalOffset / legs.Count;

        // Final NaN guard before applying
        if (float.IsNaN(averageOffset.x) || float.IsNaN(averageOffset.y) || float.IsNaN(averageOffset.z))
        {
            Debug.LogWarning($"[ComponentSnapper] NaN offset detected on '{gameObject.name}', aborting snap.");
            return;
        }

        transform.position += averageOffset;

        // Restore the rotation the player had when holding the object
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