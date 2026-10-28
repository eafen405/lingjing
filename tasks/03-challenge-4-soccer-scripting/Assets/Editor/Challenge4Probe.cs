using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play mode probe for Challenge 4 - Soccer Scripting.
///
/// Opens the challenge scene, enters play mode and asserts the observable
/// behaviour the assignment asks for:
///   - an enemy ball moves towards the "Player Goal"
///   - a collision pushes the enemy ball away from the player
///   - the powerup is picked up and wears off after its duration
///   - a new wave spawns once the enemy balls are cleared, with one more
///     enemy ball than the wave before and a higher enemy speed
///   - the space bar dash speeds the player up and fires the smoke effect
///     that hangs under the focal point
///
/// The legacy <c>Input</c> manager cannot be faked in a head-less batch mode
/// session, so the dash is triggered through the public
/// <c>PlayerControllerX.Dash()</c> method. The space bar binding itself is
/// covered by the static wiring check in tools/check-wiring.ps1 and later by
/// the build smoke test.
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod Challenge4Probe.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;report path&gt;"
///
/// Note: do NOT pass -quit. The editor has to stay alive for the play mode
/// callback to run; the probe exits the editor itself once the report is out.
/// </summary>
[InitializeOnLoad]
public static class Challenge4Probe
{
    private const string SceneKey = "Challenge4Probe.Scene";
    internal const string ReportKey = "Challenge4Probe.Report";
    private const string StartKey = "Challenge4Probe.Start";
    private const double WatchdogSeconds = 300d;

    static Challenge4Probe()
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
            : "Assets/Challenge 4/Challenge 4.unity";
        string report = separator >= 0 && separator + 2 < args.Length
            ? args[separator + 2]
            : "challenge4-probe.txt";

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

        var host = new GameObject("Challenge4ProbeAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Challenge4ProbeAgent>();
    }
}

/// <summary>Runs the scenario, collects results and writes the report.</summary>
public class Challenge4ProbeAgent : MonoBehaviour
{
    /// <summary>How long the probe watches an enemy ball to see it approach the goal.</summary>
    private const float MoveWindow = 1.5f;

    /// <summary>How much closer to the goal the enemy ball has to come.</summary>
    private const float MoveThreshold = 2f;

    /// <summary>How long the probe waits before it clears the enemy balls itself.</summary>
    private const float ForceWaveDeadline = 14f;

    /// <summary>How long the probe waits for the wave to be visible.</summary>
    private const float WaveTimeout = 6f;

    /// <summary>Overall budget for the probe run.</summary>
    private const float TimeoutSeconds = 45f;

    private readonly List<string> _results = new List<string>();
    private readonly List<string> _errors = new List<string>();
    private readonly Dictionary<int, int> _enemiesPerWave = new Dictionary<int, int>();
    private readonly Dictionary<int, float> _speedPerWave = new Dictionary<int, float>();

    private float _startTime;
    private float _lastHeartbeat = -1f;
    private int _step;
    private bool _finished;

    // step 1 - the enemy ball walks towards the player goal
    private bool _watching;
    private int _watchedEnemyId = -1;
    private float _watchStart;
    private float _watchStartDistance = -1f;
    private float _watchMinDistance = float.MaxValue;

    // step 2 - the knock back
    private bool _knockBackPlaced;
    private bool _knockBackDone;
    private int _knockBackEnemyId = -1;
    private Vector3 _knockBackDirection;
    private float _knockBackAlong;
    private float _knockBackStart;

    // step 3 - the powerup
    private bool _powerupCollected;
    private bool _powerupPickedUp;
    private float _powerupTime;
    private float _powerupDuration = 5f;

    // step 4 - the waves
    private bool _enemiesCleared;
    private bool _enemySeen;
    private bool _waveAdvanced;
    private bool _forcedClear;
    private int _highestWave;
    private float _waveCheckTime = -1f;

