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

    [Header("Test Override")]
    public BreadboardSocket testSocketA;
    public BreadboardSocket testSocketB;

    void OnEnable()
    {
       
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

    void Start()
    {
        Debug.Log($"[Resistor] Start called. Solver exists: {CircuitSolver.Instance != null}");
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterComponent(this);
        else
            Debug.LogError("[Resistor] CircuitSolver.Instance is null in Start! Check execution order.");
    }

    void DetectSockets()
    {
        // Test override
        if (testSocketA != null && testSocketB != null)
        {
            if (NodeA == null || NodeB == null)
            {
                _socketA = testSocketA;
                _socketB = testSocketB;
                NodeA = _socketA.node;
                NodeB = _socketB.node;
            }
            return;
        }

        // Physics detection
        BreadboardSocket newA = FindNearestSocket(legATip);
        BreadboardSocket newB = FindNearestSocket(legBTip);

        if (newA != _socketA || newB != _socketB)
        {
            _socketA = newA;
            _socketB = newB;
            NodeA = _socketA?.node;
            NodeB = _socketB?.node;
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