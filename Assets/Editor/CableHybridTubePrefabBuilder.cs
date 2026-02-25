#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CableHybridTubePrefabBuilder
{
    [MenuItem("Tools/EiT VR/Create Cable Prefab (Hybrid Tube Bends)")]
    public static void CreateCablePrefab()
    {
        // ---- Dimensions (meters) ----
        float defaultEndDistance = 0.06f; // 6 cm between ends (starting pose)

        float pinDiameter = 0.0010f; // 1.0 mm
        float pinLength   = 0.0060f; // 6.0 mm (down into socket)

        float wireDiameter = 0.0020f; // 2.0 mm insulation diameter

        // ---- Create root ----
        var root = new GameObject("Cable_HybridTube");

        // ---- Endpoints ----
        var endA = new GameObject("EndA");
        endA.transform.SetParent(root.transform, false);

        var endB = new GameObject("EndB");
        endB.transform.SetParent(root.transform, false);
        endB.transform.localPosition = new Vector3(defaultEndDistance, 0f, 0f);

        // ---- Materials (URP Lit if available, else Standard) ----
        EnsureFolder("Assets", "Materials");

        var wireMat = CreateOrLoadLitMaterial(
            "Assets/Materials/Mat_Wire_Red.mat",
            new Color(0.85f, 0.05f, 0.05f),
            metallic: 0f,
            smoothness: 0.30f
        );

        var metalMat = CreateOrLoadLitMaterial(
            "Assets/Materials/Mat_Metal_Silver.mat",
            new Color(0.75f, 0.75f, 0.75f),
            metallic: 1f,
            smoothness: 0.80f
        );

        // ---- Pins (silver cylinders) ----
        CreatePin("PinA", endA.transform, metalMat, pinDiameter, pinLength);
        CreatePin("PinB", endB.transform, metalMat, pinDiameter, pinLength);

        // ---- Add the runtime visual/geometry script ----
        // IMPORTANT: this requires you to have CableRightAngleHybridTube.cs in Assets/Scripts/Cables/
        var cable = root.AddComponent<CableRightAngleHybridTube>();
        cable.endA = endA.transform;
        cable.endB = endB.transform;
        cable.boardNormal = Vector3.up;

        cable.wireMaterial = wireMat;
        cable.wireDiameter = wireDiameter;

        // Nice defaults
        cable.liftHeight = 0.01f;
        cable.bendRadius = 0.004f;
        cable.bendSamples = 24;
        cable.tubeSides = 16;

        // ---- Save prefab ----
        EnsureFolder("Assets", "Prefabs");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefabs/Cable_HybridTube.prefab");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created prefab: {path}");
    }

    // ---------------- Helpers ----------------

    static void CreatePin(string name, Transform parent, Material mat, float diameter, float length)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Unity cylinder axis is Y; height is 2 units => scale.y = length/2; scale.x/z = diameter
        go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
        go.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);

        var r = go.GetComponent<Renderer>();
        if (r && mat) r.sharedMaterial = mat;
    }

    static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }

    static Material CreateOrLoadLitMaterial(string assetPath, Color baseColor, float metallic, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        // URP Lit: _BaseColor, Standard: _Color
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

        // URP Lit: _Smoothness, Standard: _Glossiness
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif