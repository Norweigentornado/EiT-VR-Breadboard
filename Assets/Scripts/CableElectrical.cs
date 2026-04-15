using UnityEngine;

[RequireComponent(typeof(CableRightAngleHybridTube))]
public class CableElectrical : MonoBehaviour, ITwoTerminalComponent
{
    [Header("Detection")]
    [Tooltip("OverlapSphere radius for finding sockets")]
    public float detectRadius = 0.012f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    public BreadboardNode NodeA { get; private set; }
    public BreadboardNode NodeB { get; private set; }
    public float OhmsValue => 0.001f;

    private BreadboardSocket _socketA;
    private BreadboardSocket _socketB;
    private CableRightAngleHybridTube _physics;
    private CableFlowVisualizer _flowVis;

    private float _nextDebugTime = 0f;
    private const float DEBUG_INTERVAL = 1f;

    void Awake()
    {
        _physics = GetComponent<CableRightAngleHybridTube>();
        _flowVis = GetComponent<CableFlowVisualizer>();
        if (_flowVis == null)
            _flowVis = GetComponentInChildren<CableFlowVisualizer>();
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
        UpdateFlowVisualizer();

        if (showDebugInfo && Time.time >= _nextDebugTime)
        {
            _nextDebugTime = Time.time + DEBUG_INTERVAL;
            Debug.Log($"[Cable] STATUS — " +
                      $"EndA pos={(_physics?.endA != null ? _physics.endA.position.ToString() : "NULL")} " +
                      $"EndB pos={(_physics?.endB != null ? _physics.endB.position.ToString() : "NULL")} " +
                      $"SocketA={(_socketA != null ? _socketA.name : "NULL")} " +
                      $"SocketB={(_socketB != null ? _socketB.name : "NULL")} " +
                      $"NodeA={(NodeA != null ? $"{NodeA.solvedVoltage:F2}V" : "NULL")} " +
                      $"NodeB={(NodeB != null ? $"{NodeB.solvedVoltage:F2}V" : "NULL")}");

            // Always probe so we can see what's around the tips
            FindNearestSocket(_physics?.endA);
            FindNearestSocket(_physics?.endB);
        }
    }

    void UpdateFlowVisualizer()
    {
        if (_flowVis == null) return;

        if (NodeA == null || NodeB == null || CircuitSolver.Instance == null)
        {
            _flowVis.hasCurrent = false;
            _flowVis.current = 0f;
            return;
        }

        // Get the actual solved current through this cable (Amps).
        // This accounts for all resistance in the circuit.
        float current = CircuitSolver.Instance.GetCurrent(this);

        bool flowing = Mathf.Abs(current) > _flowVis.minVisibleCurrent;

        _flowVis.hasCurrent = flowing;
        _flowVis.current = current;
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

        Collider[] hits = Physics.OverlapSphere(tip.position, detectRadius);

        if (showDebugInfo && Time.time >= _nextDebugTime - 0.01f)
        {
            Debug.Log($"[Cable] OverlapSphere at {tip.position} r={detectRadius} — {hits.Length} collider(s) hit");
            foreach (var hit in hits)
            {
                bool isSelf = hit.transform.IsChildOf(transform) || hit.transform == transform;
                BreadboardSocket s = hit.GetComponentInParent<BreadboardSocket>();
                float dist = Vector3.Distance(tip.position, hit.transform.position);
                Debug.Log($"  hit: '{hit.gameObject.name}' (layer={LayerMask.LayerToName(hit.gameObject.layer)}) " +
                          $"dist={dist:F4} socket={(s != null ? s.name : "NONE")} self={isSelf}");
            }
        }

        BreadboardSocket nearest = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // Skip colliders belonging to this component
            if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                continue;

            BreadboardSocket s = hit.GetComponentInParent<BreadboardSocket>();
            if (s == null) continue;

            float dist = Vector3.Distance(tip.position, s.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = s;
            }
        }

        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        if (_physics == null) return;
        if (_physics.endA != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(_physics.endA.position, detectRadius); }
        if (_physics.endB != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(_physics.endB.position, detectRadius); }
    }
}