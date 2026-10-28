using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play mode probe for Challenge 2 - Play Fetch.
///
/// Opens the challenge scene, enters play mode and asserts the observable
/// behaviour the assignment asks for:
///   - the player sends out a dog and the dog runs towards -X
///   - balls appear at the top of the screen, of three different types, at a
///     random interval between 3 and 5 seconds
///   - a ball dies when the dog touches it, and only then
///   - a ball that reaches the ground is cleaned up and reports Game Over
///   - the dog is cleaned up when it leaves the screen
///
/// The destruction paths are tested deterministically instead of by waiting for
/// a lucky interception: the probe puts a ball exactly onto the dog, puts a
/// second ball just outside the hitbox and drops a third one under the floor.
///
/// The probe drives the game through <c>PlayerControllerX.SpawnDog()</c> rather
/// than a synthetic key press, because legacy <c>Input</c> cannot be faked in a
/// head-less batch mode session. The space bar binding itself is covered by
/// the static wiring check in tools/check-wiring.ps1 and by the build smoke test.
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod Challenge2Probe.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;report path&gt;"
///
/// Note: do NOT pass -quit. The editor has to stay alive for the play mode
/// callback to run; the probe exits the editor itself once the report is out.
/// </summary>
[InitializeOnLoad]
public static class Challenge2Probe
{
    private const string SceneKey = "Challenge2Probe.Scene";
    internal const string ReportKey = "Challenge2Probe.Report";
    internal const string ColliderKey = "Challenge2Probe.Collider";
    private const string StartKey = "Challenge2Probe.Start";
    private const double WatchdogSeconds = 240d;

    static Challenge2Probe()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Watchdog;
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        int separator = Array.IndexOf(args, "--");
        string scene = separator >= 0 && separator + 1 < args.Length
            ? args[separator + 1]
            : "Assets/Challenge 2/Challenge 2.unity";
        string report = separator >= 0 && separator + 2 < args.Length
            ? args[separator + 2]
            : "challenge2-probe.txt";

        SessionState.SetString(SceneKey, scene);
        SessionState.SetString(ReportKey, report);
        SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);

        EditorSceneManager.OpenScene(scene);
        SessionState.SetString(ColliderKey, CheckDogCollider());
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Compares the dog collider with the dog model. This has to happen in edit
    /// mode: in play mode the dog is animated, so the live renderer bounds no
    /// longer describe the model the collider was measured from.
    ///
    /// The starter file ships a collider that is about 13 times too long and 8
    /// times too high, which is what made the balls disappear when they merely
    /// came near the dog.
    /// </summary>
    private static string CheckDogCollider()
    {
        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        if (controller == null || controller.dogPrefab == null)
        {
            return "FAIL no dog prefab assigned to the player";
        }

        string prefabPath = AssetDatabase.GetAssetPath(controller.dogPrefab);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return "FAIL the dog prefab could not be loaded";
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        Transform root = instance.transform;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var renderer in renderers)
        {
            Bounds r = renderer.bounds;
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
        Vector3 modelSize = localMax - localMin;

        BoxCollider collider = instance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return "FAIL the dog prefab has no BoxCollider";
        }

        Vector3 colliderSize = collider.size;
        bool ok = true;
        for (int i = 0; i < 3; i++)
        {
            if (modelSize[i] <= 0f)
            {
                continue;
            }
            if (Mathf.Abs(colliderSize[i] - modelSize[i]) > modelSize[i] * 0.15f)
            {
                ok = false;
            }
        }

        UnityEngine.Object.DestroyImmediate(instance);

        string detail = "collider " + colliderSize.ToString("F3", CultureInfo.InvariantCulture) +
                        " vs model " + modelSize.ToString("F3", CultureInfo.InvariantCulture);
        return (ok ? "PASS" : "FAIL") + " " + detail;
    }

    /// <summary>
    /// The editor is started without -quit, so it needs a way out when the play
    /// mode session never reaches the report stage.
    /// </summary>
    private static void Watchdog()
    {
        float start = SessionState.GetFloat(StartKey, 0f);
        if (start <= 0f)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup - start > WatchdogSeconds)
        {
            Debug.Log("PROBE-REPORT-BEGIN\nWATCHDOG TIMEOUT - the probe never finished\nPROBE-REPORT-END");
            EditorApplication.Exit(2);
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        var host = new GameObject("Challenge2ProbeAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Challenge2ProbeAgent>();
    }
}

