using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reports the real size of a prefab's renderers and colliders so colliders can
/// be sized to the model instead of guessed.
///
/// The model bounds are reported in the prefab root's LOCAL space, because a
/// BoxCollider on the root is expressed in that same space. The world space
/// numbers alone are misleading here: the dog prefab root is rotated -90 degrees
/// on Y, so its local X axis points along world -X.
///
/// Run head-less, passing the prefab path after "--":
///   Unity.exe -batchmode -quit -nographics -projectPath &lt;project&gt; \
///             -executeMethod MeasureTools.Measure -logFile &lt;log&gt; -- \
///             "Assets/Challenge 2/Prefabs/Dog.prefab"
///
/// Lives under Assets/Editor so it never reaches a player build.
/// </summary>
public static class MeasureTools
{
    public static void Measure()
    {
        string path = ReadPathArgument();
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("MEASURE no prefab path given");
            EditorApplication.Exit(1);
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.Log($"MEASURE prefab not found at {path}");
            EditorApplication.Exit(1);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Keep the prefab's own rotation and scale, only move it to the origin.
        instance.transform.position = Vector3.zero;
        Transform root = instance.transform;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        bool first = true;
        Bounds bounds = default;
        foreach (var renderer in renderers)
        {
            if (first)
            {
                bounds = renderer.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Debug.Log($"MEASURE {path} renderers={renderers.Length} worldBoundsSize={bounds.size} worldBoundsCenter={bounds.center}");
        Debug.Log($"MEASURE {path} rootScale={root.lossyScale} rootEuler={root.localEulerAngles}");

        // Convert the world space bounds into the root's local space. The corners
        // are transformed one by one because the root is rotated, so a plain
        // centre/extents conversion would give a wrong local box.
        Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var renderer in renderers)
        {
            Bounds r = renderer.bounds;
            Vector3 c = r.center;
            Vector3 e = r.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = root.InverseTransformPoint(c + Vector3.Scale(e, new Vector3(xi, yi, zi)));
                        localMin = Vector3.Min(localMin, corner);
                        localMax = Vector3.Max(localMax, corner);
                    }
                }
            }
        }
        Vector3 localSize = localMax - localMin;
        Vector3 localCenter = (localMin + localMax) * 0.5f;
        Debug.Log($"MEASURE {path} LOCAL model size={localSize} center={localCenter}");

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            string detail;
            if (collider is BoxCollider box)
            {
                detail = $"box LOCAL size={box.size} center={box.center} worldSize={Vector3.Scale(box.size, root.lossyScale)}";
            }
            else if (collider is SphereCollider sphere)
            {
                detail = $"sphere r={sphere.radius} worldR={sphere.radius * root.lossyScale.x}";
            }
            else
            {
                detail = collider.GetType().Name;
            }
            Debug.Log($"MEASURE {path} collider on '{collider.name}' trigger={collider.isTrigger} {detail}");
        }

        if (renderers.Length > 0)
        {
            Debug.Log($"MEASURE {path} SUGGESTED BoxCollider size={localSize} center={localCenter}");
        }

        UnityEngine.Object.DestroyImmediate(instance);
        EditorApplication.Exit(0);
    }

    private static string ReadPathArgument()
    {
        var args = Environment.GetCommandLineArgs();
        int separator = Array.IndexOf(args, "--");
        return separator >= 0 && separator + 1 < args.Length ? args[separator + 1] : null;
    }
}
