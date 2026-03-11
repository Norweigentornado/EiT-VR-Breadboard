using UnityEngine;

/// <summary>
/// Resistor component for the breadboard.
/// Registers itself with CircuitSolver so its resistance is included in every solve.
/// 
/// Attach alongside ComponentSnapper and XRGrabInteractable.
/// Uses the same leg-tip + OverlapSphere pattern as Battery and LED.
/// </summary>
public class ResistorComponent : MonoBehaviour, ITwoTerminalComponent
{
    [Header("Resistance")]
    [Tooltip("Resistance in Ohms. Common values: 220, 330, 1000, 10000")]
    [field: SerializeField]
    public float OhmsValue { get; set; } = 220f;

    [Header("Leg Transforms")]
    public Transform legATip;
    public Transform legBTip;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // The nodes this resistor currently bridges
    public BreadboardNode NodeA { get; private set; }
    public BreadboardNode NodeB { get; private set; }

    private BreadboardSocket _socketA;
    private BreadboardSocket _socketB;

    void OnEnable()
    {
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterComponent(this);
    }

    void OnDisable()
    {
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.UnregisterComponent(this);
    }

    void Update()
    {
        DetectSockets();
    }

    void DetectSockets()
    {
        BreadboardSocket newA = FindNearestSocket(legATip);
        BreadboardSocket newB = FindNearestSocket(legBTip);

        if (newA != _socketA || newB != _socketB)
        {
            _socketA = newA;
            _socketB = newB;

            NodeA = _socketA?.node;
            NodeB = _socketB?.node;

            if (showDebugInfo)
                Debug.Log($"[Resistor {OhmsValue}Ω] A: {(NodeA != null ? "connected" : "none")}, " +
                          $"B: {(NodeB != null ? "connected" : "none")}");
        }
    }

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
        if (legATip != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(legATip.position, 0.005f);
        }
        if (legBTip != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(legBTip.position, 0.005f);
        }
    }
}