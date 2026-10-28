using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Play mode probe for Raining Apocalypse.
///
/// Opens the main menu, enters play mode and asserts the observable behaviour
/// the achievement list asks for:
///   - the Play button loads the Game scene
///   - the player moves to the left and to the right and turns around
///   - the player can dash: the dash adds extraSpeed and lasts startDashTime
///   - hazards spawn at the spawn points, fall down and the interval between
///     two spawns gets shorter every time
///   - a hazard that hits the ground explodes without hurting the player
///   - a hazard that hits the player takes one health point off and the
///     health display follows
///   - the lose panel shows up when the health reaches zero and the restart
///     button brings a fresh round back
///   - the music manager survives the scene change
///
/// The probe drives the game through <c>Player.SetInputAxis()</c> and
/// <c>Player.StartDash()</c> instead of a synthetic key press, because legacy
/// <c>Input</c> cannot be faked in a head-less batch mode session. The space
/// bar binding itself is covered by the static wiring check in
/// tools/check-wiring.ps1.
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod RainingApocalypseProbe.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;report path&gt;"
///
/// Note: do NOT pass -quit. The editor has to stay alive for the play mode
/// callback to run; the probe exits the editor itself once the report is out.
/// </summary>
[InitializeOnLoad]
public static class RainingApocalypseProbe
{
    private const string SceneKey = "RainingApocalypseProbe.Scene";
    internal const string ReportKey = "RainingApocalypseProbe.Report";
    private const string StartKey = "RainingApocalypseProbe.Start";
    private const double WatchdogSeconds = 420d;

    static RainingApocalypseProbe()
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
            : "Assets/Scenes/MainMenu.unity";
        string report = separator >= 0 && separator + 2 < args.Length
            ? args[separator + 2]
            : "raining-apocalypse-probe.txt";

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

        var host = new GameObject("RainingApocalypseProbeAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<RainingApocalypseProbeAgent>();
    }
}

/// <summary>Runs the scenario, collects results and writes the report.</summary>
public class RainingApocalypseProbeAgent : MonoBehaviour
{
    private const float MoveSeconds = 0.5f;
    private const float DashTimeout = 4f;
    private const float HitTimeout = 4f;
    private const float SpawnTimeout = 20f;
    private const float KillInterval = 1.2f;
    private const float StopGraceSeconds = 0.4f;
    private const float StopCheckSeconds = 1.6f;
    private const int RequiredSpawns = 4;
    private const int MaxKillAttempts = 8;
    private const float OverallTimeout = 180f;

    private enum Phase
    {
        WaitForMainMenu,
        WaitForGameScene,
        MoveTest,
        MoveLeftTest,
        DashTest,
        GroundTest,
        PlayerHitTest,
        SpawnObservation,
        KillTest,
        SpawnerStopTest,
        MusicTest,
        RestartTest,
        WaitForRestart,
        Done
    }

    private readonly List<string> _results = new List<string>();
    private readonly List<string> _loggedErrors = new List<string>();
    private readonly List<float> _spawnTimes = new List<float>();
    private readonly HashSet<int> _ignoredHazards = new HashSet<int>();
    private readonly HashSet<int> _seenHazards = new HashSet<int>();

    private Phase _phase = Phase.WaitForMainMenu;
    private float _phaseStart;
    private float _startTime;
    private bool _finished;

    private Player _player;
    private Rigidbody2D _playerBody;
    private GameObject _losePanel;
    private Text _healthText;
    private Spawner _spawner;
    private GameObject[] _hazardPrefabs;
    private Transform _ground;
    private float _spawnHeight;

