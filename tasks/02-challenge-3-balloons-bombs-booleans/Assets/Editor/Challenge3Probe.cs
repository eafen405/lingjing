using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play mode probe for Challenge 3 - Balloons, Bombs, &amp; Booleans.
///
/// Opens the challenge scene, enters play mode and asserts the observable
/// behaviour the assignment asks for:
///   - the player can control the balloon and it never floats above the
///     ceiling (isLowEnough)
///   - the background scrolls while the game runs and stops when the game is
///     over
///   - the background repeats seamlessly, after half of its WIDTH
///   - bombs and money are spawned on a timer
///   - collecting money plays the fireworks at the balloon's position
///   - hitting a bomb explodes and ends the round
///   - the balloon bounces off the ground and makes a sound
///
/// The probe drives the game through <c>PlayerControllerX.Float()</c> rather
/// than a synthetic key press, because legacy <c>Input</c> cannot be faked in
/// a head-less batch mode session. The space bar binding itself is covered by
/// the static wiring check in tools/check-wiring.ps1 and by the build smoke test.
///
/// The two props that ship with the scene (a bomb and a money token right
/// below the balloon) would end the round within a second, so the probe
/// deactivates them and spawns its own money and bomb later on.
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod Challenge3Probe.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;report path&gt;"
///
/// Note: do NOT pass -quit. The editor has to stay alive for the play mode
/// callback to run; the probe exits the editor itself once the report is out.
/// </summary>
[InitializeOnLoad]
public static class Challenge3Probe
{
    private const string SceneKey = "Challenge3Probe.Scene";
    internal const string ReportKey = "Challenge3Probe.Report";
    private const string StartKey = "Challenge3Probe.Start";
    private const double WatchdogSeconds = 300d;

    static Challenge3Probe()
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
            : "Assets/Challenge 3/Challenge 3.unity";
        string report = separator >= 0 && separator + 2 < args.Length
            ? args[separator + 2]
            : "challenge3-probe.txt";

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

        var host = new GameObject("Challenge3ProbeAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Challenge3ProbeAgent>();
    }
}

/// <summary>Runs the scenario, collects results and writes the report.</summary>
public class Challenge3ProbeAgent : MonoBehaviour
{
    // The probe drives the game in phases. The times are seconds of play mode.
    private const float SetupEnd = 0.5f;
    private const float SpawnWatchEnd = 12f;
    private const float FloatEnd = 18f;
    private const float MoneyEnd = 20.5f;
    private const float BombEnd = 24f;
    private const float FinishTime = 25f;

    private readonly List<string> _results = new List<string>();
    private readonly List<float> _spawnTimes = new List<float>();
    private readonly HashSet<string> _spawnTags = new HashSet<string>();
    private readonly HashSet<int> _seenProps = new HashSet<int>();
    private readonly Dictionary<string, GameObject> _cachedPrefabs = new Dictionary<string, GameObject>();
    private readonly List<string> _errorMessages = new List<string>();

    private Transform _background;
    private Transform _player;
    private PlayerControllerX _controller;
    private Rigidbody _playerRb;
    private AudioSource _playerAudio;
    private float _backgroundWidth;

    private float _startTime;
    private float _backgroundStartX;
    private float _lastBackgroundX;
    private bool _backgroundMovedLeft;
    private bool _backgroundWrapped;
    private float _wrapFromX;

    private float _fallingSpeed;
    private bool _bounced;
    private float _firstSoundTime = -1f;
    private float _lowestPlayerY;
    private float _highestPlayerY;
    private bool _ceilingReached;
    private bool _floating;

    private int _moneyId = -1;
    private bool _moneySpawned;
    private bool _moneyCollected;
    private bool _fireworksOnBalloon;
    private bool _fireworksPlayed;

    private int _bombId = -1;
    private bool _bombSpawned;
    private bool _bombHit;
    private bool _explosionPlayed;

    private bool _gameOverLogged;
    private bool _gameOverRecorded;
    private float _backgroundXAtGameOver;
    private bool _backgroundKeptMoving;

    private bool _wiringChecked;
    private bool _setupDone;
    private bool _spawnerStopped;
    private bool _finished;
    private int _errorCount;

