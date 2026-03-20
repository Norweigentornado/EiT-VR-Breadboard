using System.Collections.Generic;
using UnityEngine;

public class CableFlowVisualizer : MonoBehaviour
{
    [Header("References")]
    public CableRightAngleHybridTube cable;

    [Header("Flow State")]
    public bool hasCurrent = false;
    public float current = 0f;   // positive or negative
    public float minVisibleCurrent = 0.001f;

    [Header("Dot Visuals")]
    public GameObject dotPrefab;
    public int dotCount = 12;
    public float dotScale = 0.003f;

    [Header("Animation")]
    public float speedMultiplier = 0.5f;
    public float spacing = 0.08f; // normalized spacing offset

    readonly List<Transform> dots = new();
    readonly List<Vector3> pathPoints = new();

    float flowOffset = 0f;

    void Start()
    {
        EnsureDots();
    }

    void Update()
    {
        EnsureDots();

        bool visible = hasCurrent && Mathf.Abs(current) > minVisibleCurrent;

        for (int i = 0; i < dots.Count; i++)
            dots[i].gameObject.SetActive(visible);

        if (!visible || cable == null)
            return;

        BuildCablePath(pathPoints);

        if (pathPoints.Count < 2)
            return;

        float direction = Mathf.Sign(current);
        float speed = Mathf.Abs(current) * speedMultiplier;

        flowOffset += Time.deltaTime * speed * direction;

        for (int i = 0; i < dots.Count; i++)
        {
            float t = Repeat01(flowOffset - i * spacing);
            Vector3 pos = EvaluatePath(pathPoints, t);
            Vector3 offsetDir = cable.boardNormal.normalized * (cable.wireDiameter * cable.cableScale * 0.6f);

            dots[i].position = pos + offsetDir;
            dots[i].localScale = Vector3.one * dotScale;
        }
    }

    void EnsureDots()
    {
        if (dotPrefab == null) return;

        while (dots.Count < dotCount)
        {
            GameObject go = Instantiate(dotPrefab, transform);
            go.name = $"FlowDot_{dots.Count}";
            dots.Add(go.transform);
        }

        while (dots.Count > dotCount)
        {
            Transform last = dots[dots.Count - 1];
            if (Application.isPlaying) Destroy(last.gameObject);
            else DestroyImmediate(last.gameObject);
            dots.RemoveAt(dots.Count - 1);
        }
    }

    void BuildCablePath(List<Vector3> points)
    {
        points.Clear();

        if (cable.endA == null || cable.endB == null)
            return;

        Vector3 up = cable.boardNormal.sqrMagnitude < 1e-6f
            ? Vector3.up
            : cable.boardNormal.normalized;

        float scaledLift = cable.liftHeight * cable.cableScale;
        float scaledBend = cable.bendRadius * cable.cableScale;

        Vector3 a = cable.endA.position;
        Vector3 b = cable.endB.position;

        Vector3 aUp = a + up * scaledLift;
        Vector3 bUp = b + up * scaledLift;

        Vector3 across = bUp - aUp;
        float acrossLen = across.magnitude;
        if (acrossLen < 1e-5f) return;

        Vector3 acrossDir = across / acrossLen;

        float r = Mathf.Max(0f, scaledBend);
        r = Mathf.Min(r, scaledLift * 0.95f);
        r = Mathf.Min(r, acrossLen * 0.45f);

        if (r <= 1e-6f)
        {
            points.Add(a);
            points.Add(aUp);
            points.Add(bUp);
            points.Add(b);
            return;
        }

        Vector3 aVertEnd = aUp - up * r;
        Vector3 aAcrossStart = aUp + acrossDir * r;

        Vector3 bAcrossEnd = bUp - acrossDir * r;
        Vector3 bVertStart = bUp - up * r;

        points.Add(a);
        points.Add(aVertEnd);

        AddBezier(points, aVertEnd, aAcrossStart, up, acrossDir, r, cable.bendSamples, cable.kappa);

        points.Add(bAcrossEnd);

        AddBezier(points, bAcrossEnd, bVertStart, acrossDir, -up, r, cable.bendSamples, cable.kappa);

        points.Add(b);
    }

    static void AddBezier(List<Vector3> pts, Vector3 p0, Vector3 p3, Vector3 t0, Vector3 t3, float radius, int samples, float kappa)
    {
        t0 = t0.normalized;
        t3 = t3.normalized;

        float h = radius * Mathf.Max(0f, kappa);
        Vector3 p1 = p0 + t0 * h;
        Vector3 p2 = p3 - t3 * h;

        int n = Mathf.Max(4, samples);
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)(n - 1);
            pts.Add(Cubic(p0, p1, p2, p3, t));
        }

        pts.Add(p3);
    }

    static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return (u * u * u) * p0
             + (3f * u * u * t) * p1
             + (3f * u * t * t) * p2
             + (t * t * t) * p3;
    }

    static float Repeat01(float t)
    {
        t %= 1f;
        if (t < 0f) t += 1f;
        return t;
    }

    static Vector3 EvaluatePath(List<Vector3> pts, float t)
    {
        if (pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];

        float totalLength = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
            totalLength += Vector3.Distance(pts[i], pts[i + 1]);

        if (totalLength < 1e-5f) return pts[0];

        float target = t * totalLength;
        float accum = 0f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            float segLen = Vector3.Distance(pts[i], pts[i + 1]);
            if (accum + segLen >= target)
            {
                float localT = (target - accum) / segLen;
                return Vector3.Lerp(pts[i], pts[i + 1], localT);
            }
            accum += segLen;
        }

        return pts[pts.Count - 1];
    }
}