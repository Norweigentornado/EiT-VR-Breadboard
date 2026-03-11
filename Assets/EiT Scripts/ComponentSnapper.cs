using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Add this to any breadboard component (Battery, LED, etc.) alongside an XRGrabInteractable.
/// 
/// While held:
///   - Each leg tip scans for the nearest socket within hover range
///   - The nearest candidate socket highlights (cyan)
///   - Any previously highlighted socket that is no longer nearest un-highlights
/// 
/// On release:
///   - If all leg tips have a candidate socket within snap range, the component
///     snaps so every leg tip aligns to its target socket hole
///   - Otherwise the component is just dropped normally
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class ComponentSnapper : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        [Tooltip("The tip transform of this leg (same one used by the electrical script)")]
        public Transform tip;

        [HideInInspector] public BreadboardSocket hoveredSocket;
    }

    [Header("Legs")]
    [Tooltip("Add one entry per leg. Order doesn't matter.")]
    public List<Leg> legs = new List<Leg>();

    [Header("Distances")]
    [Tooltip("How close a leg tip needs to be to a socket to start highlighting it (metres)")]
    public float hoverRadius = 0.012f;

    [Tooltip("How close ALL leg tips must be to their sockets to trigger a snap on release")]
    public float snapRadius = 0.006f;

    [Header("Highlight colours")]
    public Color hoverColor   = Color.cyan;
    public Color defaultColor = Color.white;

    // ── internal state ──────────────────────────────────────────────
    private XRGrabInteractable _grab;
    private bool _isHeld = false;

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

    // ── XR callbacks ────────────────────────────────────────────────

    void OnGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
    }

    void OnReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        TrySnap();
        ClearAllHighlights();
    }

    // ── per-frame hover scan ─────────────────────────────────────────

    void Update()
    {
        if (!_isHeld) return;

        foreach (var leg in legs)
        {
            BreadboardSocket nearest = FindNearest(leg.tip, hoverRadius);

            if (nearest != leg.hoveredSocket)
            {
                // Un-highlight old
                if (leg.hoveredSocket != null)
                    SetSocketColor(leg.hoveredSocket, defaultColor);

                // Highlight new
                leg.hoveredSocket = nearest;
                if (leg.hoveredSocket != null)
                    SetSocketColor(leg.hoveredSocket, hoverColor);
            }
        }
    }

    // ── snap logic ───────────────────────────────────────────────────

    void TrySnap()
    {
        // Every leg must have a candidate socket within snap range
        foreach (var leg in legs)
        {
            BreadboardSocket candidate = FindNearest(leg.tip, snapRadius);
            if (candidate == null)
                return; // at least one leg too far — abort snap
        }

        // All legs are close enough — compute the correction offset.
        // We average the per-leg offsets so the component sits as centred as possible.
        Vector3 totalOffset = Vector3.zero;

        foreach (var leg in legs)
        {
            BreadboardSocket target = FindNearest(leg.tip, snapRadius);
            totalOffset += (target.transform.position - leg.tip.position);
        }

        Vector3 averageOffset = totalOffset / legs.Count;
        transform.position += averageOffset;

        Debug.Log($"[ComponentSnapper] Snapped '{gameObject.name}' into board.");
    }

    // ── helpers ──────────────────────────────────────────────────────

    /// <summary>Finds the nearest BreadboardSocket to a tip within a given radius.</summary>
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

    /// <summary>Sets the highlight colour on a socket's renderer via MaterialPropertyBlock.</summary>
    void SetSocketColor(BreadboardSocket socket, Color color)
    {
        Renderer r = socket.GetComponentInChildren<Renderer>();
        if (r == null) return;

        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color); // URP — swap for "_Color" if using Built-in RP
        r.SetPropertyBlock(block);
    }

    /// <summary>Resets highlight on all currently hovered sockets.</summary>
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

    // ── Scene gizmos ─────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        foreach (var leg in legs)
        {
            if (leg.tip == null) continue;

            // Hover radius — yellow
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(leg.tip.position, hoverRadius);

            // Snap radius — green
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(leg.tip.position, snapRadius);
        }
    }
}