using UnityEngine;

[RequireComponent(typeof(CableRightAngleHybridTube))]
public class CableElectrical : MonoBehaviour, ITwoTerminalComponent
{
    [Header("Debug")]
    public bool showDebugInfo = false;

    public BreadboardNode NodeA { get; private set; }
    public BreadboardNode NodeB { get; private set; }
    public float OhmsValue => 0.001f;

    private BreadboardSocket _socketA;
    private BreadboardSocket _socketB;
    private CableRightAngleHybridTube _physics;

    void Awake()
    {
        _physics = GetComponent<CableRightAngleHybridTube>();
    }

    void Start()
    {
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterComponent(this);
        else
            Debug.LogError("[Cable] CircuitSolver not found!");
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
        if (_physics == null) return;

        BreadboardSocket newA = FindNearestSocket(_physics.endA);
        BreadboardSocket newB = FindNearestSocket(_physics.endB);

        if (newA != _socketA || newB != _socketB)
        {
            _socketA = newA;
            _socketB = newB;
            NodeA = _socketA?.node;
            NodeB = _socketB?.node;

            if (showDebugInfo)
                Debug.Log($"[Cable] Connection changed — " +
                          $"EndA: {(newA != null ? newA.name : "disconnected")} " +
                          $"({(NodeA != null ? $"node OK, {NodeA.solvedVoltage:F2}V" : "no node")}) | " +
                          $"EndB: {(newB != null ? newB.name : "disconnected")} " +
                          $"({(NodeB != null ? $"node OK, {NodeB.solvedVoltage:F2}V" : "no node")})");
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
        if (_physics == null) return;
        if (_physics.endA != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(_physics.endA.position, 0.005f); }
        if (_physics.endB != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(_physics.endB.position, 0.005f); }
    }
}