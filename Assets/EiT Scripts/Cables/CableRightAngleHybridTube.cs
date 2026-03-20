using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CableRightAngleHybridTube : MonoBehaviour
{
    [Header("Endpoints (socket insertion points)")]
    public Transform endA;
    public Transform endB;

    [Header("Pins")]
    public Transform pinA;
    public Transform pinB;
    public float pinDiameter = 0.001f;
    public float pinLength = 0.006f;
    public float pinLengthScale = 1f;

    [Header("Shape")]
    public Vector3 boardNormal = Vector3.up;
    public float liftHeight = 0.01f;
    public float bendRadius = 0.004f;
    [Range(6, 64)]
    public int bendSamples = 24;

    [Header("Tube quality")]
    [Range(6, 32)]
    public int tubeSides = 12;

    [Header("Thickness")]
    public float wireDiameter = 0.002f;

    [Header("Global Scale")]
    public float cableScale = 1f;

    [Header("Material")]
    public Material wireMaterial;

    [Header("Bezier")]
    [Tooltip("0.55228475 approximates a quarter circle well.")]
    public float kappa = 0.55228475f;

    Transform _partsRoot;
    Transform _straightUpA, _straightAcross, _straightDownB;
    MeshFilter _bendAFilter, _bendBFilter;
    MeshRenderer _bendARenderer, _bendBRenderer;

    readonly List<Vector3> _tmp = new();

    void OnEnable() => EnsureParts();
    void OnValidate() => EnsureParts();

    void LateUpdate()
    {
        if (!endA || !endB) return;
        EnsureParts();

        float scaledLift = liftHeight * cableScale;
        float scaledBend = bendRadius * cableScale;
        float scaledWire = wireDiameter * cableScale;
        float scaledPinDiameter = pinDiameter * cableScale;
        float scaledPinLength = pinLength * cableScale * pinLengthScale;

        ScalePin(pinA, scaledPinDiameter, scaledPinLength);
        ScalePin(pinB, scaledPinDiameter, scaledPinLength);

        Vector3 up = boardNormal.sqrMagnitude < 1e-6f ? Vector3.up : boardNormal.normalized;

        Vector3 a = endA.position;
        Vector3 b = endB.position;

        Vector3 aUp = a + up * scaledLift;
        Vector3 bUp = b + up * scaledLift;

        Vector3 across = bUp - aUp;
        float acrossLen = across.magnitude;
        if (acrossLen < 1e-5f) return;
        Vector3 acrossDir = across / acrossLen;

        float r = Mathf.Max(0f, scaledBend);
        r = Mathf.Min(r, scaledLift * 0.95f);
        r = Mathf.Min(r, acrossLen * 0.45f);

        float radius = scaledWire * 0.5f;

        if (r <= 1e-6f)
        {
            SetCylinderBetween(_straightUpA, a, aUp, scaledWire);
            SetCylinderBetween(_straightAcross, aUp, bUp, scaledWire);
            SetCylinderBetween(_straightDownB, bUp, b, scaledWire);

            SetBendActive(false);
            return;
        }

        Vector3 aVertEnd = aUp - up * r;
        Vector3 aAcrossStart = aUp + acrossDir * r;

        Vector3 bAcrossEnd = bUp - acrossDir * r;
        Vector3 bVertStart = bUp - up * r;

        SetCylinderBetween(_straightUpA, a, aVertEnd, scaledWire);
        SetCylinderBetween(_straightAcross, aAcrossStart, bAcrossEnd, scaledWire);
        SetCylinderBetween(_straightDownB, bVertStart, b, scaledWire);

        SetBendActive(true);

        BuildBezierPoints(_tmp, aVertEnd, aAcrossStart, t0: up, t3: acrossDir, radius: r, samples: bendSamples);
        ConvertPointsToLocal(_tmp, transform);
        GenerateTubeMesh(
            _bendAFilter.sharedMesh,
            _tmp,
            radius,
            tubeSides,
            transform.InverseTransformDirection(boardNormal).normalized
        );

        BuildBezierPoints(_tmp, bAcrossEnd, bVertStart, t0: acrossDir, t3: -up, radius: r, samples: bendSamples);
        ConvertPointsToLocal(_tmp, transform);
        GenerateTubeMesh(
            _bendBFilter.sharedMesh,
            _tmp,
            radius,
            tubeSides,
            transform.InverseTransformDirection(boardNormal).normalized
        );
    }

    void ScalePin(Transform pin, float diameter, float length)
    {
        if (!pin) return;

        // Only scale the pin mesh. Do NOT move it.
        // This assumes the pin has already been positioned correctly in the prefab.
        Vector3 s = pin.localScale;
        s.x = diameter;
        s.y = length * 0.5f;
        s.z = diameter;
        pin.localScale = s;
    }

    void SetBendActive(bool on)
    {
        if (_bendAFilter) _bendAFilter.gameObject.SetActive(on);
        if (_bendBFilter) _bendBFilter.gameObject.SetActive(on);
    }

    void EnsureParts()
    {
        if (_partsRoot == null)
        {
            var existing = transform.Find("CableParts");
            if (existing) _partsRoot = existing;
            else
            {
                var go = new GameObject("CableParts");
                _partsRoot = go.transform;
                _partsRoot.SetParent(transform, false);
            }
        }

        if (_straightUpA == null) _straightUpA = EnsureCylinder("StraightUpA");
        if (_straightAcross == null) _straightAcross = EnsureCylinder("StraightAcross");
        if (_straightDownB == null) _straightDownB = EnsureCylinder("StraightDownB");

        if (_bendAFilter == null) EnsureBendObject("BendA", out _bendAFilter, out _bendARenderer);
        if (_bendBFilter == null) EnsureBendObject("BendB", out _bendBFilter, out _bendBRenderer);

        if (_bendARenderer && wireMaterial) _bendARenderer.sharedMaterial = wireMaterial;
        if (_bendBRenderer && wireMaterial) _bendBRenderer.sharedMaterial = wireMaterial;
    }

    Transform EnsureCylinder(string name)
    {
        var t = _partsRoot.Find(name);
        GameObject go;

        if (!t)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_partsRoot, false);

            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);

            t = go.transform;
        }
        else
        {
            go = t.gameObject;
        }

        var rend = go.GetComponent<Renderer>();
        if (rend && wireMaterial) rend.sharedMaterial = wireMaterial;

        return t;
    }

    void EnsureBendObject(string name, out MeshFilter mf, out MeshRenderer mr)
    {
        var t = _partsRoot.Find(name);
        GameObject go;

        if (!t)
        {
            go = new GameObject(name);
            go.transform.SetParent(_partsRoot, false);
        }
        else
        {
            go = t.gameObject;
        }

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        mf = go.GetComponent<MeshFilter>();
        if (!mf) mf = go.AddComponent<MeshFilter>();

        mr = go.GetComponent<MeshRenderer>();
        if (!mr) mr = go.AddComponent<MeshRenderer>();

        if (mf.sharedMesh == null)
        {
            var mesh = new Mesh();
            mesh.name = $"{name}_Mesh";
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }
    }

    static void SetCylinderBetween(Transform cyl, Vector3 p0, Vector3 p1, float diameter)
    {
        Vector3 d = p1 - p0;
        float len = d.magnitude;

        if (len < 1e-5f)
        {
            cyl.gameObject.SetActive(false);
            return;
        }

        cyl.gameObject.SetActive(true);

        Vector3 mid = (p0 + p1) * 0.5f;
        Vector3 dir = d / len;

        cyl.position = mid;
        cyl.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        cyl.localScale = new Vector3(diameter, len * 0.5f, diameter);
    }

    void BuildBezierPoints(List<Vector3> pts, Vector3 p0, Vector3 p3, Vector3 t0, Vector3 t3, float radius, int samples)
    {
        pts.Clear();

        t0 = t0.normalized;
        t3 = t3.normalized;

        float h = radius * Mathf.Max(0f, kappa);
        Vector3 p1 = p0 + t0 * h;
        Vector3 p2 = p3 - t3 * h;

        int n = Mathf.Max(4, samples);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            pts.Add(Cubic(p0, p1, p2, p3, t));
        }
    }

    static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return (u * u * u) * p0
             + (3f * u * u * t) * p1
             + (3f * u * t * t) * p2
             + (t * t * t) * p3;
    }

    static void ConvertPointsToLocal(List<Vector3> points, Transform root)
    {
        for (int i = 0; i < points.Count; i++)
            points[i] = root.InverseTransformPoint(points[i]);
    }

    static void GenerateTubeMesh(Mesh mesh, List<Vector3> centerline, float radius, int sides, Vector3 upGuide)
    {
        int n = centerline.Count;
        if (n < 2)
        {
            mesh.Clear();
            return;
        }

        sides = Mathf.Max(3, sides);
        upGuide = (upGuide.sqrMagnitude < 1e-6f) ? Vector3.up : upGuide.normalized;

        int vertCount = n * sides;
        int triCount = (n - 1) * sides * 2;

        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var tris = new int[triCount * 3];

        for (int i = 0; i < n; i++)
        {
            Vector3 p = centerline[i];

            Vector3 tangent;
            if (i == 0) tangent = (centerline[i + 1] - centerline[i]).normalized;
            else if (i == n - 1) tangent = (centerline[i] - centerline[i - 1]).normalized;
            else tangent = (centerline[i + 1] - centerline[i - 1]).normalized;

            Vector3 normal = Vector3.Cross(upGuide, tangent);
            if (normal.sqrMagnitude < 1e-6f)
            {
                normal = Vector3.Cross(Vector3.right, tangent);
                if (normal.sqrMagnitude < 1e-6f)
                    normal = Vector3.Cross(Vector3.forward, tangent);
            }
            normal.Normalize();

            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            for (int s = 0; s < sides; s++)
            {
                float a = (s / (float)sides) * Mathf.PI * 2f;
                Vector3 offset = (Mathf.Cos(a) * normal + Mathf.Sin(a) * binormal) * radius;

                int idx = i * sides + s;
                verts[idx] = p + offset;
                norms[idx] = offset.normalized;
                uvs[idx] = new Vector2(s / (float)sides, i / (float)(n - 1));
            }
        }

        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int ring0 = i * sides;
            int ring1 = (i + 1) * sides;

            for (int s = 0; s < sides; s++)
            {
                int s0 = s;
                int s1 = (s + 1) % sides;

                int a = ring0 + s0;
                int b = ring1 + s0;
                int c = ring1 + s1;
                int d = ring0 + s1;

                tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                tris[ti++] = a; tris[ti++] = d; tris[ti++] = c;
            }
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }
}