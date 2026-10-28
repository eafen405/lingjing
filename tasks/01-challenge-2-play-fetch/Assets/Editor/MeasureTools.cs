using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reports the real world-space size of a prefab's renderers and colliders so
/// colliders can be sized to the model instead of guessed.
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
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

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

        Debug.Log($"MEASURE {path} renderers={renderers.Length} boundsSize={bounds.size} boundsCenter={bounds.center}");
        Debug.Log($"MEASURE {path} rootScale={instance.transform.lossyScale}");

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            string detail;
            if (collider is BoxCollider box)
            {
                detail = $"box size={box.size} center={box.center} worldSize={Vector3.Scale(box.size, instance.transform.lossyScale)}";
            }
            else if (collider is SphereCollider sphere)
            {
                detail = $"sphere r={sphere.radius} worldR={sphere.radius * instance.transform.lossyScale.x}";
            }
            else
            {
                detail = collider.GetType().Name;
            }
            Debug.Log($"MEASURE {path} collider on '{collider.name}' trigger={collider.isTrigger} {detail}");
        }

        Object.DestroyImmediate(instance);
        EditorApplication.Exit(0);
    }

    private static string ReadPathArgument()
    {
        var args = Environment.GetCommandLineArgs();
        int separator = Array.IndexOf(args, "--");
        return separator >= 0 && separator + 1 < args.Length ? args[separator + 1] : null;
    }
}