/// <summary>Runs the scenario, collects results and writes the report.</summary>
public class Challenge2ProbeAgent : MonoBehaviour
{
    private const float LeftLimit = -25f;
    private const float SpawnPosY = 30f;
    private const float MinInterval = 3f;
    private const float MaxInterval = 5f;
    private const float RunSeconds = 30f;

    private readonly List<string> _results = new List<string>();
    private readonly List<float> _ballSpawnTimes = new List<float>();
    private readonly HashSet<string> _ballTypesSeen = new HashSet<string>();
    private readonly HashSet<int> _testBalls = new HashSet<int>();
    private readonly HashSet<int> _seenBallIds = new HashSet<int>();
    private bool _ballFellFromTop;

    private float _startTime;
    private float _dogSpawnTime = -1f;
    private float _dogLastX;
    private float _dogFirstY;
    private bool _dogSpawned;
    private bool _dogMovedLeft;
    private bool _dogDestroyed;
    private bool _gameOverLogged;
    private int _gameOverCount;
    private bool _cooldownHolds;
    private bool _cooldownTested;
    private bool _wiringChecked;
    private bool _finished;
    private int _dogsSeen;

    // deterministic destruction tests
    private bool _contactTestStarted;
    private bool _contactTestFinished;
    private bool _ballTouchedDog;
    private int _contactBallId = -1;

    private bool _nearTestStarted;
    private bool _nearTestFinished;
    private bool _ballSurvivedNearby = true;
    private int _nearBallId = -1;
    private float _nearTestStart;
    private float _nearBallLastY = float.NaN;

    private bool _groundTestStarted;
    private bool _groundTestFinished;
    private bool _ballRemovedAtBottom;
    private int _groundBallId = -1;
    private int _gameOverAtGroundStart;

