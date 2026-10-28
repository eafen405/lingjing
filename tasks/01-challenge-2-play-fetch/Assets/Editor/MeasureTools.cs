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
    /// <summary>
    /// The merged bounds of the renderers below <paramref name="root"/>, expressed
    /// in the root's local space.
    ///
    /// The corners of every renderer's world space bounding box are transformed
    /// one by one: a root that carries a rotation cannot be handled by taking
    /// the world AABB apart, that would give a negative size.
    /// </summary>
    public static Bounds GetLocalModelBounds(GameObject instance, Transform root)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        foreach (var renderer in renderers)
        {
            Bounds r = renderer.bounds;
            any = true;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = root.InverseTransformPoint(r.center + Vector3.Scale(r.extents, new Vector3(xi, yi, zi)));
                        localMin = Vector3.Min(localMin, corner);
                        localMax = Vector3.Max(localMax, corner);
                    }
                }
            }
        }

        if (!any)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(localMin, localMax);
        return bounds;
    }

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
        Physics.SyncTransforms();
        Transform root = instance.transform;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds model = GetLocalModelBounds(instance, root);

        Debug.Log($"MEASURE {path} renderers={renderers.Length} localModelSize={model.size} localModelCenter={model.center}");
        Debug.Log($"MEASURE {path} rootScale={root.lossyScale} rootEuler={root.localEulerAngles}");

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

        Debug.Log($"MEASURE {path} SUGGESTED BoxCollider size={model.size} center={model.center}");

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
