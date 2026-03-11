using UnityEngine;

/// <summary>
/// LED component. Reads solved node voltages from the CircuitSolver
/// and dims the point light proportionally to the current through it.
/// 
/// Forward voltage: minimum voltage drop anode→cathode to emit any light.
/// At exactly forwardVoltage the LED is minimally lit.
/// Above it, brightness scales with current (I = (Vdrop - Vf) / internalResistance).
/// </summary>
public class LEDComponent : MonoBehaviour
{
    [Header("LED Properties")]
    public Color  ledColor         = Color.red;
    [Tooltip("Minimum forward voltage to emit light (red≈2V, blue/white≈3V)")]
    public float  forwardVoltage   = 2f;
    [Tooltip("Internal resistance used to calculate current once forward voltage is exceeded (Ohms)")]
    public float  internalResistance = 68f;
    [Tooltip("Current (Amps) at which the LED reaches maximum brightness")]
    public float  maxCurrent       = 0.02f; // 20 mA — typical LED max

    [Header("Leg Transforms")]
    [Tooltip("Longer leg — anode (+)")]
    public Transform anodeLegTip;
    [Tooltip("Shorter leg — cathode (-)")]
    public Transform cathodeLegTip;

    [Header("Light Settings")]
    public float maxLitIntensity = 2f;
    public float lightRange      = 0.05f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    private BreadboardSocket _anodeSocket;
    private BreadboardSocket _cathodeSocket;
    private Light            _pointLight;
    private bool             _isLit = false;

    void Start()
    {
        SetupLight();
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

        _pointLight.color     = ledColor;
        _pointLight.range     = lightRange;
        _pointLight.intensity = 0f;
        _pointLight.enabled   = false;
    }

    void DetectSocketConnections()
    {
        BreadboardSocket newAnode   = FindNearestSocket(anodeLegTip);
        BreadboardSocket newCathode = FindNearestSocket(cathodeLegTip);

        if (newAnode != _anodeSocket || newCathode != _cathodeSocket)
        {
            _anodeSocket   = newAnode;
            _cathodeSocket = newCathode;
        }
    }

    void EvaluateLED()
    {
        if (_anodeSocket?.node == null || _cathodeSocket?.node == null)
        {
            SetBrightness(0f);
            return;
        }

        float anodeV   = _anodeSocket.node.solvedVoltage;
        float cathodeV = _cathodeSocket.node.solvedVoltage;
        float vDrop    = anodeV - cathodeV;

        if (vDrop < forwardVoltage)
        {
            // Reverse biased or insufficient voltage
            SetBrightness(0f);
            return;
        }

        // Current through LED: I = (Vdrop - Vf) / Rinternal
        float current    = (vDrop - forwardVoltage) / Mathf.Max(internalResistance, 0.1f);
        float brightness = Mathf.Clamp01(current / maxCurrent);

        SetBrightness(brightness);

        if (showDebugInfo)
            Debug.Log($"[LED] Vdrop={vDrop:F2}V  I={current * 1000f:F1}mA  brightness={brightness:P0}");
    }

    void SetBrightness(float t)
    {
        bool shouldBeOn = t > 0.001f;

        if (shouldBeOn != _isLit)
        {
            _isLit              = shouldBeOn;
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
        if (anodeLegTip   != null) { Gizmos.color = Color.red;  Gizmos.DrawWireSphere(anodeLegTip.position,   0.005f); }
        if (cathodeLegTip != null) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(cathodeLegTip.position, 0.005f); }
    }
}