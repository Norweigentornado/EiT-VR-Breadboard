using UnityEngine;

/// <summary>
/// LED component for breadboard simulation.
/// Has an anode (+) and cathode (-) leg.
/// Lights up only when anode node has higher voltage than cathode (correct polarity).
/// Uses a Point Light to illuminate when powered.
/// </summary>
public class LEDComponent : MonoBehaviour
{
    [Header("LED Properties")]
    public Color ledColor = Color.red;
    [Tooltip("Minimum voltage across the LED needed to light it (forward voltage). Typically 1.8-3.3V.")]
    public float forwardVoltage = 2f;

    [Header("Leg Transforms")]
    [Tooltip("Longer leg — positive/anode")]
    public Transform anodeLegTip;
    [Tooltip("Shorter leg — negative/cathode")]
    public Transform cathodeLegTip;

    [Header("Light Settings")]
    public float litIntensity = 1.5f;
    public float lightRange = 0.05f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Detected sockets
    private BreadboardSocket anodeSocket;
    private BreadboardSocket cathodeSocket;

    // The point light — found or created at runtime
    private Light pointLight;

    private bool isLit = false;

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
        // Look for an existing Light child, or create one
        pointLight = GetComponentInChildren<Light>();

        if (pointLight == null)
        {
            GameObject lightGO = new GameObject("LEDLight");
            lightGO.transform.SetParent(transform);
            lightGO.transform.localPosition = Vector3.zero;
            pointLight = lightGO.AddComponent<Light>();
            pointLight.type = LightType.Point;
        }

        pointLight.color = ledColor;
        pointLight.range = lightRange;
        pointLight.intensity = 0f;
        pointLight.enabled = false;
    }

    void DetectSocketConnections()
    {
        BreadboardSocket newAnode = FindNearestSocket(anodeLegTip);
        BreadboardSocket newCathode = FindNearestSocket(cathodeLegTip);

        if (newAnode != anodeSocket || newCathode != cathodeSocket)
        {
            anodeSocket = newAnode;
            cathodeSocket = newCathode;

            if (showDebugInfo)
                Debug.Log($"[LED] Anode: {(anodeSocket != null ? "connected" : "none")}, " +
                          $"Cathode: {(cathodeSocket != null ? "connected" : "none")}");
        }
    }

    void EvaluateLED()
    {
        bool shouldLight = false;

        if (anodeSocket != null && cathodeSocket != null)
        {
            BreadboardNode anodeNode = anodeSocket.node;
            BreadboardNode cathodeNode = cathodeSocket.node;

            if (anodeNode != null && cathodeNode != null)
            {
                float voltageDrop = anodeNode.voltage - cathodeNode.voltage;

                // Only light if forward biased with enough voltage
                if (voltageDrop >= forwardVoltage)
                {
                    shouldLight = true;

                    if (showDebugInfo && !isLit)
                        Debug.Log($"[LED] Lit! Voltage drop: {voltageDrop}V");
                }
                else if (showDebugInfo && isLit)
                {
                    if (voltageDrop < 0)
                        Debug.Log($"[LED] Reverse biased ({voltageDrop}V) — won't light.");
                    else
                        Debug.Log($"[LED] Insufficient voltage ({voltageDrop}V, need {forwardVoltage}V).");
                }
            }
        }

        if (shouldLight != isLit)
            SetLit(shouldLight);
    }

    void SetLit(bool on)
    {
        isLit = on;
        pointLight.enabled = on;
        pointLight.intensity = on ? litIntensity : 0f;
    }

    void OnDestroy()
    {
        SetLit(false);
    }

    BreadboardSocket FindNearestSocket(Transform tip)
    {
        if (tip == null) return null;

        float snapRadius = 0.005f;
        Collider[] hits = Physics.OverlapSphere(tip.position, snapRadius);

        foreach (var hit in hits)
        {
            BreadboardSocket socket = hit.GetComponentInParent<BreadboardSocket>();
            if (socket != null)
                return socket;
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (anodeLegTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(anodeLegTip.position, 0.005f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(anodeLegTip.position + Vector3.up * 0.01f, "A+");
#endif
        }

        if (cathodeLegTip != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(cathodeLegTip.position, 0.005f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(cathodeLegTip.position + Vector3.up * 0.01f, "K-");
#endif
        }
    }
}