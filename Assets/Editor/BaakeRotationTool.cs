using UnityEngine;
using UnityEditor;

public class BakeRotationTool : EditorWindow
{
    [MenuItem("Tools/Bake Root Rotation Into Children")]
    static void BakeRotation()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No GameObject selected!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Bake Rotation");

        // Store the world transforms of all children
        Transform[] children = selected.GetComponentsInChildren<Transform>(true);
        Vector3[] worldPositions = new Vector3[children.Length];
        Quaternion[] worldRotations = new Quaternion[children.Length];

        for (int i = 0; i < children.Length; i++)
        {
            worldPositions[i] = children[i].position;
            worldRotations[i] = children[i].rotation;
        }

        // Zero out the root rotation
        selected.transform.rotation = Quaternion.identity;

        // Restore all children to their original world transforms
        for (int i = 1; i < children.Length; i++) // skip index 0 (the root itself)
        {
            children[i].position = worldPositions[i];
            children[i].rotation = worldRotations[i];
        }

        Debug.Log($"Baked rotation of '{selected.name}'. Root rotation is now (0,0,0).");
    }
}