    private void Start()
    {
        _startTime = Time.time;
        Application.logMessageReceived += OnLog;

        var background = GameObject.Find("Background");
        var player = GameObject.Find("Player");
        if (background == null || player == null)
        {
            AddCheck(false, "scene-objects", "Background or Player missing from the scene");
            return;
        }

        _background = background.transform;
        _player = player.transform;
        _controller = player.GetComponent<PlayerControllerX>();
        _playerRb = player.GetComponent<Rigidbody>();
        _playerAudio = player.GetComponent<AudioSource>();

        var backgroundCollider = background.GetComponent<BoxCollider>();
        _backgroundWidth = backgroundCollider != null ? backgroundCollider.size.x : 0f;

        _backgroundStartX = _background.position.x;
        _lastBackgroundX = _backgroundStartX;
        _lowestPlayerY = _player.position.y;
        _highestPlayerY = _player.position.y;

        // Remember the props before the spawn manager is taken out of the game.
        GetPrefab("Money");
        GetPrefab("Bomb");
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            _errorCount++;
            if (_errorMessages.Count < 8)
            {
                _errorMessages.Add(condition);
            }
        }
        else if (condition.Contains("Game Over"))
        {
            _gameOverLogged = true;
        }
    }

    /// <summary>
    /// The physics rate is the reliable clock in a batch mode session: the
    /// editor can render far fewer frames than the physics steps, so
    /// everything that has to catch a short event (a bounce, a sound, a
    /// particle effect) is watched here.
    /// </summary>
    private void FixedUpdate()
    {
        if (_finished || _controller == null || _playerRb == null)
        {
            return;
        }

        float elapsed = Time.time - _startTime;

        if (elapsed >= SpawnWatchEnd && elapsed < FloatEnd)
        {
            // Hold down the space bar: the balloon floats up to the ceiling.
            _controller.Float();
            _floating = true;
        }

        float verticalSpeed = _playerRb.velocity.y;
        if (verticalSpeed > 0.5f && _fallingSpeed < -1f && _player.position.y < 3f)
        {
            // The balloon was falling and now moves up again: it bounced. The
            // contact solver sets the speed to zero for a single step, so the
            // last downward speed is kept instead of the previous one.
            _bounced = true;
        }
        if (verticalSpeed < -1f)
        {
            _fallingSpeed = verticalSpeed;
        }

        if (_firstSoundTime < 0f && _playerAudio != null && _playerAudio.isPlaying)
        {
            _firstSoundTime = Time.time - _startTime;
        }

        if (!_fireworksPlayed && _controller.fireworksParticle != null && _controller.fireworksParticle.isPlaying)
        {
            _fireworksPlayed = true;
        }

        if (!_explosionPlayed && _controller.explosionParticle != null && _controller.explosionParticle.isPlaying)
        {
            _explosionPlayed = true;
        }
    }

    private void Update()
    {
        if (_finished)
        {
            return;
        }

        float elapsed = Time.time - _startTime;

        CheckWiring();

        if (_controller == null || _playerRb == null)
        {
            if (elapsed > 2f)
            {
                Finish();
            }
            return;
        }

        if (elapsed < SetupEnd)
        {
            PrepareScene();
        }
        else if (elapsed < SpawnWatchEnd)
        {
            TrackSpawns();
        }
        else if (elapsed < FloatEnd)
        {
            StopSpawner();
        }
        else if (elapsed < MoneyEnd)
        {
            SpawnMoney();
        }
        else if (elapsed < BombEnd)
        {
            SpawnBomb();
        }

        TrackBackground();
        TrackPlayer();

        if (elapsed >= FinishTime)
        {
            Finish();
        }
    }

    // ------------------------------------------------------------- wiring ---

    /// <summary>Scene wiring the challenge depends on.</summary>
    private void CheckWiring()
    {
        if (_wiringChecked)
        {
            return;
        }

        _wiringChecked = true;

        var player = GameObject.Find("Player");
        var background = GameObject.Find("Background");
        var ground = GameObject.Find("Ground");
        var spawner = GameObject.Find("Spawn Manager");

        bool playerOk = player != null
                        && player.GetComponent<PlayerControllerX>() != null
                        && player.GetComponent<Rigidbody>() != null
                        && player.GetComponent<AudioSource>() != null
                        && player.GetComponent<Collider>() != null;
        AddCheck(playerOk, "wiring-player",
            "the Player has a PlayerControllerX, a Rigidbody, an AudioSource and a collider");

        bool partsOk = _controller != null
                       && _controller.fireworksParticle != null
                       && _controller.explosionParticle != null
                       && _controller.moneySound != null
                       && _controller.explodeSound != null
                       && _controller.bounceSound != null;
        AddCheck(partsOk, "wiring-player-parts",
            "the player references the fireworks, the explosion and all three sounds");

        bool backgroundOk = background != null
                            && background.CompareTag("Background")
                            && background.GetComponent<BoxCollider>() != null;
        AddCheck(backgroundOk, "wiring-background",
            "the Background is tagged and has a BoxCollider");

        bool groundOk = ground != null
                        && ground.CompareTag("Ground")
                        && ground.GetComponent<BoxCollider>() != null
                        && ground.GetComponent<BoxCollider>().enabled;
        AddCheck(groundOk, "wiring-ground",
            "the Ground is tagged and has an enabled BoxCollider");

        var manager = spawner != null ? spawner.GetComponent<SpawnManagerX>() : null;
        bool prefabsOk = manager != null && manager.objectPrefabs != null && manager.objectPrefabs.Length == 2;
        if (prefabsOk)
        {
            bool money = false;
            bool bomb = false;
            foreach (var prefab in manager.objectPrefabs)
            {
                if (prefab == null)
                {
                    prefabsOk = false;
                    continue;
                }
                if (prefab.CompareTag("Money")) money = true;
                if (prefab.CompareTag("Bomb")) bomb = true;
            }
            prefabsOk = prefabsOk && money && bomb;
        }
        AddCheck(prefabsOk, "wiring-spawner",
            "the spawn manager holds one Money and one Bomb prefab");
    }

    // -------------------------------------------------------------- phases ---

    /// <summary>
    /// The scene ships with a bomb and a money token right below the balloon.
    /// They would end the round within a second, so the probe takes them out
    /// of the way and spawns its own props later on.
    /// </summary>
    private void PrepareScene()
    {
        if (_setupDone)
        {
            return;
        }

        _setupDone = true;

        foreach (var go in GameObject.FindGameObjectsWithTag("Bomb"))
        {
            if (go.transform.position.x < 10f)
            {
                go.SetActive(false);
            }
        }
        foreach (var go in GameObject.FindGameObjectsWithTag("Money"))
        {
            if (go.transform.position.x < 10f)
            {
                go.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Stops the spawn manager once enough spawns have been observed, so the
    /// balloon can float up to the ceiling without running into a prop. The
    /// props that are still on their way are cleaned up as well.
    /// </summary>
    private void StopSpawner()
    {
        if (_spawnerStopped)
        {
            return;
        }

        _spawnerStopped = true;
        var spawner = GameObject.Find("Spawn Manager");
        if (spawner != null)
        {
            // Deactivating the object stops the repeating invoke for good.
            spawner.SetActive(false);
        }

        foreach (var go in GameObject.FindGameObjectsWithTag("Bomb"))
        {
            Destroy(go);
        }
        foreach (var go in GameObject.FindGameObjectsWithTag("Money"))
        {
            Destroy(go);
        }
    }

    private void TrackSpawns()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Bomb"))
        {
            if (go.transform.position.x > 20f && _seenProps.Add(go.GetInstanceID()))
            {
                _spawnTimes.Add(Time.time - _startTime);
                _spawnTags.Add("Bomb");
            }
        }
        foreach (var go in GameObject.FindGameObjectsWithTag("Money"))
        {
            if (go.transform.position.x > 20f && _seenProps.Add(go.GetInstanceID()))
            {
                _spawnTimes.Add(Time.time - _startTime);
                _spawnTags.Add("Money");
            }
        }
    }

    private void SpawnMoney()
    {
        if (_moneySpawned)
        {
            return;
        }

        var prefab = GetPrefab("Money");
        if (prefab == null)
        {
            return;
        }

        _moneySpawned = true;
        var money = Instantiate(prefab, _player.position, prefab.transform.rotation);
        _moneyId = money.GetInstanceID();

        // The token has to stay on the balloon, so it must not scroll away.
        var mover = money.GetComponent<MoveLeftX>();
        if (mover != null)
        {
            mover.enabled = false;
        }
    }

    private void SpawnBomb()
    {
        if (_bombSpawned)
        {
            return;
        }

        var prefab = GetPrefab("Bomb");
        if (prefab == null)
        {
            return;
        }

        _bombSpawned = true;
        var bomb = Instantiate(prefab, _player.position, prefab.transform.rotation);
        _bombId = bomb.GetInstanceID();

        var mover = bomb.GetComponent<MoveLeftX>();
        if (mover != null)
        {
            mover.enabled = false;
        }
    }

    private GameObject GetPrefab(string tag)
    {
        if (_cachedPrefabs.ContainsKey(tag))
        {
            return _cachedPrefabs[tag];
        }

        var spawner = GameObject.Find("Spawn Manager");
        if (spawner == null)
        {
            return null;
        }

        var manager = spawner.GetComponent<SpawnManagerX>();
        if (manager == null || manager.objectPrefabs == null)
        {
            return null;
        }

        foreach (var prefab in manager.objectPrefabs)
        {
            if (prefab != null && prefab.CompareTag(tag))
            {
                _cachedPrefabs[tag] = prefab;
                return prefab;
            }
        }

        return null;
    }

    private static bool Exists(int id)
    {
        if (id < 0)
        {
            return false;
        }

        foreach (var go in GameObject.FindGameObjectsWithTag("Money"))
        {
            if (go.GetInstanceID() == id)
            {
                return true;
            }
        }
        foreach (var go in GameObject.FindGameObjectsWithTag("Bomb"))
        {
            if (go.GetInstanceID() == id)
            {
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------ tracking ---

    private void TrackBackground()
    {
        float x = _background.position.x;
        if (x < _lastBackgroundX - 0.01f)
        {
            _backgroundMovedLeft = true;
        }
        else if (x > _lastBackgroundX + 1f)
        {
            // The background snapped back to its start position: it repeated.
            _backgroundWrapped = true;
            _wrapFromX = _lastBackgroundX;
        }

        _lastBackgroundX = x;
    }

    private void TrackPlayer()
    {
        float y = _player.position.y;
        if (y < _lowestPlayerY)
        {
            _lowestPlayerY = y;
        }
        if (y > _highestPlayerY)
        {
            _highestPlayerY = y;
        }

        if (!_controller.isLowEnough && y > _controller.maxHeight - 0.5f)
        {
            _ceilingReached = true;
        }

        if (_moneySpawned && !_moneyCollected && !Exists(_moneyId))
        {
            _moneyCollected = true;
        }
        if (_moneyCollected && !_fireworksOnBalloon && _controller.fireworksParticle != null)
        {
            _fireworksOnBalloon = Vector3.Distance(_controller.fireworksParticle.transform.position, _player.position) < 0.05f;
        }

        if (_bombSpawned && !_bombHit && !Exists(_bombId))
        {
            _bombHit = true;
        }

        if (_gameOverLogged && !_gameOverRecorded)
        {
            _gameOverRecorded = true;
            _backgroundXAtGameOver = _background.position.x;
        }
        else if (_gameOverRecorded && !_backgroundKeptMoving)
        {
            if (Mathf.Abs(_background.position.x - _backgroundXAtGameOver) > 0.05f)
            {
                _backgroundKeptMoving = true;
            }
        }
    }

    // ------------------------------------------------------------- report ---

    private void AddCheck(bool ok, string name, string detail)
    {
        _results.Add((ok ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private void Finish()
    {
        _finished = true;

        float ceiling = _controller != null ? _controller.maxHeight : 0f;
        float expectedWrap = _backgroundStartX - _backgroundWidth / 2f;

        // --- the achievement conditions of the challenge --------------------
        AddCheck(_floating && _highestPlayerY > _lowestPlayerY + 5f, "player-controls-the-balloon",
            "the balloon rose from y=" + F(_lowestPlayerY) + " to y=" + F(_highestPlayerY));

        AddCheck(_backgroundMovedLeft, "background-scrolls-while-playing",
            "the background moved to the left from x=" + F(_backgroundStartX) + " to x=" + F(_lastBackgroundX));

        AddCheck(_backgroundWrapped, "background-repeats",
            "the background jumped back to its start position " + (_backgroundWrapped ? "at x=" + F(_wrapFromX) : "never"));

        AddCheck(_backgroundWrapped && Mathf.Abs(_wrapFromX - expectedWrap) < 1.5f, "background-repeats-after-half-its-width",
            "the repeat happened at x=" + F(_wrapFromX) + ", expected " + F(expectedWrap) + " (half of the " + F(_backgroundWidth) + " wide background)");

        _spawnTimes.Sort();
        bool intervalOk = _spawnTimes.Count >= 3;
        var intervals = new List<float>();
        for (int i = 1; i < _spawnTimes.Count; i++)
        {
            float delta = _spawnTimes[i] - _spawnTimes[i - 1];
            intervals.Add(delta);
            if (delta < 1.0f || delta > 2.2f)
            {
                intervalOk = false;
            }
        }
        AddCheck(intervalOk, "spawns-on-a-timer",
            _spawnTimes.Count + " props appeared, intervals " + Join(intervals));

        AddCheck(_spawnTags.Contains("Bomb") && _spawnTags.Contains("Money"), "spawns-bombs-and-money",
            "spawned prop types: " + (_spawnTags.Count == 0 ? "none" : string.Join(", ", _spawnTags.ToArray())));

        AddCheck(_moneyCollected && _fireworksPlayed, "money-plays-the-fireworks",
            "the money token was collected, the fireworks played: " + _fireworksPlayed);

        AddCheck(_fireworksOnBalloon, "fireworks-at-the-balloon",
            "the fireworks sit on the balloon instead of next to it");

        AddCheck(_bombHit && _explosionPlayed, "bomb-plays-the-explosion",
            "the bomb was collected, the explosion played: " + _explosionPlayed);

        AddCheck(_gameOverLogged, "game-over-is-reported",
            "the round ends with a Game Over message");

        AddCheck(_gameOverLogged && !_backgroundKeptMoving, "background-stops-on-game-over",
            "the background stopped moving after the round ended");

        // --- the two bonus items --------------------------------------------
        AddCheck(_ceilingReached && _highestPlayerY <= ceiling + 0.25f, "balloon-never-floats-too-high",
            "isLowEnough turned false at the ceiling, highest y was " + F(_highestPlayerY) + " (limit " + F(ceiling) + ")");

        AddCheck(_bounced, "balloon-bounces-off-the-ground",
            "the balloon fell, hit the ground and moved up again");

        AddCheck(_firstSoundTime >= 0f && _firstSoundTime < SpawnWatchEnd, "bounce-plays-a-sound",
            "the first sound effect was played at t=" + F(_firstSoundTime) + " (the ground bounce)");

        AddCheck(_lowestPlayerY > -2f, "balloon-stays-above-the-ground",
            "the lowest y of the balloon was " + F(_lowestPlayerY));

        AddCheck(_errorCount == 0, "no-errors",
            _errorCount == 0 ? "no error was logged" : string.Join(" | ", _errorMessages.ToArray()));

        int failed = 0;
        foreach (var result in _results)
        {
            if (result.StartsWith("FAIL"))
            {
                failed++;
            }
        }

        var report = new StringBuilder();
        report.AppendLine("Challenge 3 - Balloons, Bombs, & Booleans probe");
        report.AppendLine(new string('-', 60));
        foreach (var result in _results)
        {
            report.AppendLine(result);
        }
        report.AppendLine(new string('-', 60));
        report.AppendLine($"checks: {_results.Count}  failed: {failed}");

        string path = SessionState.GetString(Challenge3Probe.ReportKey, "challenge3-probe.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("PROBE-REPORT-BEGIN\n" + report + "PROBE-REPORT-END");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }

    private static string F(float value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Join(List<float> values)
    {
        var parts = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            parts[i] = F(values[i]);
        }
        return string.Join(", ", parts);
    }
}
