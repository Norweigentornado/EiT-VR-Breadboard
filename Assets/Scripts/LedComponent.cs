using UnityEngine;

public class LEDComponent : MonoBehaviour, ITwoTerminalComponent
{
    [Header("LED Properties")]
    public Color ledColor = Color.red;
    [Tooltip("Minimum forward voltage to emit light (red≈2V, blue/white≈3V)")]
    public float forwardVoltage = 2f;
    [Tooltip("Internal resistance in Ohms")]
    public float internalResistance = 68f;
    [Tooltip("Current (Amps) at which the LED reaches maximum brightness")]
    public float maxCurrent = 0.02f;

    [Header("Leg Transforms")]
    [Tooltip("Longer leg — anode (+)")]
    public Transform anodeLegTip;
    [Tooltip("Shorter leg — cathode (-)")]
    public Transform cathodeLegTip;

    [Header("Light Settings")]
    public float maxLitIntensity = 2f;
    public float lightRange = 0.05f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // ITwoTerminalComponent
    public BreadboardNode NodeA => _anodeSocket?.node;
    public BreadboardNode NodeB => _cathodeSocket?.node;
    public float OhmsValue => internalResistance + (forwardVoltage / maxCurrent);

    private BreadboardSocket _anodeSocket;
    private BreadboardSocket _cathodeSocket;
    private Light _pointLight;
    private bool _isLit = false;
    private bool _isSnapped = false;
    private Vector3 _snappedPosition;
    public float unSnapDistance = 0.01f;

    private BreadboardLogic _boardLogic;

    void Start()
    {
        SetupLight();
        _boardLogic = FindObjectOfType<BreadboardLogic>();

        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterComponent(this);
        else
            Debug.LogError("[LED] CircuitSolver not found!");
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
        EvaluateLED();
    }

    void SetupLight()
    {
        _pointLight = GetComponentInChildren<Light>();
        if (_pointLight == null)
        {
            var go = new GameObject("LEDLight");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            _pointLight = go.AddComponent<Light>();
            _pointLight.type = LightType.Point;
        }
        _pointLight.color = ledColor;
        _pointLight.range = lightRange;
        _pointLight.intensity = 0f;
        _pointLight.enabled = false;
    }

    void DetectSocketConnections()
    {
        // If already snapped, only unsnap if moved away
        if (_isSnapped)
        {
            if (Vector3.Distance(transform.position, _snappedPosition) > unSnapDistance)
            {
                _isSnapped = false;
                _anodeSocket = null;
                _cathodeSocket = null;
                if (showDebugInfo)
                    Debug.Log("[LED] Unsnapped — moved away");
            }
            return;
        }

        // Cathode is the physically detected leg
        BreadboardSocket newCathode = FindNearestSocket(cathodeLegTip);

        if (newCathode == null)
        {
            if (_cathodeSocket != null)
            {
                _cathodeSocket = null;
                _anodeSocket = null;
                if (showDebugInfo)
                    Debug.Log("[LED] Cathode disconnected");
            }
            return;
        }

        // Anode is inferred as the next row, same column as cathode
        BreadboardSocket newAnode = InferAdjacentSocket(newCathode, rowOffset: 1);

        if (newAnode == null)
        {
            if (showDebugInfo)
                Debug.Log($"[LED] Cathode found ({newCathode.name}) but could not infer anode row");
            return;
        }

        if (newAnode != _anodeSocket || newCathode != _cathodeSocket)
        {
            _cathodeSocket = newCathode;
            _anodeSocket = newAnode;
            _isSnapped = true;
            _snappedPosition = transform.position;

            if (showDebugInfo)
                Debug.Log($"[LED] SNAPPED — " +
                          $"Cathode={_cathodeSocket.name} ({(NodeB != null ? $"node OK, {NodeB.solvedVoltage:F2}V" : "no node")}) | " +
                          $"Anode={_anodeSocket.name} ({(NodeA != null ? $"node OK, {NodeA.solvedVoltage:F2}V" : "no node")})");
        }
    }

    BreadboardSocket InferAdjacentSocket(BreadboardSocket known, int rowOffset)
    {
        if (_boardLogic == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[LED] Cannot infer anode — BreadboardLogic not found");
            return null;
        }

        var coords = _boardLogic.GetSocketCoords(known);
        if (coords == null)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[LED] Cannot infer anode — coords not found for {known.name}");
            return null;
        }

        BreadboardSocket inferred = _boardLogic.GetSocket(coords.Value.row + rowOffset, coords.Value.col);

        if (showDebugInfo)
            Debug.Log($"[LED] Inferred anode from cathode {known.name} " +
                      $"at row={coords.Value.row} col={coords.Value.col} " +
                      $"→ row={coords.Value.row + rowOffset} = {(inferred != null ? inferred.name : "NOT FOUND")}");

        return inferred;
    }

    void EvaluateLED()
    {
        if (NodeA == null || NodeB == null)
        {
            SetBrightness(0f);
            return;
        }

        float vDrop = NodeA.solvedVoltage - NodeB.solvedVoltage;

        if (vDrop < forwardVoltage)
        {
            SetBrightness(0f);
            if (showDebugInfo)
                Debug.Log($"[LED] Not lit — Vdrop={vDrop:F2}V < Vf={forwardVoltage}V");
            return;
        }

        float current = (vDrop - forwardVoltage) / Mathf.Max(internalResistance, 0.1f);
        float brightness = Mathf.Clamp01(current / maxCurrent);
        SetBrightness(brightness);

        if (showDebugInfo)
            Debug.Log($"[LED] ON — Vdrop={vDrop:F2}V  I={current * 1000f:F1}mA  brightness={brightness:P0}");
    }

    void SetBrightness(float t)
    {
        bool shouldBeOn = t > 0.001f;
        if (shouldBeOn != _isLit)
        {
            _isLit = shouldBeOn;
            _pointLight.enabled = shouldBeOn;
        }
        if (shouldBeOn)
            _pointLight.intensity = Mathf.Lerp(0.1f, maxLitIntensity, t);
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
        if (cathodeLegTip != null) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(cathodeLegTip.position, 0.005f); }
        if (anodeLegTip != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(anodeLegTip.position, 0.005f); }

        // Show inferred anode socket if snapped
        if (_anodeSocket != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_anodeSocket.transform.position, 0.005f);
            Gizmos.DrawLine(cathodeLegTip.position, _anodeSocket.transform.position);
        }
    }
}