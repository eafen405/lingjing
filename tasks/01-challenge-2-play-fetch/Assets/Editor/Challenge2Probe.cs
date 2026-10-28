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
/// Opens the challenge scene, enters play mode, drives the game and asserts the
/// observable behaviour the assignment asks for: the player sends out a dog, the
/// dog runs towards -X, balls fall from the sky, a ball dies only when the dog
/// actually touches it, a ball that reaches the ground ends the round with a
/// "Game Over" message, and both the dog and the balls are cleaned up when they
/// leave the screen.
///
/// The probe drives the game through <c>PlayerControllerX.SpawnDog()</c> rather
/// than a synthetic key press, because legacy <c>Input</c> cannot be faked in a
/// head-less batch mode session. The space bar binding itself is covered by the
/// static wiring check in tools/check-wiring.ps1 and by the build smoke test.
///
/// To make the catch happen reliably the probe times the dog: a dog only lives
/// about 1.3 seconds and the ball needs about 2.4 seconds to reach the ground,
/// so the dog has to be sent out at the right moment. The probe releases a dog
/// when the ball is about to reach the dog's height at the same time the dog
/// would arrive at the ball's x position.
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
        EditorApplication.EnterPlaymode();
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
    private const float CatchHeight = 1.5f;

    private readonly List<string> _results = new List<string>();
    private readonly Dictionary<int, BallState> _balls = new Dictionary<int, BallState>();
    private readonly List<float> _ballSpawnTimes = new List<float>();
    private readonly HashSet<string> _ballTypesSeen = new HashSet<string>();
    private readonly HashSet<int> _aimedBalls = new HashSet<int>();

    private float _startTime;
    private float _dogSpawnTime = -1f;
    private float _dogFirstX;
    private float _dogLastX;
    private float _dogFirstY;
    private float _dogSpeed = 30f;
    private bool _dogSpawned;
    private bool _dogMovedLeft;
    private bool _dogDestroyed;
    private bool _gameOverLogged;
    private bool _ballFellFromTop;
    private bool _spaceSpawnsDog;
    private bool _cooldownHolds;
    private bool _cooldownTested;
    private bool _wiringChecked;
    private bool _ballCaughtByDog;
    private bool _finished;
    private int _dogsSeen;
    private int _gameOverCount;
    private int _ballsReachingGround;
    private string _colliderDetail = "no dog with a BoxCollider was found";
    private string _groundDetail = "";

    private sealed class BallState
    {
        public float LastY;
        public bool TouchedDog;
    }

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
        TrackBalls();

        if (elapsed > 0.5f)
        {
            SendDogToInterceptBall();
            TestSpawnCooldown();
        }

        if (elapsed > 30f)
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

    /// <summary>
    /// Releases a dog so that it reaches the falling ball at the moment the ball
    /// is at the dog's height. A dog crosses the play area in about 1.3 seconds
    /// while a ball needs about 2.4 seconds to reach the ground, so the timing
    /// is what decides whether the catch happens.
    /// </summary>
    private void SendDogToInterceptBall()
    {
        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        if (controller == null || controller.dogPrefab == null)
        {
            return;
        }

        GameObject target = null;
        float bestHeight = float.MinValue;
        foreach (var ball in FindSceneBalls())
        {
            int id = ball.GetInstanceID();
            if (_aimedBalls.Contains(id))
            {
                continue;
            }

            float height = ball.transform.position.y;
            if (height > bestHeight)
            {
                bestHeight = height;
                target = ball.gameObject;
            }
        }

        if (target == null)
        {
            return;
        }

        var body = target.GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        float fallSpeed = Mathf.Max(1f, -body.velocity.y);
        float timeUntilCatchHeight = (target.transform.position.y - CatchHeight) / fallSpeed;
        float timeUntilDogArrives = (player.transform.position.x - target.transform.position.x) / _dogSpeed;

        if (timeUntilCatchHeight > timeUntilDogArrives)
        {
            return;
        }

        _aimedBalls.Add(target.GetInstanceID());

        int before = CountDogs();
        controller.SpawnDog();
        int after = CountDogs();
        if (after > before)
        {
            _spaceSpawnsDog = true;
            _dogSpawned = true;
            _dogSpawnTime = Time.time;
            _dogFirstX = player.transform.position.x;
            _dogLastX = _dogFirstX;
            _dogFirstY = player.transform.position.y;
        }
    }

    /// <summary>
    /// A second spawn inside the cooldown window has to be rejected, otherwise
    /// the player could spam the spacebar and fill the screen with dogs.
    /// </summary>
    private void TestSpawnCooldown()
    {
        if (_cooldownTested || !_spaceSpawnsDog)
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
            var mover = dog.GetComponent<MoveForwardX>();
            if (mover == null)
            {
                continue;
            }

            _dogSpeed = mover.speed;

            if (!_dogSpawned)
            {
                continue;
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
                // the dog should stay on the ground
                _results.Add("FAIL dog-stays-on-ground: y moved to " + y.ToString("F2", CultureInfo.InvariantCulture));
                _dogSpawned = false;
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
    /// Walks the active scene instead of using Object.FindGameObjectsOfType, so the
    /// probe only looks at the challenge scene and stays clear of API differences
    /// between Unity versions.
    /// </summary>
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

    private static List<Bounds> FindDogModelBounds()
    {
        var bounds = new List<Bounds>();
        foreach (var dog in GameObject.FindGameObjectsWithTag("Dog"))
        {
            if (dog.GetComponent<MoveForwardX>() == null)
            {
                continue;
            }

            var renderers = dog.GetComponentsInChildren<Renderer>(true);
            bool first = true;
            Bounds merged = default;
            foreach (var renderer in renderers)
            {
                if (first)
                {
                    merged = renderer.bounds;
                    first = false;
                }
                else
                {
                    merged.Encapsulate(renderer.bounds);
                }
            }

            if (!first)
            {
                bounds.Add(merged);
            }
        }
        return bounds;
    }

    private void TrackBalls()
    {
        var alive = new HashSet<int>();
        var dogBounds = FindDogModelBounds();

        foreach (var ball in FindSceneBalls())
        {
            int id = ball.GetInstanceID();
            alive.Add(id);

            if (!_balls.ContainsKey(id))
            {
                _balls.Add(id, new BallState());
                _ballSpawnTimes.Add(Time.time - _startTime);
                _ballTypesSeen.Add(ball.name);

                if (ball.transform.position.y > SpawnPosY - 2f)
                {
                    _ballFellFromTop = true;
                }
            }

            BallState state = _balls[id];
            state.LastY = ball.transform.position.y;

            // The ball may only vanish while it is inside the dog's MODEL, not
            // merely inside an oversized collider. The margin covers the distance
            // the ball travels in one frame.
            Vector3 ballPosition = ball.transform.position;
            foreach (Bounds model in dogBounds)
            {
                Bounds contact = model;
                contact.Expand(0.8f);
                if (contact.Contains(ballPosition))
                {
                    state.TouchedDog = true;
                    break;
                }
            }
        }

        // A ball that is gone again was destroyed. It either hit the dog or it
        // reached the ground; the Game Over log count tells the two apart.
        foreach (var pair in _balls)
        {
            if (alive.Contains(pair.Key))
            {
                continue;
            }

            BallState state = pair.Value;
            if (state.TouchedDog)
            {
                _ballCaughtByDog = true;
            }
            else
            {
                _ballsReachingGround++;
                _groundDetail += (state.LastY.ToString("F2", CultureInfo.InvariantCulture) + " ");
            }
        }

        foreach (var id in new List<int>(_balls.Keys))
        {
            if (!alive.Contains(id))
            {
                _balls.Remove(id);
            }
        }
    }

    /// <summary>
    /// The dog collider has to be sized to the dog model. The starter file ships
    /// a collider that is about 13 times too long and 8 times too high, which is
    /// what made the balls disappear when they merely came near the dog.
    /// </summary>
    private bool CheckColliderMatchesModel()
    {
        foreach (var dog in GameObject.FindGameObjectsWithTag("Dog"))
        {
            var box = dog.GetComponent<BoxCollider>();
            if (box == null)
            {
                continue;
            }

            var renderers = dog.GetComponentsInChildren<Renderer>(true);
            bool first = true;
            Bounds model = default;
            foreach (var renderer in renderers)
            {
                if (first)
                {
                    model = renderer.bounds;
                    first = false;
                }
                else
                {
                    model.Encapsulate(renderer.bounds);
                }
            }

            if (first)
            {
                continue;
            }

            Vector3 colliderSize = box.bounds.size;
            Vector3 modelSize = model.size;
            for (int i = 0; i < 3; i++)
            {
                if (modelSize[i] <= 0f)
                {
                    continue;
                }
                if (Mathf.Abs(colliderSize[i] - modelSize[i]) > modelSize[i] * 0.25f)
                {
                    _colliderDetail = "collider " + colliderSize.ToString("F2", CultureInfo.InvariantCulture) +
                                      " vs model " + modelSize.ToString("F2", CultureInfo.InvariantCulture) +
                                      " (renderers " + renderers.Length + ")";
                    return false;
                }
            }
            return true;
        }

        return false;
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
            if (delta < MinInterval - 0.35f || delta > MaxInterval + 0.35f)
            {
                intervalsOk = false;
            }
        }

        _results.Add((_spaceSpawnsDog ? "PASS" : "FAIL") + " space-spawns-dog: the player can send out a dog");
        _results.Add((_cooldownHolds ? "PASS" : "FAIL") + " spawn-cooldown: a second spawn inside the cooldown is rejected");
        _results.Add((_dogMovedLeft ? "PASS" : "FAIL") + " dog-runs-left: the dog travels towards -X");
        _results.Add((_dogDestroyed ? "PASS" : "FAIL") + " dog-destroyed-off-screen: the dog is cleaned up at x < " + LeftLimit);
        _results.Add((_ballFellFromTop ? "PASS" : "FAIL") + " ball-spawns-at-top: balls appear near y = " + SpawnPosY);
        _results.Add((_ballSpawnTimes.Count > 0 ? "PASS" : "FAIL") + " ball-spawns: balls keep spawning");
        _results.Add((_ballTypesSeen.Count >= 2 ? "PASS" : "FAIL") + " ball-type-random: saw " + _ballTypesSeen.Count + " different ball types");
        _results.Add((intervalsOk ? "PASS" : "FAIL") + " spawn-interval-random: intervals " + string.Join(", ", intervals.ConvertAll(v => v.ToString("F2", CultureInfo.InvariantCulture))));
        _results.Add((_ballCaughtByDog ? "PASS" : "FAIL") + " ball-destroyed-on-contact: a ball dies when the dog touches it");
        _results.Add((_ballsReachingGround > 0 ? "PASS" : "FAIL") + " ball-destroyed-at-bottom: a ball that reached the ground is cleaned up");
        _results.Add((_gameOverCount == _ballsReachingGround && _gameOverCount > 0 ? "PASS" : "FAIL") +
                     " game-over-message: every ball that reached the ground reported Game Over (" +
                     _gameOverCount + " messages, " + _ballsReachingGround + " balls, last y " + _groundDetail + ")");
        _results.Add((CheckColliderMatchesModel() ? "PASS" : "FAIL") + " dog-collider-matches-model: the collider is sized to the dog model - " + _colliderDetail);

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