    private void Start()
    {
        _startTime = Time.time;
        Application.logMessageReceived += OnLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            _results.Add("FAIL no-errors: logged " + type + ": " + condition);
        }
        else if (condition.Contains("Game Over"))
        {
            _gameOverLogged = true;
            _gameOverCount++;
        }
    }

    private void Update()
    {
        if (_finished)
        {
            return;
        }

        float elapsed = Time.time - _startTime;

        CheckStaticWiring();
        TrackDogs();
        TrackNaturalBalls();

        if (elapsed > 1.0f)
        {
            EnsureDog();
            TestSpawnCooldown();
            RunContactTest();
            RunNearbyTest();
            RunGroundTest();
        }

        if (elapsed > RunSeconds)
        {
            Finish();
        }
    }

    /// <summary>Scene wiring the challenge expects: dog on the player, balls in the spawner.</summary>
    private void CheckStaticWiring()
    {
        if (_wiringChecked)
        {
            return;
        }

        _wiringChecked = true;

        var player = GameObject.Find("Player");
        if (player == null)
        {
            _results.Add("FAIL wiring: no object named Player in the scene");
            return;
        }

        var controller = player.GetComponent<PlayerControllerX>();
        var spawner = GameObject.Find("Spawn Manager");
        if (controller == null || spawner == null)
        {
            _results.Add("FAIL wiring: Player/PlayerControllerX or Spawn Manager missing");
            return;
        }

        bool dogAssigned = controller.dogPrefab != null && controller.dogPrefab.CompareTag("Dog");
        _results.Add((dogAssigned ? "PASS" : "FAIL") + " wiring-dog-prefab: the player spawns the dog prefab");

        var manager = spawner.GetComponent<SpawnManagerX>();
        bool threeBalls = manager.ballPrefabs != null && manager.ballPrefabs.Length == 3;
        var distinct = new HashSet<GameObject>();
        bool noDog = true;
        if (threeBalls)
        {
            foreach (var prefab in manager.ballPrefabs)
            {
                if (prefab == null) { threeBalls = false; continue; }
                distinct.Add(prefab);
                if (prefab.CompareTag("Dog")) { noDog = false; }
            }
        }
        _results.Add((threeBalls ? "PASS" : "FAIL") + " wiring-ball-array: the spawn manager holds three balls");
        _results.Add(((noDog && distinct.Count == 3) ? "PASS" : "FAIL") + " wiring-ball-variety: three distinct ball prefabs, no dog among them");
    }

    // ---------------------------------------------------------------- dogs ---

    private void EnsureDog()
    {
        if (CountDogs() > 0)
        {
            return;
        }

        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        if (controller == null)
        {
            return;
        }

        controller.SpawnDog();
    }

    private static int CountDogs()
    {
        int count = 0;
        foreach (var dog in GameObject.FindGameObjectsWithTag("Dog"))
        {
            if (dog.GetComponent<MoveForwardX>() != null)
            {
                count++;
            }
        }
        return count;
    }

    private void TrackDogs()
    {
        foreach (var dog in GameObject.FindGameObjectsWithTag("Dog"))
        {
            if (dog.GetComponent<MoveForwardX>() == null)
            {
                continue;
            }

            if (!_dogSpawned)
            {
                _dogSpawned = true;
                _dogSpawnTime = Time.time;
                _dogLastX = dog.transform.position.x;
                _dogFirstY = dog.transform.position.y;
            }

            _dogsSeen++;
            float x = dog.transform.position.x;
            float y = dog.transform.position.y;

            if (x < _dogLastX - 0.01f)
            {
                _dogMovedLeft = true;
            }
            if (Mathf.Abs(y - _dogFirstY) > 0.5f)
            {
                _results.Add("FAIL dog-stays-on-ground: y moved to " + y.ToString("F2", CultureInfo.InvariantCulture));
            }
            _dogLastX = x;
        }

        if (_dogSpawned && _dogsSeen > 0 && CountDogs() == 0)
        {
            _dogDestroyed = true;
            _dogSpawned = false;
        }
    }

    /// <summary>
    /// A second spawn inside the cooldown window has to be rejected, otherwise
    /// the player could spam the spacebar and fill the screen with dogs.
    /// </summary>
    private void TestSpawnCooldown()
    {
        if (_cooldownTested || !_dogSpawned)
        {
            return;
        }

        if (Time.time - _dogSpawnTime > 0.3f)
        {
            return;
        }

        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        if (controller == null)
        {
            return;
        }

        int duringCooldown = CountDogs();
        controller.SpawnDog();
        _cooldownHolds = CountDogs() == duringCooldown;
        _cooldownTested = true;
    }

    // --------------------------------------------------------------- balls ---

    private static List<Rigidbody> FindSceneBalls()
    {
        var balls = new List<Rigidbody>();
        Scene scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.name.StartsWith("Ball"))
                {
                    balls.Add(body);
                }
            }
        }
        return balls;
    }

    private GameObject GetBallPrefab()
    {
        var spawner = GameObject.Find("Spawn Manager");
        if (spawner == null)
        {
            return null;
        }

        var manager = spawner.GetComponent<SpawnManagerX>();
        if (manager == null || manager.ballPrefabs == null || manager.ballPrefabs.Length == 0)
        {
            return null;
        }

        return manager.ballPrefabs[0];
    }

    private static float FindBallY(int instanceId)
    {
        if (instanceId < 0)
        {
            return float.NaN;
        }
        foreach (var ball in FindSceneBalls())
        {
            if (ball.gameObject.GetInstanceID() == instanceId)
            {
                return ball.transform.position.y;
            }
        }
        return float.NaN;
    }

    private static bool Exists(int instanceId)
    {
        if (instanceId < 0)
        {
            return false;
        }
        foreach (var ball in FindSceneBalls())
        {
            if (ball.gameObject.GetInstanceID() == instanceId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Watches the balls the spawn manager produces on its own.</summary>
    private void TrackNaturalBalls()
    {
        foreach (var ball in FindSceneBalls())
        {
            // The GameObject id, not the Rigidbody's: the probe identifies the
            // balls it spawned itself by the GameObject id.
            int id = ball.gameObject.GetInstanceID();
            if (_testBalls.Contains(id) || !_seenBallIds.Add(id))
            {
                continue;
            }

            _ballSpawnTimes.Add(Time.time - _startTime);
            _ballTypesSeen.Add(ball.name);
            if (ball.transform.position.y > SpawnPosY - 2f)
            {
                _ballFellFromTop = true;
            }
        }
    }

    /// <summary>
    /// Drops a ball exactly onto the dog. It has to be destroyed by the
    /// collision and must not report Game Over, because it never reached the
    /// ground.
    /// </summary>
    private void RunContactTest()
    {
        if (_contactTestFinished)
        {
            return;
        }

        if (!_contactTestStarted)
        {
            if (CountDogs() == 0)
            {
                return;
            }

            var prefab = GetBallPrefab();
            if (prefab == null)
            {
                return;
            }

            var dog = GameObject.FindGameObjectWithTag("Dog");
            Vector3 position = dog != null ? dog.transform.position : Vector3.zero;
            var ball = Instantiate(prefab, position, prefab.transform.rotation);
            _contactBallId = ball.GetInstanceID();
            _testBalls.Add(_contactBallId);
            _contactTestStarted = true;
            return;
        }

        if (Exists(_contactBallId))
        {
            return;
        }

        _ballTouchedDog = true;
        _contactTestFinished = true;
    }

    /// <summary>
    /// Puts a ball just outside the dog's hitbox. It has to survive, otherwise
    /// the collider is so big that the balls vanish when they merely come near
    /// the dog.
    /// </summary>
    private void RunNearbyTest()
    {
        if (_nearTestFinished)
        {
            return;
        }

        if (!_nearTestStarted)
        {
            if (CountDogs() == 0)
            {
                return;
            }

            var prefab = GetBallPrefab();
            if (prefab == null)
            {
                return;
            }

            var dog = GameObject.FindGameObjectWithTag("Dog");
            if (dog == null)
            {
                return;
            }

            // The dog runs towards -X, so a ball to its right is never run into.
            // It is dropped from a height so that it cannot reach the bottom of
            // the play area before the observation window is over.
            // Ten units to the right is outside the repaired hitbox (three units
            // half width) but well inside the broken one (thirty nine units).
            Vector3 position = dog.transform.position + new Vector3(10f, 4f, 0f);
            var ball = Instantiate(prefab, position, prefab.transform.rotation);
            // Hold the ball in the air: only a trigger with the dog may destroy
            // it, not the bottom of the play area. The body stays dynamic so
            // that trigger events are still generated.
            var body = ball.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
            }
            _nearBallId = ball.GetInstanceID();
            _testBalls.Add(_nearBallId);
            _nearTestStart = Time.time;
            _nearTestStarted = true;
            return;
        }

        if (Time.time - _nearTestStart < 0.8f)
        {
            _nearBallLastY = FindBallY(_nearBallId);
            return;
        }

        _ballSurvivedNearby = Exists(_nearBallId);
        _nearTestFinished = true;
    }

    /// <summary>
    /// Drops a ball under the floor. It has to be destroyed and it has to
    /// report Game Over.
    /// </summary>
    private void RunGroundTest()
    {
        if (_groundTestFinished)
        {
            return;
        }

        if (!_groundTestStarted)
        {
            var prefab = GetBallPrefab();
            if (prefab == null)
            {
                return;
            }

            var player = GameObject.Find("Player");
            Vector3 position = new Vector3(player != null ? player.transform.position.x : 0f, -8f, 0f);
            var ball = Instantiate(prefab, position, prefab.transform.rotation);
            _groundBallId = ball.GetInstanceID();
            _testBalls.Add(_groundBallId);
            _groundTestStarted = true;
            return;
        }

        if (Exists(_groundBallId))
        {
            return;
        }

        _ballRemovedAtBottom = true;
        _groundTestFinished = true;
    }

    private void Finish()
    {
        _finished = true;

        bool intervalsOk = true;
        var intervals = new List<float>();
        for (int i = 1; i < _ballSpawnTimes.Count; i++)
        {
            float delta = _ballSpawnTimes[i] - _ballSpawnTimes[i - 1];
            intervals.Add(delta);
            if (delta < MinInterval - 0.4f || delta > MaxInterval + 0.4f)
            {
                intervalsOk = false;
            }
        }

        _results.Add((_dogSpawned || _dogsSeen > 0 ? "PASS" : "FAIL") + " space-spawns-dog: the player can send out a dog");
        _results.Add((_cooldownHolds ? "PASS" : "FAIL") + " spawn-cooldown: a second spawn inside the cooldown is rejected");
        _results.Add((_dogMovedLeft ? "PASS" : "FAIL") + " dog-runs-left: the dog travels towards -X");
        _results.Add((_dogDestroyed ? "PASS" : "FAIL") + " dog-destroyed-off-screen: the dog is cleaned up at x < " + LeftLimit);
        _results.Add((_ballFellFromTop ? "PASS" : "FAIL") + " ball-spawns-at-top: balls appear near y = " + SpawnPosY);
        _results.Add((_ballTypesSeen.Count >= 2 ? "PASS" : "FAIL") + " ball-type-random: saw " + _ballTypesSeen.Count + " different ball types");
        _results.Add((intervalsOk ? "PASS" : "FAIL") + " spawn-interval-random: intervals " + string.Join(", ", intervals.ConvertAll(v => v.ToString("F2", CultureInfo.InvariantCulture))));
        _results.Add((_ballTouchedDog ? "PASS" : "FAIL") + " ball-destroyed-on-contact: a ball dies when the dog touches it");
        _results.Add((_ballSurvivedNearby ? "PASS" : "FAIL") + " ball-survives-nearby: a ball that only comes near the dog survives (last y " +
                     _nearBallLastY.ToString("F2", CultureInfo.InvariantCulture) + ", game over logs " + _gameOverCount + ")");
        _results.Add((_ballRemovedAtBottom ? "PASS" : "FAIL") + " ball-destroyed-at-bottom: a ball that reached the ground is cleaned up");
        _results.Add((_gameOverLogged ? "PASS" : "FAIL") + " game-over-message: the round end is reported");
        _results.Add(SessionState.GetString(Challenge2Probe.ColliderKey, "FAIL the dog collider was not checked"));

        int failed = 0;
        foreach (var result in _results)
        {
            if (result.StartsWith("FAIL")) failed++;
        }

        var report = new StringBuilder();
        report.AppendLine("Challenge 2 - Play Fetch probe");
        report.AppendLine(new string('-', 60));
        foreach (var result in _results)
        {
            report.AppendLine(result);
        }
        report.AppendLine(new string('-', 60));
        report.AppendLine($"checks: {_results.Count}  failed: {failed}");

        string path = SessionState.GetString(Challenge2Probe.ReportKey, "challenge2-probe.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("PROBE-REPORT-BEGIN\n" + report + "PROBE-REPORT-END");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