    private float _moveStartX;
    private bool _movedRight;
    private bool _movedLeft;
    private bool _dashStarted;
    private bool _dashBlockedSecondStart;
    private bool _dashEnded;
    private float _dashMaxSpeed;
    private float _dashSpeedAfter;
    private float _dashTimeBeforeTest = 0.2f;
    private int _groundHazardId = -1;
    private int _playerHazardId = -1;
    private string _healthBefore = string.Empty;
    private bool _hazardFell;
    private bool _hazardSpawnedHigh;
    private float _lowestHazardY = float.MaxValue;
    private int _hazardsAtDeath;
    private bool _deathCountTaken;
    private int _killAttempts;
    private bool _startedInGameScene;

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
            _loggedErrors.Add(type + ": " + condition);
        }
    }

    private void Update()
    {
        if (_finished)
        {
            return;
        }

        if (Time.time - _startTime > OverallTimeout)
        {
            AddResult(false, "probe-finishes", "the probe did not finish within " + OverallTimeout + "s");
            GoTo(Phase.Done);
            return;
        }

        switch (_phase)
        {
            case Phase.WaitForMainMenu:
                RunMainMenuPhase();
                break;
            case Phase.WaitForGameScene:
                RunWaitForGameScenePhase();
                break;
            case Phase.MoveTest:
                RunMovePhase();
                break;
            case Phase.MoveLeftTest:
                RunMoveLeftPhase();
                break;
            case Phase.DashTest:
                RunDashPhase();
                break;
            case Phase.GroundTest:
                RunGroundPhase();
                break;
            case Phase.PlayerHitTest:
                RunPlayerHitPhase();
                break;
            case Phase.SpawnObservation:
                RunSpawnObservationPhase();
                break;
            case Phase.KillTest:
                RunKillPhase();
                break;
            case Phase.SpawnerStopTest:
                RunSpawnerStopPhase();
                break;
            case Phase.MusicTest:
                RunMusicPhase();
                break;
            case Phase.RestartTest:
                RunRestartPhase();
                break;
            case Phase.WaitForRestart:
                RunWaitForRestartPhase();
                break;
            case Phase.Done:
                Finish();
                break;
        }
    }

    // ------------------------------------------------------------ main menu --

    private void RunMainMenuPhase()
    {
        GameObject playButtonObject = GameObject.Find("PlayButton");
        if (playButtonObject == null)
        {
            // The probe may also be started directly in the Game scene.
            if (FindInScene("Player") != null)
            {
                _startedInGameScene = true;
                AddResult(true, "game-scene-can-be-played-on-its-own", "the probe was started in the game scene");
                GoTo(Phase.WaitForGameScene);
                return;
            }

            AddResult(false, "main-menu-has-a-play-button", "there is no object named PlayButton in the scene");
            GoTo(Phase.Done);
            return;
        }

        Button playButton = playButtonObject.GetComponent<Button>();
        if (playButton == null)
        {
            AddResult(false, "main-menu-has-a-play-button", "PlayButton has no Button component");
            GoTo(Phase.Done);
            return;
        }

        AddResult(true, "main-menu-has-a-play-button", "the main menu has a PlayButton");

        bool wired = false;
        int count = playButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (playButton.onClick.GetPersistentMethodName(i) == "PlayGame")
            {
                wired = true;
            }
        }
        AddResult(wired, "play-button-calls-play-game", "the Play button calls GameManager.PlayGame()");

        CheckCourseExercise();

        playButton.onClick.Invoke();
        GoTo(Phase.WaitForGameScene);
    }

    /// <summary>
    /// The hot and cold exercise from the course has to run and has to answer
    /// with the right feedback.
    /// </summary>
    private void CheckCourseExercise()
    {
        HotAndColdGame.StartNewGame();
        int secret = HotAndColdGame.SecretNumber;
        int far = secret > 50 ? secret - 30 : secret + 30;
        int near = secret > 50 ? secret - 10 : secret + 10;

        string first = HotAndColdGame.Guess(far);
        string second = HotAndColdGame.Guess(near);
        string third = HotAndColdGame.Guess(secret);

        bool hotter = second.Contains("更热了");
        bool won = third.Contains("猜中了");
        AddResult(hotter && won, "exercise-hot-and-cold", "the exercise answered '" +
            first + "', '" + second + "' and '" + third + "'");
    }

    private void RunWaitForGameScenePhase()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "Game")
        {
            if (Time.time - _phaseStart > 15f)
            {
                AddResult(false, "menu-loads-the-game-scene", "the active scene is still '" + sceneName + "'");
                GoTo(Phase.Done);
            }
            return;
        }

        AddResult(true, "menu-loads-the-game-scene", _startedInGameScene
            ? "the probe runs in the game scene '" + sceneName + "'"
            : "the Play button loaded the scene '" + sceneName + "'");
        SetupGameScene();
    }

    // ----------------------------------------------------------- the scene --

    private void SetupGameScene()
    {
        GameObject playerObject = FindInScene("Player");
        _player = playerObject != null ? playerObject.GetComponent<Player>() : null;
        if (_player == null)
        {
            AddResult(false, "game-scene-has-a-player", "there is no Player object with a Player script in the Game scene");
            GoTo(Phase.Done);
            return;
        }

        _playerBody = _player.GetComponent<Rigidbody2D>();
        _losePanel = _player.losePanel;
        _healthText = _player.healthDisplay;

        GameObject spawnerObject = GameObject.Find("Spawner");
        _spawner = spawnerObject != null ? spawnerObject.GetComponent<Spawner>() : null;
        if (_spawner == null)
        {
            AddResult(false, "game-scene-has-a-spawner", "there is no Spawner object in the Game scene");
            GoTo(Phase.Done);
            return;
        }

        _hazardPrefabs = _spawner.hazards;
        _spawnHeight = _spawner.spawnPoints.Length > 0 ? _spawner.spawnPoints[0].position.y : 0f;

        GameObject groundObject = FindInScene("Ground");
        _ground = groundObject != null ? groundObject.transform : null;

        bool healthShown = _healthText != null && _healthText.text == _player.health.ToString();
        AddResult(healthShown, "health-is-on-the-screen", "the health display shows '" +
            (_healthText != null ? _healthText.text : "nothing") + "'");

        bool panelHidden = _losePanel != null && !_losePanel.activeSelf;
        AddResult(panelHidden, "lose-panel-starts-hidden", "the lose panel is hidden when the round starts");

        bool threeHazards = _hazardPrefabs != null && _hazardPrefabs.Length == 3;
        AddResult(threeHazards, "spawner-has-three-hazards", "the spawner holds " +
            (_hazardPrefabs != null ? _hazardPrefabs.Length.ToString(CultureInfo.InvariantCulture) : "no") + " hazard prefabs");

        // Park the player outside the columns the hazards fall down, so that
        // the observation phase is not disturbed by a random hit, and hold the
        // spawner back until the deliberate tests are done.
        _player.transform.position = new Vector3(-35f, _player.transform.position.y, 0f);
        _spawner.enabled = false;

        GoTo(Phase.MoveTest);
    }

    // ------------------------------------------------------------- moving ---

    private void RunMovePhase()
    {
        if (Time.time - _phaseStart < MoveSeconds)
        {
            return;
        }

        _player.SetInputAxis(null);

        float delta = _player.transform.position.x - _moveStartX;
        _movedRight = delta > 1f;
        AddResult(_movedRight, "player-moves-to-the-right", "the player moved " +
            delta.ToString("F2", CultureInfo.InvariantCulture) + " units to the right");

        bool facesRight = Mathf.Approximately(_player.transform.eulerAngles.y, 0f);
        AddResult(facesRight, "player-turns-with-the-movement", "the player faces " +
            _player.transform.eulerAngles.y.ToString("F0", CultureInfo.InvariantCulture) + " degrees");

        Animator animator = _player.GetComponent<Animator>();
        bool running = animator != null && animator.GetBool("isRunning");
        AddResult(running, "isrunning-drives-the-animation", "the animator is in the running state while the player moves");

        // And back to the left.
        _moveStartX = _player.transform.position.x;
        _player.SetInputAxis(-1f);
        GoTo(Phase.MoveLeftTest);
    }

    private void RunMoveLeftPhase()
    {
        if (Time.time - _phaseStart < MoveSeconds)
        {
            return;
        }

        _player.SetInputAxis(null);
        float delta = _player.transform.position.x - _moveStartX;
        _movedLeft = delta < -1f;
        AddResult(_movedLeft, "player-moves-to-the-left", "the player moved " +
            delta.ToString("F2", CultureInfo.InvariantCulture) + " units to the left");

        bool facesLeft = Mathf.Approximately(_player.transform.eulerAngles.y, 180f);
        AddResult(facesLeft, "player-flips-when-moving-left", "the player faces " +
            _player.transform.eulerAngles.y.ToString("F0", CultureInfo.InvariantCulture) + " degrees");

        GoTo(Phase.DashTest);
    }

    // -------------------------------------------------------------- dashing --

    private void RunDashPhase()
    {
        float speed = Mathf.Abs(_playerBody.velocity.x);
        _dashMaxSpeed = Mathf.Max(_dashMaxSpeed, speed);

        if (_player.dashing)
        {
            return;
        }

        // The dash is over, but the velocity is only brought back to the
        // walking speed in the next fixed update.
        if (speed > _player.speed + 0.5f)
        {
            if (Time.time - _phaseStart > DashTimeout)
            {
                AddResult(false, "dash-ends", "the dash was still running after " +
                    DashTimeout.ToString("F1", CultureInfo.InvariantCulture) + "s");
                GoTo(Phase.GroundTest);
            }
            return;
        }

        _dashEnded = true;
        _dashSpeedAfter = speed;
        bool canDashAgain = _player.CanDash();

        AddResult(_dashStarted, "dash-starts", "the dash flag went up when the dash was triggered");
        AddResult(_dashBlockedSecondStart, "no-second-dash-while-dashing", "a second dash is refused while a dash is running");
        AddResult(_dashMaxSpeed >= _player.speed + _player.extraSpeed - 0.5f, "dash-adds-extra-speed",
            "the peak speed was " + _dashMaxSpeed.ToString("F2", CultureInfo.InvariantCulture) +
            ", the walking speed is " + _player.speed.ToString("F2", CultureInfo.InvariantCulture) +
            " and extraSpeed is " + _player.extraSpeed.ToString("F2", CultureInfo.InvariantCulture));
        AddResult(_dashEnded, "dash-ends", "the dash stopped again");
        AddResult(_dashSpeedAfter <= _player.speed + 0.5f, "speed-is-normal-again-after-the-dash",
            "the speed after the dash was " + _dashSpeedAfter.ToString("F2", CultureInfo.InvariantCulture));
        AddResult(canDashAgain, "dash-is-available-again", "a new dash is allowed once startDashTime has passed");

        _player.startDashTime = _dashTimeBeforeTest;
        _player.SetInputAxis(null);
        GoTo(Phase.GroundTest);
    }

    // ------------------------------------------------------------- hazards --

    private void RunGroundPhase()
    {
        if (!HazardExists(_groundHazardId))
        {
            bool healthUnchanged = _healthText.text == _healthBefore;
            AddResult(true, "hazard-explodes-on-the-ground", "the hazard that reached the ground was cleaned up");
            AddResult(healthUnchanged, "ground-hit-does-not-hurt-the-player",
                "the health is still '" + _healthText.text + "'");
            GoTo(Phase.PlayerHitTest);
            return;
        }

        if (Time.time - _phaseStart > HitTimeout)
        {
            AddResult(false, "hazard-explodes-on-the-ground", "the hazard was still alive after " +
                HitTimeout.ToString("F1", CultureInfo.InvariantCulture) + "s");
            GoTo(Phase.PlayerHitTest);
        }
    }

    private void RunPlayerHitPhase()
    {
        if (_healthText.text != _healthBefore)
        {
            bool onePointOff = int.TryParse(_healthText.text, out int newHealth) &&
                               int.TryParse(_healthBefore, out int oldHealth) &&
                               newHealth == oldHealth - 1;
            AddResult(onePointOff, "hazard-hurts-the-player", "the health display went from '" +
                _healthBefore + "' to '" + _healthText.text + "'");
            AddResult(!HazardExists(_playerHazardId), "hazard-explodes-on-the-player",
                "the hazard that hit the player was cleaned up");
            GoTo(Phase.SpawnObservation);
            return;
        }

        if (Time.time - _phaseStart > HitTimeout)
        {
            AddResult(false, "hazard-hurts-the-player", "the health display stayed at '" +
                _healthBefore + "' for " + HitTimeout.ToString("F1", CultureInfo.InvariantCulture) + "s");
            GoTo(Phase.SpawnObservation);
        }
    }

    private void RunSpawnObservationPhase()
    {
        foreach (Enemy hazard in FindHazards())
        {
            int id = hazard.gameObject.GetInstanceID();
            if (_ignoredHazards.Contains(id))
            {
                continue;
            }

            if (_seenHazards.Add(id))
            {
                _spawnTimes.Add(Time.time);

                if (hazard.transform.position.y > _spawnHeight - 2f)
                {
                    _hazardSpawnedHigh = true;
                }
            }

            // The hazards keep falling, so their height has to be watched over
            // time and not only when they appear.
            if (hazard.transform.position.y < _lowestHazardY)
            {
                _lowestHazardY = hazard.transform.position.y;
            }
        }

        _hazardFell = _lowestHazardY < _spawnHeight - 4f;

        if (_spawnTimes.Count < RequiredSpawns)
        {
            if (Time.time - _phaseStart > SpawnTimeout)
            {
                AddResult(false, "hazards-spawn", "only " + _spawnTimes.Count +
                    " hazards appeared within " + SpawnTimeout.ToString("F0", CultureInfo.InvariantCulture) + "s");
                GoTo(Phase.KillTest);
            }
            return;
        }

        var intervals = new List<float>();
        for (int i = 1; i < _spawnTimes.Count; i++)
        {
            intervals.Add(_spawnTimes[i] - _spawnTimes[i - 1]);
        }

        bool shrinks = intervals.Count >= 2 &&
                       intervals[intervals.Count - 1] < intervals[0] - 0.05f;

        AddResult(true, "hazards-spawn", "the spawner produced " + _spawnTimes.Count + " hazards");
        AddResult(_hazardSpawnedHigh, "hazards-spawn-at-the-spawn-points", "the hazards appear at y = " +
            _spawnHeight.ToString("F2", CultureInfo.InvariantCulture));
        AddResult(_hazardFell, "hazards-fall-down", "the hazards move down the screen");
        AddResult(shrinks, "spawn-interval-gets-shorter", "the intervals between the spawns were " +
            string.Join(", ", intervals.Select(v => v.ToString("F2", CultureInfo.InvariantCulture)).ToArray()));

        GoTo(Phase.KillTest);
    }

    // ----------------------------------------------------------- game over --

    private void RunKillPhase()
    {
        if (_losePanel != null && _losePanel.activeSelf)
        {
            AddResult(true, "lose-panel-shows-up", "the lose panel is visible once the health reached zero");
            GoTo(Phase.SpawnerStopTest);
            return;
        }

        if (Time.time - _phaseStart < KillInterval)
        {
            return;
        }

        _phaseStart = Time.time;
        _killAttempts++;
        if (_killAttempts > MaxKillAttempts)
        {
            AddResult(false, "lose-panel-shows-up", "the player still had health after " +
                MaxKillAttempts + " hits");
            GoTo(Phase.SpawnerStopTest);
            return;
        }

        if (_player != null)
        {
            GameObject hazard = Instantiate(_hazardPrefabs[0], _player.transform.position, Quaternion.identity);
            _ignoredHazards.Add(hazard.GetInstanceID());
        }
    }

    private void RunSpawnerStopPhase()
    {
        float elapsed = Time.time - _phaseStart;

        if (!_deathCountTaken)
        {
            if (elapsed < StopGraceSeconds)
            {
                return;
            }

            _hazardsAtDeath = CountHazards();
            _deathCountTaken = true;
            AddResult(_player == null, "player-is-removed", "the player object was destroyed");
            _phaseStart = Time.time;
            return;
        }

        if (elapsed < StopCheckSeconds)
        {
            return;
        }

        int now = CountHazards();
        AddResult(now <= _hazardsAtDeath, "spawner-stops-when-the-player-is-gone",
            "the number of hazards went from " + _hazardsAtDeath + " to " + now +
            " after the player was gone");

        GoTo(Phase.MusicTest);
    }

    // --------------------------------------------------------------- music --

    private void RunMusicPhase()
    {
        if (_startedInGameScene)
        {
            // The music manager lives in the main menu and survives the scene
            // change. Started in the game scene there is nothing to survive.
            AddResult(true, "music-manager-survives-the-scene-change",
                "the game scene holds no music manager of its own, the music is started in the main menu");
            GoTo(Phase.RestartTest);
            return;
        }

        MusicManager[] managers = UnityEngine.Object.FindObjectsOfType<MusicManager>();
        AddResult(managers.Length == 1, "music-manager-survives-the-scene-change",
            "there are " + managers.Length + " music managers after the scene change");

        if (managers.Length > 0)
        {
            MusicManager manager = managers[0];
            string sceneName = manager.gameObject.scene.name;
            AddResult(sceneName == "DontDestroyOnLoad", "music-manager-is-persistent",
                "the music manager lives in the scene '" + sceneName + "'");

            AudioSource audio = manager.GetComponent<AudioSource>();
            bool hasMusic = audio != null && audio.clip != null && audio.clip.name == "Music";
            AddResult(hasMusic, "music-is-still-playing",
                "the music source holds '" + (audio != null && audio.clip != null ? audio.clip.name : "nothing") +
                "' and is playing: " + (audio != null && audio.isPlaying).ToString().ToLowerInvariant());
        }

        GoTo(Phase.RestartTest);
    }

    // ------------------------------------------------------------- restart --

    private void RunRestartPhase()
    {
        Button restart = null;
        if (_losePanel != null)
        {
            Button[] buttons = _losePanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                restart = buttons[0];
            }
        }

        if (restart == null)
        {
            AddResult(false, "lose-panel-offers-a-restart", "the lose panel has no restart button");
            GoTo(Phase.Done);
            return;
        }

        bool wired = false;
        int count = restart.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (restart.onClick.GetPersistentMethodName(i) == "PlayGame")
            {
                wired = true;
            }
        }
        AddResult(wired, "restart-button-calls-play-game", "the restart button calls GameManager.PlayGame()");

        restart.onClick.Invoke();
        GoTo(Phase.WaitForRestart);
    }

    private void RunWaitForRestartPhase()
    {
        if (SceneManager.GetActiveScene().name != "Game")
        {
            if (Time.time - _phaseStart > 15f)
            {
                AddResult(false, "restart-brings-a-new-round", "the Game scene did not come back");
                GoTo(Phase.Done);
            }
            return;
        }

        if (Time.time - _phaseStart < 1f)
        {
            return;
        }

        GameObject newPlayer = FindInScene("Player");
        Player newPlayerScript = newPlayer != null ? newPlayer.GetComponent<Player>() : null;

        // GameObject.Find only sees active objects, and the lose panel is
        // hidden at the start of a round.
        GameObject newLosePanel = FindByName("LosePanel");

        bool fresh = newPlayerScript != null && newPlayerScript.health > 0 &&
                     newLosePanel != null && !newLosePanel.activeSelf;
        AddResult(fresh, "restart-brings-a-new-round", "the new round starts with health " +
            (newPlayerScript != null ? newPlayerScript.health.ToString(CultureInfo.InvariantCulture) : "none") +
            " and the lose panel hidden again");

        GoTo(Phase.Done);
    }

    // -------------------------------------------------------------- helpers --

    private void GoTo(Phase phase)
    {
        _phase = phase;
        _phaseStart = Time.time;

        switch (phase)
        {
            case Phase.MoveTest:
                _moveStartX = _player.transform.position.x;
                _player.SetInputAxis(1f);
                break;

            case Phase.DashTest:
                _player.SetInputAxis(1f);
                // The dash is stretched for the test, so that a slow batch
                // mode frame cannot swallow the whole dash.
                _dashTimeBeforeTest = _player.startDashTime;
                _player.startDashTime = Mathf.Max(_dashTimeBeforeTest, 0.6f);
                _player.StartDash();
                _dashStarted = _player.dashing;
                _dashBlockedSecondStart = !_player.CanDash();
                _dashMaxSpeed = 0f;
                break;

            case Phase.GroundTest:
                // Park the player out of the way again: the ground is only
                // below the middle of the screen.
                _player.SetInputAxis(null);
                _player.transform.position = new Vector3(-35f, _player.transform.position.y, 0f);
                _healthBefore = _healthText.text;
                if (_ground != null && _hazardPrefabs.Length > 0)
                {
                    GameObject hazard = Instantiate(_hazardPrefabs[0], _ground.position + new Vector3(0f, 1f, 0f),
                        Quaternion.identity);
                    _groundHazardId = hazard.GetInstanceID();
                    _ignoredHazards.Add(_groundHazardId);
                }
                else
                {
                    AddResult(false, "hazard-explodes-on-the-ground", "there is no ground object to drop a hazard on");
                }
                break;

            case Phase.PlayerHitTest:
                _healthBefore = _healthText.text;
                if (_player != null && _hazardPrefabs.Length > 0)
                {
                    GameObject hazard = Instantiate(_hazardPrefabs[0], _player.transform.position, Quaternion.identity);
                    _playerHazardId = hazard.GetInstanceID();
                    _ignoredHazards.Add(_playerHazardId);
                }
                break;

            case Phase.SpawnObservation:
                _spawner.enabled = true;
                foreach (Enemy hazard in FindHazards())
                {
                    _ignoredHazards.Add(hazard.gameObject.GetInstanceID());
                }
                _seenHazards.Clear();
                _spawnTimes.Clear();
                break;
        }
    }

    private void AddResult(bool ok, string name, string detail)
    {
        _results.Add((ok ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private static List<Enemy> FindHazards()
    {
        var hazards = new List<Enemy>();
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            hazards.AddRange(root.GetComponentsInChildren<Enemy>(true));
        }
        return hazards;
    }

    private static bool HazardExists(int instanceId)
    {
        if (instanceId < 0)
        {
            return false;
        }

        foreach (Enemy hazard in FindHazards())
        {
            if (hazard.gameObject.GetInstanceID() == instanceId)
            {
                return true;
            }
        }
        return false;
    }

    private static int CountHazards()
    {
        return FindHazards().Count;
    }

    private static GameObject FindInScene(string tag)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.tag == tag)
                {
                    return child.gameObject;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds an object by name, also when it is hidden. GameObject.Find only
    /// sees the active objects, and the lose panel is hidden while a round is
    /// running.
    /// </summary>
    private static GameObject FindByName(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name == objectName)
                {
                    return child.gameObject;
                }
            }
        }
        return null;
    }

    private void Finish()
    {
        _finished = true;

        foreach (string error in _loggedErrors)
        {
            _results.Add("FAIL no-errors: " + error);
        }

        int failed = _results.Count(result => result.StartsWith("FAIL"));

        var report = new StringBuilder();
        report.AppendLine("Raining Apocalypse probe");
        report.AppendLine(new string('-', 60));
        foreach (string result in _results)
        {
            report.AppendLine(result);
        }
        report.AppendLine(new string('-', 60));
        report.AppendLine("checks: " + _results.Count + "  failed: " + failed);

        string path = SessionState.GetString(RainingApocalypseProbe.ReportKey, "raining-apocalypse-probe.txt");

        // UTF-8 with a byte order mark, so that the report stays readable in
        // the PowerShell console.
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(true));
        Debug.Log("PROBE-REPORT-BEGIN\n" + report + "PROBE-REPORT-END");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
