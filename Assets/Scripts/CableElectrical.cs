using UnityEngine;

/// <summary>
/// Handles the electrical behaviour of a jumper cable on the breadboard.
/// Attach this alongside CableRightAngleHybridTube on the same GameObject.
/// It reads endA and endB from the physics script — no duplication needed.
/// 
/// A cable simply bridges two nodes: whatever voltage is on one end
/// is propagated to the other, making them electrically equivalent.
/// </summary>
[RequireComponent(typeof(CableRightAngleHybridTube))]
public class CableElectrical : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugInfo = true;

    // Detected sockets at each end
    private BreadboardSocket socketA;
    private BreadboardSocket socketB;

    // The nodes we are currently bridging
    private BreadboardNode nodeA;
    private BreadboardNode nodeB;

    // Reference to the physics script so we can read its endpoints
    private CableRightAngleHybridTube _physics;

    void Awake()
    {
        _physics = GetComponent<CableRightAngleHybridTube>();
    }

    void Update()
    {
        DetectSocketConnections();
        PropagatVoltage();
    }

    void DetectSocketConnections()
    {
        BreadboardSocket newA = FindNearestSocket(_physics.endA);
        BreadboardSocket newB = FindNearestSocket(_physics.endB);

        if (newA != socketA || newB != socketB)
        {
            // Clear old bridging before we reassign
            ClearBridge();

            socketA = newA;
            socketB = newB;
            nodeA = socketA?.node;
            nodeB = socketB?.node;

            if (showDebugInfo)
                Debug.Log($"[Cable] EndA: {(socketA != null ? "connected" : "none")}, " +
                          $"EndB: {(socketB != null ? "connected" : "none")}");
        }
    }

    /// <summary>
    /// Propagates voltage from whichever end is a voltage source to the other.
    /// If both ends are floating, nothing happens.
    /// If both ends have a source, the higher voltage wins (short circuit approximation).
    /// </summary>
    void PropagatVoltage()
    {
        if (nodeA == null || nodeB == null) return;

        bool aSourced = nodeA.isVoltageSource;
        bool bSourced = nodeB.isVoltageSource;

        if (aSourced && !bSourced)
        {
            nodeB.voltage = nodeA.voltage;
        }
        else if (bSourced && !aSourced)
        {
            nodeA.voltage = nodeB.voltage;
        }
        else if (aSourced && bSourced)
        {
            // Both driven — higher voltage propagates (simple approximation)
            if (nodeA.voltage >= nodeB.voltage)
                nodeB.voltage = nodeA.voltage;
            else
                nodeA.voltage = nodeB.voltage;
        }
        // If neither is sourced, do nothing — both float at 0
    }

    /// <summary>
    /// Clears any voltage this cable has bridged onto non-source nodes,
    /// so pulling out a cable doesn't leave phantom voltages behind.
    /// </summary>
    void ClearBridge()
    {
        // Only reset the node if it was not itself a source
        // (don't zero out a battery's node just because a cable was unplugged)
        if (nodeA != null && !nodeA.isVoltageSource)
            nodeA.voltage = 0f;

        if (nodeB != null && !nodeB.isVoltageSource)
            nodeB.voltage = 0f;
    }

    void OnDestroy()
    {
        ClearBridge();
    }

    BreadboardSocket FindNearestSocket(Transform tip)
    {
        if (tip == null) return null;

        float snapRadius = 0.005f;
        Collider[] hits = Physics.OverlapSphere(tip.position, snapRadius);

        foreach (var hit in hits)
        {
            BreadboardSocket socket = hit.GetComponentInParent<BreadboardSocket>();
            if (socket != null)
                return socket;
        }

        return null;
    }
}