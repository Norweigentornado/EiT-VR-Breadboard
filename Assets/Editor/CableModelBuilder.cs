#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CableModelBuilder
{
    [MenuItem("Tools/EiT VR/Create Cable Prefab (Primitives)")]
    public static void CreateCablePrefab()
    {
        // ====== TUNABLE DIMENSIONS (meters) ======
        float plugBodyRadius = 0.0040f;   // 4mm
        float plugBodyLength = 0.0120f;   // 12mm

        float tipRadius      = 0.0012f;   // 1.2mm
        float tipLength      = 0.0100f;   // 10mm

        float wireRadius     = 0.0015f;   // 1.5mm
        float initialWireLen = 0.08f;     // 8cm

        var root = new GameObject("Cable");

        var plugA = new GameObject("PlugA");
        plugA.transform.SetParent(root.transform, false);

        var plugB = new GameObject("PlugB");
        plugB.transform.SetParent(root.transform, false);
        plugB.transform.localPosition = new Vector3(0, 0, initialWireLen);

        BuildPlug(plugA.transform, plugBodyRadius, plugBodyLength, tipRadius, tipLength);
        BuildPlug(plugB.transform, plugBodyRadius, plugBodyLength, tipRadius, tipLength);

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);

        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localPosition = new Vector3(0, 0, initialWireLen * 0.5f);
        shaft.transform.localScale = new Vector3(wireRadius * 2f, initialWireLen * 0.5f, wireRadius * 2f);

        string prefabDir = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{prefabDir}/Cable.prefab");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created cable prefab at: {path}");
    }

    private static void BuildPlug(Transform plugRoot, float bodyRadius, float bodyLen, float tipRadius, float tipLen)
    {
        var tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tip.name = "MetalTip";
        tip.transform.SetParent(plugRoot, false);

        tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tip.transform.localPosition = new Vector3(0, 0, tipLen * 0.5f);
        tip.transform.localScale = new Vector3(tipRadius * 2f, tipLen * 0.5f, tipRadius * 2f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "PlasticBody";
        body.transform.SetParent(plugRoot, false);

        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localPosition = new Vector3(0, 0, tipLen + bodyLen * 0.5f);
        body.transform.localScale = new Vector3(bodyRadius * 2f, bodyLen * 0.5f, bodyRadius * 2f);

        Object.DestroyImmediate(tip.GetComponent<Collider>());
        Object.DestroyImmediate(body.GetComponent<Collider>());
    }
}
#endif
