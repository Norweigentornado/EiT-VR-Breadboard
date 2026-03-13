using UnityEngine;

/// <summary>
/// Battery component. Sets source voltages on its two nodes so the
/// CircuitSolver can include them in the MNA solve each frame.
/// </summary>
public class BatteryComponent : MonoBehaviour
{
    [Header("Battery Properties")]
    public float voltage = 9f;

    [Header("Leg Transforms")]
    public Transform positiveLegTip;
    public Transform negativeLegTip;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Exposed so CircuitSolver can read them
    public BreadboardNode PositiveNode { get; private set; }
    public BreadboardNode NegativeNode { get; private set; }

    public BreadboardSocket positiveSocket;
    public BreadboardSocket negativeSocket;

    void OnEnable()
    {
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterBattery(this);
    }

    void OnDisable()
    {
        Disconnect();
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.UnregisterBattery(this);
    }

    void Update()
    {
        DetectSocketConnections();
    }

    void DetectSocketConnections()
    {
        // If sockets are manually assigned in the Inspector, skip physics detection
        if (positiveSocket != null && negativeSocket != null)
        {
            // Connect once if not already connected
            if (PositiveNode == null || NegativeNode == null)
                Connect();
            return;
        }

        // Otherwise use physics overlap detection
        BreadboardSocket newPos = FindNearestSocket(positiveLegTip);
        BreadboardSocket newNeg = FindNearestSocket(negativeLegTip);

        if (newPos != positiveSocket || newNeg != negativeSocket)
        {
            Disconnect();
            positiveSocket = newPos;
            negativeSocket = newNeg;

            if (positiveSocket != null && negativeSocket != null)
                Connect();
        }
    }

    void Connect()
    {
        PositiveNode = positiveSocket.node;
        NegativeNode = negativeSocket.node;

        PositiveNode.isVoltageSource = true;
        PositiveNode.sourceVoltage   = voltage;

        NegativeNode.isVoltageSource = true;
        NegativeNode.sourceVoltage   = 0f;

        // Register now that we have nodes
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterBattery(this);

        if (showDebugInfo)
            Debug.Log($"[Battery] Connected: +{voltage}V / GND");
    }

    void Disconnect()
    {
        if (PositiveNode != null)
        {
            PositiveNode.isVoltageSource = false;
            PositiveNode.sourceVoltage   = 0f;
            PositiveNode.solvedVoltage   = 0f;
        }

        if (NegativeNode != null)
        {
            NegativeNode.isVoltageSource = false;
            NegativeNode.sourceVoltage   = 0f;
            NegativeNode.solvedVoltage   = 0f;
        }

        PositiveNode = null;
        NegativeNode = null;

        if (showDebugInfo)
            Debug.Log("[Battery] Disconnected.");
    }

    /// <summary>Manual connection for testing without physics.</summary>
    public void ConnectToSockets(BreadboardSocket positive, BreadboardSocket negative)
    {
        Disconnect();
        positiveSocket = positive;
        negativeSocket = negative;
        Connect();
    }

    void OnDestroy() => Disconnect();

    BreadboardSocket FindNearestSocket(Transform tip)
    {
        if (tip == null) return null;

        Collider[] hits = Physics.OverlapSphere(tip.position, 0.005f);
        foreach (var hit in hits)
        {
            BreadboardSocket s = hit.GetComponentInParent<BreadboardSocket>();
            if (s != null) return s;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (positiveLegTip != null) { Gizmos.color = Color.red;  Gizmos.DrawWireSphere(positiveLegTip.position, 0.005f); }
        if (negativeLegTip != null) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(negativeLegTip.position, 0.005f); }
    }
}