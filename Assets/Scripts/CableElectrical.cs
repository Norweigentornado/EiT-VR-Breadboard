using UnityEngine;

/// <summary>
/// Handles the electrical behaviour of a jumper cable.
/// A cable is simply a near-zero resistance between two nodes.
/// The CircuitSolver treats it as a short circuit, propagating
/// voltage naturally without this script writing anything directly.
/// </summary>
[RequireComponent(typeof(CableRightAngleHybridTube))]
public class CableElectrical : MonoBehaviour, ITwoTerminalComponent
{
    [Header("Debug")]
    public bool showDebugInfo = false;

    // Exposed so CircuitSolver can read them (same interface as ResistorComponent)
    public BreadboardNode NodeA { get; private set; }
    public BreadboardNode NodeB { get; private set; }

    // Cables are treated as a very small resistance — effectively a short
    public float OhmsValue => 0.001f;

    private BreadboardSocket _socketA;
    private BreadboardSocket _socketB;
    private CableRightAngleHybridTube _physics;

    void Awake()
    {
        _physics = GetComponent<CableRightAngleHybridTube>();
    }

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
        DetectSocketConnections();
    }

    void DetectSocketConnections()
    {
        BreadboardSocket newA = FindNearestSocket(_physics.endA);
        BreadboardSocket newB = FindNearestSocket(_physics.endB);

        if (newA != _socketA || newB != _socketB)
        {
            _socketA = newA;
            _socketB = newB;
            NodeA = _socketA?.node;
            NodeB = _socketB?.node;

            if (showDebugInfo)
                Debug.Log($"[Cable] A: {(NodeA != null ? "connected" : "none")}, " +
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
}