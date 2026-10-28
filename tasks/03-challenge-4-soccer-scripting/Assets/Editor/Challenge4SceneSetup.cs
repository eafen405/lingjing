using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor side helpers for the Challenge 4 scene.
///
/// <para><c>Apply</c> performs the one scene change the turbo boost needs: the
/// smoke effect of the <c>Smoke_Particle</c> prefab has to live under the
/// "Focal Point" (that is where the tutorial puts it) and the player controller
/// has to know about it.</para>
///
/// <para><c>Dump</c> prints the collider bounds of every scene object, which is
/// the quickest way to see where the arena actually is.</para>
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod Challenge4SceneSetup.Apply -quit
/// </summary>
public static class Challenge4SceneSetup
{
    private const string ScenePath = "Assets/Challenge 4/Challenge 4.unity";

    /// <summary>Prints the collider bounds of every object in the challenge scene.</summary>
    public static void Dump()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var report = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var colliders = transform.GetComponents<Collider>();
                string bounds = colliders.Length == 0 ? "-" : string.Join(" | ", System.Array.ConvertAll(colliders, c => c.bounds.ToString()));
                report.AppendLine(string.Format(
                    "{0,-20} active={1,-5} pos={2,-42} scale={3,-28} bounds={4}",
                    transform.name,
                    transform.gameObject.activeInHierarchy,
                    transform.position.ToString("F2"),
                    transform.lossyScale.ToString("F2"),
                    bounds));
            }
        }

        Debug.Log("CHALLENGE4-DUMP-BEGIN\n" + report + "CHALLENGE4-DUMP-END");
    }

    /// <summary>
    /// Puts the smoke effect under the focal point and hands it to the player
    /// controller, then saves the scene.
    /// </summary>
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var focalPoint = GameObject.Find("Focal Point");
        var smoke = GameObject.Find("Smoke_Particle");
        var player = GameObject.Find("Player");

        if (focalPoint == null || smoke == null || player == null)
        {
            Debug.LogError("CHALLENGE4-SETUP: the scene is missing Focal Point, Smoke_Particle or Player");
            EditorApplication.Exit(1);
            return;
        }

        var controller = player.GetComponent<PlayerControllerX>();
        if (controller == null)
        {
            Debug.LogError("CHALLENGE4-SETUP: the player has no PlayerControllerX");
            EditorApplication.Exit(1);
            return;
        }

        var spawner = GameObject.Find("Spawn Manager");
        if (spawner == null)
        {
            Debug.LogError("CHALLENGE4-SETUP: the scene has no Spawn Manager");
            EditorApplication.Exit(1);
            return;
        }

        // The tutorial hangs the particle effect under the focal point.
        smoke.transform.SetParent(focalPoint.transform, false);
        smoke.transform.localPosition = Vector3.zero;

        controller.dashParticleEffect = smoke;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format(
            "CHALLENGE4-SETUP: smoke parent={0} localPosition={1} dashParticleEffect={2}",
            smoke.transform.parent != null ? smoke.transform.parent.name : "<none>",
            smoke.transform.localPosition,
            controller.dashParticleEffect != null ? controller.dashParticleEffect.name : "<none>"));
    }
}