    // step 5 - the dash
    private bool _dashPending;
    private float _dashStart;
    private float _dashSpeed;

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
            _errors.Add(condition);
        }
    }

    private void Update()
    {
        if (_finished)
        {
            return;
        }

        float elapsed = Time.time - _startTime;
        TrackWaves();
        Heartbeat(elapsed);

        switch (_step)
        {
            case 0:
                RunWiringChecks();
                break;
            case 1:
                RunEnemyMovement();
                break;
            case 2:
                RunKnockBack();
                break;
            case 3:
                RunPowerup();
                break;
            case 4:
                RunWaves();
                break;
            case 5:
                RunDash();
                break;
        }

        if (elapsed > TimeoutSeconds)
        {
            _results.Add("FAIL probe-timeout: the probe did not finish within " +
                         TimeoutSeconds.ToString("F0", CultureInfo.InvariantCulture) + " seconds");
            Finish();
        }
    }

    // ------------------------------------------------------------- wiring ---

    /// <summary>Scene wiring the challenge expects.</summary>
    private void RunWiringChecks()
    {
        var player = GameObject.Find("Player");
        var spawner = GameObject.Find("Spawn Manager");
        var focalPoint = GameObject.Find("Focal Point");

        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        var manager = spawner != null ? spawner.GetComponent<SpawnManagerX>() : null;

        Check(controller != null, "wiring-player-controller", "the player carries a PlayerControllerX");
        Check(manager != null, "wiring-spawn-manager", "the spawn manager carries a SpawnManagerX");

        if (controller != null)
        {
            Check(controller.powerupIndicator != null, "wiring-powerup-indicator", "the player has a powerup indicator");
            Check(controller.dashParticleEffect != null, "wiring-dash-effect", "the player controller knows the smoke effect");
        }

        if (manager != null)
        {
            Check(manager.enemyPrefab != null && manager.enemyPrefab.CompareTag("Enemy"),
                "wiring-enemy-prefab", "the spawn manager spawns an enemy prefab tagged Enemy");
            Check(manager.powerupPrefab != null && manager.powerupPrefab.CompareTag("Powerup"),
                "wiring-powerup-prefab", "the spawn manager spawns a powerup prefab tagged Powerup");
        }

        var smoke = controller != null ? controller.dashParticleEffect : null;
        bool underFocalPoint = smoke != null && focalPoint != null && smoke.transform.IsChildOf(focalPoint.transform);
        Check(underFocalPoint, "wiring-smoke-under-focal-point", "the smoke effect hangs under the Focal Point");

        _step = 1;
    }

    // ------------------------------------------------------- enemy movement ---

    /// <summary>
    /// Watches one enemy ball and checks that it comes closer to the goal the
    /// player defends.
    /// </summary>
    private void RunEnemyMovement()
    {
        var goal = GameObject.Find("Player Goal");
        if (goal == null)
        {
            _results.Add("FAIL enemy-moves-to-goal: the scene has no Player Goal");
            _step = 2;
            return;
        }

        if (!_watching)
        {
            var enemy = FindEnemy();
            if (enemy == null)
            {
                if (Time.time - _startTime > 8f)
                {
                    _results.Add("FAIL enemy-moves-to-goal: no enemy ball was spawned");
                    _step = 2;
                }
                return;
            }

            _watching = true;
            _watchedEnemyId = enemy.GetInstanceID();
            _watchStart = Time.time;
            _watchStartDistance = HorizontalDistance(enemy.transform.position, goal.transform.position);
            _watchMinDistance = _watchStartDistance;
            return;
        }

        var watched = FindEnemyById(_watchedEnemyId);
        if (watched == null)
        {
            _results.Add("FAIL enemy-moves-to-goal: the enemy ball was gone before it came closer");
            _step = 2;
            return;
        }

        _watchMinDistance = Mathf.Min(_watchMinDistance, HorizontalDistance(watched.transform.position, goal.transform.position));

        if (Time.time - _watchStart < MoveWindow)
        {
            return;
        }

        float gain = _watchStartDistance - _watchMinDistance;
        bool moved = gain >= MoveThreshold;
        _results.Add((moved ? "PASS" : "FAIL") + " enemy-moves-to-goal: the enemy ball came " +
                     gain.ToString("F2", CultureInfo.InvariantCulture) + " closer to the Player Goal");
        _step = 2;
    }

    // ---------------------------------------------------------- knock back ---

    /// <summary>
    /// Drives the player into an enemy ball and checks that the ball is pushed
    /// away from the player instead of towards it.
    /// </summary>
    private void RunKnockBack()
    {
        if (_knockBackDone)
        {
            _step = 3;
            return;
        }

        var player = GameObject.Find("Player");
        var playerRb = player != null ? player.GetComponent<Rigidbody>() : null;
        if (player == null || playerRb == null)
        {
            _results.Add("FAIL knock-back: the player is missing");
            _knockBackDone = true;
            _step = 3;
            return;
        }

        if (!_knockBackPlaced)
        {
            var enemy = FindEnemy();
            if (enemy == null)
            {
                if (Time.time - _startTime > 12f)
                {
                    _results.Add("FAIL knock-back: there was no enemy ball to collide with");
                    _knockBackDone = true;
                    _step = 3;
                }
                return;
            }

            var enemyRb = enemy.GetComponent<Rigidbody>();
            if (enemyRb == null)
            {
                _results.Add("FAIL knock-back: the enemy ball has no rigidbody");
                _knockBackDone = true;
                _step = 3;
                return;
            }

            // Stop the enemy ball first, so the measurement only sees the
            // knock back and not the ball rolling towards the goal.
            enemyRb.WakeUp();
            enemyRb.velocity = Vector3.zero;
            enemyRb.angularVelocity = Vector3.zero;

            // The player is placed next to the enemy ball, sideways to the
            // direction the ball is rolling, so that the knock back cannot be
            // confused with the roll. The collider of the enemy ball sits
            // above its origin, so the player is raised as well. The rigidbody
            // pose is used, because the transform of a rolling ball lags one
            // physics step behind. The transform is moved too, because the
            // knock back reads transform.position while the collision is
            // reported.
            var goal = GameObject.Find("Player Goal");
            Vector3 enemyPos = enemyRb.position;
            Vector3 roll = goal != null ? goal.transform.position - enemyPos : Vector3.forward;
            roll.y = 0f;
            roll = roll.sqrMagnitude > 0f ? roll.normalized : Vector3.forward;
            Vector3 sideways = new Vector3(roll.z, 0f, -roll.x);

            Vector3 place = enemyPos + new Vector3(0f, 0.7377f, 0f) + sideways * 0.9f;
            playerRb.WakeUp();
            player.transform.position = place;
            Physics.SyncTransforms();
            playerRb.velocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;

            _knockBackEnemyId = enemy.GetInstanceID();
            _knockBackDirection = (enemyPos - place).normalized;
            _knockBackStart = Time.time;
            _knockBackPlaced = true;
            return;
        }

        var watched = FindEnemyById(_knockBackEnemyId);
        if (watched == null)
        {
            _results.Add("FAIL knock-back: the enemy ball vanished during the collision");
            _knockBackDone = true;
            _step = 3;
            return;
        }

        var watchedBody = watched.GetComponent<Rigidbody>();
        float along = Vector3.Dot(watchedBody.velocity, _knockBackDirection);
        if (along > _knockBackAlong)
        {
            _knockBackAlong = along;
        }

        if (Time.time - _knockBackStart < 1.5f)
        {
            return;
        }

        if (_knockBackAlong > 1f)
        {
            _knockBackDone = true;
            _results.Add("PASS knock-back: the enemy ball was pushed " +
                         _knockBackAlong.ToString("F2", CultureInfo.InvariantCulture) + " away from the player");
            _step = 3;
            return;
        }

        _knockBackDone = true;
        _results.Add("FAIL knock-back: the enemy ball was not pushed away from the player");
        _step = 3;
    }

    // ------------------------------------------------------------- powerup ---

    /// <summary>
    /// Drives the player onto the powerup and checks that the boost wears off
    /// after the configured duration.
    /// </summary>
    private void RunPowerup()
    {
        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        var playerRb = player != null ? player.GetComponent<Rigidbody>() : null;
        if (controller == null || playerRb == null)
        {
            _results.Add("FAIL powerup: the player is missing");
            _step = 4;
            return;
        }

        if (!_powerupCollected)
        {
            if (controller.hasPowerup)
            {
                // The player already ran into the powerup.
                _powerupCollected = true;
                _powerupTime = Time.time;
                _powerupDuration = controller.powerUpDuration;
                _powerupPickedUp = FindPowerup() == null;

                _results.Add((_powerupPickedUp ? "PASS" : "FAIL") +
                             " powerup-collected: the player picked up the powerup");
                _results.Add((controller.powerupIndicator != null && controller.powerupIndicator.activeSelf ? "PASS" : "FAIL") +
                             " powerup-indicator: the powerup indicator is shown while the boost lasts");
                return;
            }

            var powerup = FindPowerup();
            if (powerup == null)
            {
                if (Time.time - _startTime > 12f)
                {
                    _results.Add("FAIL powerup: no powerup was spawned");
                    _step = 4;
                }
                return;
            }

            playerRb.WakeUp();
            playerRb.position = powerup.transform.position;
            playerRb.velocity = Vector3.zero;
            return;
        }

        if (Time.time - _powerupTime < _powerupDuration + 0.75f)
        {
            return;
        }

        bool expired = !controller.hasPowerup;
        bool indicatorOff = controller.powerupIndicator == null || !controller.powerupIndicator.activeSelf;

        _results.Add((expired ? "PASS" : "FAIL") + " powerup-wears-off: the boost is gone after " +
                     _powerupDuration.ToString("F1", CultureInfo.InvariantCulture) + " seconds");
        _results.Add((indicatorOff ? "PASS" : "FAIL") + " powerup-indicator-off: the powerup indicator is hidden again");
        _step = 4;
    }

    // --------------------------------------------------------------- waves ---

    /// <summary>Records how many enemy balls and how fast they are in every wave.</summary>
    private void TrackWaves()
    {
        var manager = GetSpawnManager();
        if (manager == null)
        {
            return;
        }

        // The spawn manager counts up to the wave it spawns next, so the wave
        // that is on the field is waveCount - 1.
        int wave = manager.waveCount - 1;
        if (wave > _highestWave)
        {
            _highestWave = wave;
            _waveAdvanced = true;
        }

        // The spawn manager only spawns the next wave when it counted zero
        // enemy balls, so its own counter is the signal that the field was
        // cleared.
        if (manager.enemyCount == 0)
        {
            _enemiesCleared = true;
        }

        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0)
        {
            if (_enemySeen)
            {
                _enemiesCleared = true;
            }
        }
        else
        {
            _enemySeen = true;

            int seen = _enemiesPerWave.ContainsKey(wave) ? _enemiesPerWave[wave] : 0;
            if (enemies.Length > seen)
            {
                _enemiesPerWave[wave] = enemies.Length;
            }

            float fastest = 0f;
            foreach (var enemy in enemies)
            {
                var enemyX = enemy.GetComponent<EnemyX>();
                if (enemyX != null && enemyX.speed > fastest)
                {
                    fastest = enemyX.speed;
                }
            }

            float known = _speedPerWave.ContainsKey(wave) ? _speedPerWave[wave] : 0f;
            if (fastest > known)
            {
                _speedPerWave[wave] = fastest;
            }
        }
    }

    /// <summary>
    /// Waits for the second wave and checks that it holds one more enemy ball
    /// than the first one and that the enemy balls are faster now.
    /// </summary>
    private void RunWaves()
    {
        var manager = GetSpawnManager();
        if (manager == null)
        {
            _results.Add("FAIL waves: the spawn manager is missing");
            _step = 5;
            return;
        }

        if (manager.waveCount < 3)
        {
            if (!_forcedClear && Time.time - _startTime > ForceWaveDeadline)
            {
                // The enemy balls did not clear themselves, so the probe clears
                // them: that is the condition the spawn manager waits for.
                _forcedClear = true;
                foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                {
                    Destroy(enemy);
                }
            }
            return;
        }

        if (_waveCheckTime < 0f)
        {
            _waveCheckTime = Time.time;
            return;
        }

        if (Time.time - _waveCheckTime < 0.3f)
        {
            return;
        }

        int first = GetWaveEnemyCount(1);
        int second = GetWaveEnemyCount(2);
        float firstSpeed = GetWaveSpeed(1);
        float secondSpeed = GetWaveSpeed(2);

        _results.Add((_enemiesCleared ? "PASS" : "FAIL") +
                     " wave-spawns-after-clear: a new wave arrived once the enemy balls were cleared");
        _results.Add((second == first + 1 ? "PASS" : "FAIL") + " wave-grows-by-one: wave 1 held " +
                     first.ToString(CultureInfo.InvariantCulture) + " enemy ball(s), wave 2 held " +
                     second.ToString(CultureInfo.InvariantCulture));
        _results.Add((secondSpeed > firstSpeed ? "PASS" : "FAIL") + " enemies-get-faster: wave 1 speed " +
                     firstSpeed.ToString("F2", CultureInfo.InvariantCulture) + ", wave 2 speed " +
                     secondSpeed.ToString("F2", CultureInfo.InvariantCulture));

        _step = 5;
    }

    // ---------------------------------------------------------------- dash ---

    /// <summary>Checks that the turbo boost speeds the player up and fires the smoke.</summary>
    private void RunDash()
    {
        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;
        var playerRb = player != null ? player.GetComponent<Rigidbody>() : null;
        if (controller == null || playerRb == null)
        {
            _results.Add("FAIL dash: the player is missing");
            _step = 6;
            Finish();
            return;
        }

        var particles = controller.dashParticleEffect != null
            ? controller.dashParticleEffect.GetComponent<ParticleSystem>()
            : null;

        if (!_dashPending)
        {
            // Start from a standstill, so the boost is the only thing that
            // moves the player.
            playerRb.WakeUp();
            playerRb.velocity = Vector3.zero;
            controller.Dash();
            _dashPending = true;
            _dashStart = Time.time;
            return;
        }

        // The impulse is applied by the physics step, which does not run in
        // every frame, so the fastest horizontal speed of the next frames is
        // the measurement.
        float horizontal = new Vector2(playerRb.velocity.x, playerRb.velocity.z).magnitude;
        if (horizontal > _dashSpeed)
        {
            _dashSpeed = horizontal;
        }

        if (Time.time - _dashStart < 0.5f)
        {
            return;
        }

        _results.Add((_dashSpeed > 20f ? "PASS" : "FAIL") + " dash-speeds-up: the player was boosted to " +
                     _dashSpeed.ToString("F2", CultureInfo.InvariantCulture) + " units per second");
        _results.Add((particles != null && particles.isPlaying ? "PASS" : "FAIL") +
                     " dash-particle-effect: the smoke effect plays while the dash is active");

        _step = 6;
        Finish();
    }

    // -------------------------------------------------------------- report ---

    private void Check(bool ok, string name, string what)
    {
        _results.Add((ok ? "PASS" : "FAIL") + " " + name + ": " + what);
    }

    private void Finish()
    {
        _finished = true;

        _results.Add((_errors.Count == 0 ? "PASS" : "FAIL") + " no-errors: " +
                     (_errors.Count == 0 ? "nothing was logged as an error" : string.Join(" | ", _errors.ToArray())));

        int failed = 0;
        foreach (var result in _results)
        {
            if (result.StartsWith("FAIL")) failed++;
        }

        var report = new StringBuilder();
        report.AppendLine("Challenge 4 - Soccer Scripting probe");
        report.AppendLine(new string('-', 60));
        foreach (var result in _results)
        {
            report.AppendLine(result);
        }
        report.AppendLine(new string('-', 60));
        report.AppendLine(string.Format("checks: {0}  failed: {1}", _results.Count, failed));

        string path = SessionState.GetString(Challenge4Probe.ReportKey, "challenge4-probe.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("PROBE-REPORT-BEGIN\n" + report + "PROBE-REPORT-END");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }

    // -------------------------------------------------------------- helpers ---

    /// <summary>Prints what the probe is doing, so a stuck run can be traced.</summary>
    private void Heartbeat(float elapsed)
    {
        if (elapsed - _lastHeartbeat < 1f)
        {
            return;
        }

        _lastHeartbeat = elapsed;

        var manager = GetSpawnManager();
        var player = GameObject.Find("Player");
        var controller = player != null ? player.GetComponent<PlayerControllerX>() : null;

        var enemy = FindEnemy();
        var enemyBody = enemy != null ? enemy.GetComponent<Rigidbody>() : null;
        var enemyScript = enemy != null ? enemy.GetComponent<EnemyX>() : null;

        Debug.Log(string.Format(
            "PROBE-HEARTBEAT step={0} elapsed={1:F1} enemies={2} powerups={3} wave={4} powerupActive={5} playerY={6} " +
            "enemyPos={7} enemyVel={8} enemySpeed={9} enemySleeping={10} goalDist={11}",
            _step,
            elapsed,
            GameObject.FindGameObjectsWithTag("Enemy").Length,
            GameObject.FindGameObjectsWithTag("Powerup").Length,
            manager != null ? manager.waveCount : -1,
            controller != null && controller.hasPowerup,
            player != null ? player.transform.position.y.ToString("F2", CultureInfo.InvariantCulture) : "-",
            enemy != null ? enemy.transform.position.ToString("F2") : "-",
            enemyBody != null ? enemyBody.velocity.ToString("F2") : "-",
            enemyScript != null ? enemyScript.speed.ToString("F2") : "-",
            enemyBody != null && enemyBody.IsSleeping(),
            enemy != null && GameObject.Find("Player Goal") != null
                ? HorizontalDistance(enemy.transform.position, GameObject.Find("Player Goal").transform.position).ToString("F2")
                : "-"));
    }

    private static SpawnManagerX GetSpawnManager()
    {
        var spawner = GameObject.Find("Spawn Manager");
        return spawner != null ? spawner.GetComponent<SpawnManagerX>() : null;
    }

    private static GameObject FindEnemy()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        return enemies.Length > 0 ? enemies[0] : null;
    }

    private static GameObject FindEnemyById(int instanceId)
    {
        foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (enemy.GetInstanceID() == instanceId)
            {
                return enemy;
            }
        }
        return null;
    }

    private static GameObject FindPowerup()
    {
        var powerups = GameObject.FindGameObjectsWithTag("Powerup");
        return powerups.Length > 0 ? powerups[0] : null;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private int GetWaveEnemyCount(int wave)
    {
        return _enemiesPerWave.ContainsKey(wave) ? _enemiesPerWave[wave] : 0;
    }

    private float GetWaveSpeed(int wave)
    {
        return _speedPerWave.ContainsKey(wave) ? _speedPerWave[wave] : 0f;
    }
}
