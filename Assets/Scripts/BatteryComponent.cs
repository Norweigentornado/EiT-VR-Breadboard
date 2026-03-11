using UnityEngine;

/// <summary>
/// Battery component for breadboard simulation.
/// Has two legs: positive (+) and negative (-).
/// When both legs are inserted into BreadboardSockets,
/// the battery injects a voltage into the node network.
/// </summary>
public class BatteryComponent : MonoBehaviour
{
    [Header("Battery Properties")]
    public float voltage = 9f; // Volts (e.g. 9V battery)

    [Header("Leg Sockets (assign in Inspector)")]
    [Tooltip("The socket this leg is physically inserted into (positive terminal)")]
    public BreadboardSocket positiveSocket;

    [Tooltip("The socket this leg is physically inserted into (negative terminal)")]
    public BreadboardSocket negativeSocket;

    [Header("Leg Transforms (for dummy object)")]
    [Tooltip("Transform at the tip of the positive leg")]
    public Transform positiveLegTip;

    [Tooltip("Transform at the tip of the negative leg")]
    public Transform negativeLegTip;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // The nodes this battery is currently connected to
    private BreadboardNode positiveNode;
    private BreadboardNode negativeNode;

    private bool isConnected = false;

    void Update()
    {
        DetectSocketConnections();
    }

    /// <summary>
    /// Checks if leg tips are overlapping with breadboard sockets.
    /// Call this each frame, or trigger it manually when the battery is placed.
    /// </summary>
    void DetectSocketConnections()
    {
        BreadboardSocket newPositive = FindNearestSocket(positiveLegTip);
        BreadboardSocket newNegative = FindNearestSocket(negativeLegTip);

        bool changed = (newPositive != positiveSocket || newNegative != negativeSocket);

        if (changed)
        {
            Disconnect();

            positiveSocket = newPositive;
            negativeSocket = newNegative;

            if (positiveSocket != null && negativeSocket != null)
                Connect();
        }
    }

    /// <summary>
    /// Finds the nearest BreadboardSocket within snap range of a given tip transform.
    /// </summary>
    BreadboardSocket FindNearestSocket(Transform tip)
    {
        if (tip == null) return null;

        float snapRadius = 0.005f; // 5mm snap distance
        Collider[] hits = Physics.OverlapSphere(tip.position, snapRadius);

        foreach (var hit in hits)
        {
            BreadboardSocket socket = hit.GetComponentInParent<BreadboardSocket>();
            if (socket != null)
                return socket;
        }

        return null;
    }

    /// <summary>
    /// Call this manually if you're assigning sockets directly (e.g. for testing
    /// without physics), instead of relying on DetectSocketConnections.
    /// </summary>
    public void ConnectToSockets(BreadboardSocket positive, BreadboardSocket negative)
    {
        Disconnect();

        positiveSocket = positive;
        negativeSocket = negative;

        Connect();
    }

    void Connect()
    {
        if (positiveSocket == null || negativeSocket == null) return;
        if (positiveSocket.node == null || negativeSocket.node == null) return;

        positiveNode = positiveSocket.node;
        negativeNode = negativeSocket.node;

        // Inject voltage into nodes
        positiveNode.voltage = voltage;
        negativeNode.voltage = 0f;

        positiveNode.isVoltageSource = true;
        negativeNode.isVoltageSource = true;

        isConnected = true;

        if (showDebugInfo)
            Debug.Log($"[Battery] Connected: +{voltage}V on node with {positiveNode.sockets.Count} sockets, GND on node with {negativeNode.sockets.Count} sockets.");
    }

    void Disconnect()
    {
        if (positiveNode != null)
        {
            positiveNode.voltage = 0f;
            positiveNode.isVoltageSource = false;
        }

        if (negativeNode != null)
        {
            negativeNode.voltage = 0f;
            negativeNode.isVoltageSource = false;
        }

        positiveNode = null;
        negativeNode = null;
        isConnected = false;

        if (showDebugInfo)
            Debug.Log("[Battery] Disconnected.");
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnDrawGizmosSelected()
    {
        // Visualise the snap radius around each leg tip in the Scene view
        if (positiveLegTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(positiveLegTip.position, 0.005f);
        }

        if (negativeLegTip != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(negativeLegTip.position, 0.005f);
        }
    }
}