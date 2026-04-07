using UnityEngine;

public class ResistorComponent : MonoBehaviour, ITwoTerminalComponent
{
    [Header("Resistance")]
    [Tooltip("Resistance in Ohms. Common values: 220, 330, 1000, 10000")]
    [field: SerializeField]
    public float OhmsValue { get; set; } = 220f;

    [Header("Leg Transforms")]
    public Transform legATip;
    public Transform legBTip;

    [Header("Detection")]
    [Tooltip("OverlapSphere radius for finding sockets. Must be >= ComponentSnapper.snapRadius")]
    public float detectRadius = 0.012f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    public BreadboardNode NodeA { get; private set; }
    public BreadboardNode NodeB { get; private set; }

    private BreadboardSocket _socketA;
    private BreadboardSocket _socketB;

    [Header("Test Override")]
    public BreadboardSocket testSocketA;
    public BreadboardSocket testSocketB;

    private BreadboardLogic _boardLogic;

    private bool _isSnapped = false;
    private Vector3 _snappedPosition;
    public float unSnapDistance = 0.01f;

    private float _nextDebugTime = 0f;
    private const float DEBUG_INTERVAL = 1f;

    void OnEnable() { }

    void OnDisable()
    {
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.UnregisterComponent(this);
    }

    void Start()
    {
        _boardLogic = FindObjectOfType<BreadboardLogic>();

        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterComponent(this);
        else
            Debug.LogError("[Resistor] CircuitSolver.Instance is null in Start!");
    }

    void Update()
    {
        DetectSockets();

        if (showDebugInfo && Time.time >= _nextDebugTime)
        {
            _nextDebugTime = Time.time + DEBUG_INTERVAL;
            Debug.Log($"[Resistor {OhmsValue}Ω] STATUS — snapped={_isSnapped} " +
                      $"SocketA={(_socketA != null ? _socketA.name : "NULL")} " +
                      $"SocketB={(_socketB != null ? _socketB.name : "NULL")} " +
                      $"NodeA={(NodeA != null ? $"{NodeA.solvedVoltage:F2}V" : "NULL")} " +
                      $"NodeB={(NodeB != null ? $"{NodeB.solvedVoltage:F2}V" : "NULL")} " +
                      $"pos={transform.position}");

            // Always probe sockets even when snapped so we can see what's around
            FindNearestSocket(legATip);
            FindNearestSocket(legBTip);
        }
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

                if (showDebugInfo)
                    Debug.Log($"[Resistor {OhmsValue}Ω] TEST OVERRIDE — " +
                              $"SocketA={_socketA.name} ({(NodeA != null ? $"node OK, {NodeA.solvedVoltage:F2}V" : "no node")}) | " +
                              $"SocketB={_socketB.name} ({(NodeB != null ? $"node OK, {NodeB.solvedVoltage:F2}V" : "no node")})");
            }
            return;
        }

        // If snapped, only unlock if the component has moved significantly
        if (_isSnapped)
        {
            if (Vector3.Distance(transform.position, _snappedPosition) > unSnapDistance)
            {
                _isSnapped = false;
                _socketA = null;
                _socketB = null;
                NodeA = null;
                NodeB = null;

                if (showDebugInfo)
                    Debug.Log($"[Resistor {OhmsValue}Ω] Unsnapped — nodes cleared");
            }
            return;
        }

        BreadboardSocket newA = FindNearestSocket(legATip);
        BreadboardSocket newB = FindNearestSocket(legBTip);

        if (showDebugInfo)
            Debug.Log($"[Resistor {OhmsValue}Ω] Scanning — " +
                      $"LegA tip: {legATip?.position} hits: {(newA != null ? newA.name : "none")} | " +
                      $"LegB tip: {legBTip?.position} hits: {(newB != null ? newB.name : "none")}");

        if (newA != null && newB == null)
        {
            newB = InferAdjacentSocket(newA, rowOffset: 1);
            if (showDebugInfo && newB != null)
                Debug.Log($"[Resistor {OhmsValue}Ω] Inferred SocketB={newB.name} from SocketA");
        }
        else if (newB != null && newA == null)
        {
            newA = InferAdjacentSocket(newB, rowOffset: -1);
            if (showDebugInfo && newA != null)
                Debug.Log($"[Resistor {OhmsValue}Ω] Inferred SocketA={newA.name} from SocketB");
        }

        if (newA != null && newB != null)
        {
            _socketA = newA;
            _socketB = newB;
            NodeA = _socketA.node;
            NodeB = _socketB.node;
            _isSnapped = true;
            _snappedPosition = transform.position;

            if (showDebugInfo)
                Debug.Log($"[Resistor {OhmsValue}Ω] SNAPPED — " +
                          $"SocketA={_socketA.name} ({(NodeA != null ? $"node OK, {NodeA.solvedVoltage:F2}V" : "no node")}) | " +
                          $"SocketB={_socketB.name} ({(NodeB != null ? $"node OK, {NodeB.solvedVoltage:F2}V" : "no node")})");
        }
    }

    public void ResetSnap()
    {
        _isSnapped = false;
        _socketA = null;
        _socketB = null;
        NodeA = null;
        NodeB = null;

        if (showDebugInfo)
            Debug.Log($"[Resistor {OhmsValue}Ω] Snap reset externally");
    }

    BreadboardSocket InferAdjacentSocket(BreadboardSocket known, int rowOffset)
    {
        if (_boardLogic == null)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[Resistor {OhmsValue}Ω] Cannot infer socket — BreadboardLogic not found");
            return null;
        }

        var coords = _boardLogic.GetSocketCoords(known);
        if (coords == null)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[Resistor {OhmsValue}Ω] Cannot infer socket — coords not found for {known.name}");
            return null;
        }

        return _boardLogic.GetSocket(coords.Value.row + rowOffset, coords.Value.col);
    }

    BreadboardSocket FindNearestSocket(Transform tip)
    {
        if (tip == null) return null;

        Collider[] hits = Physics.OverlapSphere(tip.position, detectRadius);

        if (showDebugInfo)
        {
            Debug.Log($"[Resistor {OhmsValue}Ω] OverlapSphere at {tip.position} r={detectRadius} — {hits.Length} collider(s) hit");
            foreach (var hit in hits)
            {
                BreadboardSocket s = hit.GetComponentInParent<BreadboardSocket>();
                float dist = Vector3.Distance(tip.position, hit.transform.position);
                Debug.Log($"  hit: '{hit.gameObject.name}' (layer={LayerMask.LayerToName(hit.gameObject.layer)}) " +
                          $"dist={dist:F4} socket={(s != null ? s.name : "NONE")}");
            }
        }

        BreadboardSocket nearest = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            BreadboardSocket s = hit.GetComponentInParent<BreadboardSocket>();
            if (s == null) continue;

            float dist = Vector3.Distance(tip.position, s.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = s;
            }
        }

        if (showDebugInfo && nearest == null && hits.Length > 0)
            Debug.LogWarning($"[Resistor {OhmsValue}Ω] {hits.Length} colliders hit but NONE had a BreadboardSocket parent!");

        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        if (legATip != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(legATip.position, detectRadius); }
        if (legBTip != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(legBTip.position, detectRadius); }
    }
}