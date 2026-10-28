using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Play mode probe for Challenge 5 - Whack-a-Food.
///
/// Opens the challenge scene, enters play mode and asserts the observable
/// behaviour the assignment asks for:
///   - the difficulty buttons carry their label centred horizontally and
///     vertically
///   - choosing a difficulty changes how fast the food pops up
///   - clicking the food destroys it and the score in the top left updates
///   - the food survives a mouse that only passes over it
///   - the game over screen shows a restart button
///   - the round counts down from 60 seconds and ends at zero
///
/// The probe drives the game through the real button onClick listeners and the
/// real mouse callback (OnMouseDown) instead of calling the game manager
/// directly, so the wiring between the buttons, the game manager and the food
/// is part of what is tested. Legacy <c>Input</c> cannot be faked in a
/// head-less batch mode session, so a physical mouse click is out of reach;
/// the click callback itself is invoked instead.
///
/// Run head-less:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod Challenge5Probe.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;report path&gt;"
///
/// Note: do NOT pass -quit. The editor has to stay alive for the play mode
/// callback to run; the probe exits the editor itself once the report is out.
/// </summary>
[InitializeOnLoad]
public static class Challenge5Probe
{
    private const string SceneKey = "Challenge5Probe.Scene";
    internal const string ReportKey = "Challenge5Probe.Report";
    private const string StartKey = "Challenge5Probe.Start";
    private const double WatchdogSeconds = 420d;

    static Challenge5Probe()
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
            : "Assets/Challenge 5/Challenge 5.unity";
        string report = separator >= 0 && separator + 2 < args.Length
            ? args[separator + 2]
            : "challenge5-probe.txt";

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

        var host = new GameObject("Challenge5ProbeAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Challenge5ProbeAgent>();
    }
}

/// <summary>Runs the scenario, collects results and writes the report.</summary>
public class Challenge5ProbeAgent : MonoBehaviour
{
    private const float WiringDelay = 0.6f;
    private const float MeasureSeconds = 4.5f;
    private const float HoverWatchSeconds = 0.35f;
    private const float CountdownTimeout = 8f;

    private const string RemainingTimeField = "timeRemaining";

    private static readonly MethodInfo MouseDownCallback =
        typeof(TargetX).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly MethodInfo MouseEnterCallback =
        typeof(TargetX).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly FieldInfo RemainingTime =
        typeof(GameManagerX).GetField(RemainingTimeField, BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly List<string> _results = new List<string>();
    private readonly HashSet<int> _seenTargetIds = new HashSet<int>();
    private readonly HashSet<int> _clickedTargetIds = new HashSet<int>();
    private readonly List<TargetX> _managedTargets = new List<TargetX>();

    private GameManagerX _gameManager;
    private Button _easyButton;
    private Button _mediumButton;
    private Button _hardButton;

    private bool _measuring;
    private int _spawnCount;
    private bool _finished;

    // the first food the probe clicks, kept for the report
    private bool _clickSeen;
    private GameObject _clickedFood;
    private int _clickedFoodPoints;
    private string _scoreBeforeClick;
    private string _scoreAfterClick;

    private void Start()
    {
        Application.logMessageReceived += OnLog;
        StartCoroutine(RunScenario());
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            _results.Add("FAIL no-runtime-errors: logged " + type + ": " + condition);
        }
    }

    private void Update()
    {
        if (_finished)
        {
            return;
        }

        CountSpawns();
        AutoClickTargets();
    }

    // ------------------------------------------------------------- scenario ---

    private IEnumerator RunScenario()
    {
        yield return new WaitForSeconds(WiringDelay);
        RunWiringChecks();

        // --- the food survives a mouse that only passes over it ------------
        yield return RunHoverTest();

        // --- Easy: the food should pop up slowly --------------------------
        _measuring = true;
        _spawnCount = 0;
        ClickButton(_easyButton);
        yield return null;

        Check("game-starts", _gameManager.isGameActive, "the game is running after Easy was clicked");
        Check("title-screen-hidden", !_gameManager.titleScreen.activeSelf,
            "the title screen is hidden while the game runs");
        Check("score-reset", ScoreText() == "Score: 0",
            "the score reads 'Score: 0' at the start, it reads '" + ScoreText() + "'");
        string countdownAtStart = TimeText();
        Check("countdown-starts-at-60", countdownAtStart == "Time: 60",
            "the countdown starts at 'Time: 60', it reads '" + countdownAtStart + "'");

        yield return new WaitForSeconds(MeasureSeconds);
        int easySpawns = _spawnCount;
        _measuring = false;

        // --- Hard: the food should pop up faster ---------------------------
        _measuring = true;
        _spawnCount = 0;
        ClickButton(_hardButton);
        yield return null;

        Check("difficulty-starts-game", _gameManager.isGameActive,
            "the game is running after Hard was clicked");

        yield return new WaitForSeconds(MeasureSeconds);
        int hardSpawns = _spawnCount;
        _measuring = false;

        Check("difficulty-changes-spawn-rate", hardSpawns > easySpawns,
            "Hard spawned " + hardSpawns + " targets in " + MeasureSeconds + "s, Easy spawned " + easySpawns);

        // --- clicking the food updates the score ---------------------------
        Check("food-reacts-to-a-click", _clickSeen, "the food was clicked while the game was running");
        if (_clickSeen)
        {
            int before = ParseScore(_scoreBeforeClick);
            int after = ParseScore(_scoreAfterClick);
            Check("score-text-format", _scoreAfterClick == "Score: " + after && after == before + _clickedFoodPoints,
                "the score text reads '" + _scoreAfterClick + "' after clicking a food worth " + _clickedFoodPoints +
                " points, it read '" + _scoreBeforeClick + "' before");
            Check("clicked-food-is-destroyed", _clickedFood == null, "the clicked food is gone");
        }

        // --- the round counts down and ends at zero ------------------------
        yield return RunCountdownTest();

        Finish();
    }

    /// <summary>
    /// The food may only be destroyed by a click. A mouse that merely passes
    /// over it must leave it alone. This runs before the game starts, so no
    /// other food can change the score while the food is watched.
    /// </summary>
    private IEnumerator RunHoverTest()
    {
        GameObject food = SpawnFood("Steak");
        if (food == null)
        {
            Check("food-spawned-for-hover", false, "the Steak prefab could not be spawned");
            yield break;
        }

        var target = food.GetComponent<TargetX>();
        if (target != null)
        {
            _managedTargets.Add(target);
        }

        string scoreBefore = ScoreText();

        yield return new WaitForSeconds(HoverWatchSeconds);

        Check("hovered-food-survives", food != null, "a food the mouse only passed over is still there");
        Check("hover-does-not-score", ScoreText() == scoreBefore,
            "the score is unchanged after the mouse passed over the food, it reads '" + ScoreText() + "'");
        Check("no-mouse-enter-handler", MouseEnterCallback == null,
            "the food has no mouse enter handler, so passing over it cannot destroy it");

        UnityEngine.Object.Destroy(food);
        _managedTargets.Clear();
        yield return null;
    }

    /// <summary>
    /// The round counts down in whole seconds and ends when the time runs out.
    /// The last two seconds are shortened so the probe does not have to sit
    /// through a whole minute; the countdown itself is driven by the game.
    /// </summary>
    private IEnumerator RunCountdownTest()
    {
        string before = TimeText();
        int beforeValue = ParseTime(before);
        Check("countdown-is-visible", beforeValue != int.MinValue,
            "the countdown reads '" + before + "'");

        Check("countdown-counts-down", beforeValue != int.MinValue && beforeValue < 60,
            "the countdown is below 60, it reads '" + before + "'");

        bool restartHidden = !_gameManager.restartButton.gameObject.activeSelf;
        bool gameOverHidden = !_gameManager.gameOverText.gameObject.activeSelf;
        Check("restart-button-hidden-while-playing", restartHidden,
            "the restart button is hidden while the game runs");
        Check("game-over-text-hidden-while-playing", gameOverHidden,
            "the game over text is hidden while the game runs");

        if (RemainingTime == null)
        {
            Check("countdown-reaches-zero", false,
                "the remaining time of the game manager could not be found");
            yield break;
        }

        RemainingTime.SetValue(_gameManager, 1.2f);

        float deadline = Time.time + CountdownTimeout;
        while (Time.time < deadline && _gameManager.isGameActive)
        {
            yield return null;
        }

        Check("countdown-reaches-zero", ParseTime(TimeText()) == 0,
            "the countdown reads '" + TimeText() + "'");

        yield return null;

        Check("game-over-ends-the-round", !_gameManager.isGameActive,
            "the game is over when the countdown reaches zero");
        Check("restart-button-appears", _gameManager.restartButton.gameObject.activeSelf,
            "the restart button is on screen when the game ends");
        Check("game-over-text-appears", _gameManager.gameOverText.gameObject.activeSelf,
            "the game over text is on screen when the game ends");
    }

    // -------------------------------------------------------- wiring checks ---

    private void RunWiringChecks()
    {
        var manager = GameObject.Find("Game Manager");
        _gameManager = manager != null ? manager.GetComponent<GameManagerX>() : null;
        Check("game-manager-exists", _gameManager != null, "the scene has a Game Manager with a GameManagerX script");
        if (_gameManager == null)
        {
            return;
        }

        Check("score-text-wired", _gameManager.scoreText != null, "the score text is assigned");
        Check("game-over-text-wired", _gameManager.gameOverText != null, "the game over text is assigned");
        Check("time-text-wired", _gameManager.timeText != null, "the countdown text is assigned");
        Check("title-screen-wired", _gameManager.titleScreen != null, "the title screen is assigned");
        Check("restart-button-wired", _gameManager.restartButton != null, "the restart button is assigned");
        Check("food-prefabs-wired", _gameManager.targetPrefabs != null && _gameManager.targetPrefabs.Count == 4 &&
                                     _gameManager.targetPrefabs.All(prefab => prefab != null),
            "the game manager holds the four food prefabs");

        _easyButton = FindButton("Easy Button");
        _mediumButton = FindButton("Medium Button");
        _hardButton = FindButton("Hard Button");

        Check("easy-button-exists", _easyButton != null, "the scene has an Easy Button");
        Check("medium-button-exists", _mediumButton != null, "the scene has a Medium Button");
        Check("hard-button-exists", _hardButton != null, "the scene has a Hard Button");

        Check("easy-difficulty", DifficultyOf("Easy Button") == 1, "the Easy Button carries difficulty 1");
        Check("medium-difficulty", DifficultyOf("Medium Button") == 2, "the Medium Button carries difficulty 2");
        Check("hard-difficulty", DifficultyOf("Hard Button") == 3, "the Hard Button carries difficulty 3");

        CheckButtonLabel("Easy Button");
        CheckButtonLabel("Medium Button");
        CheckButtonLabel("Hard Button");

        var sensor = GameObject.Find("Sensor");
        Check("sensor-tag", sensor != null && sensor.CompareTag("Sensor"), "the sensor carries the Sensor tag");
        Check("skull-tag", SkullHasBadTag(), "the skull prefab carries the Bad tag");
    }

    /// <summary>
    /// The label of a difficulty button has to sit in the middle of the button,
    /// horizontally and vertically.
    /// </summary>
    private void CheckButtonLabel(string buttonName)
    {
        string checkName = "label-centred-" + buttonName.Replace(" ", "-").ToLowerInvariant();
        var buttonObject = GameObject.Find(buttonName);
        if (buttonObject == null)
        {
            Check(checkName, false, "the button '" + buttonName + "' is missing");
            return;
        }

        var label = buttonObject.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            Check(checkName, false, "the button '" + buttonName + "' has no text label");
            return;
        }

        bool aligned = label.alignment == TextAnchor.MiddleCenter;
        RectTransform labelRect = label.rectTransform;
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();

        bool coversButton = labelRect != null && buttonRect != null &&
                            labelRect.anchorMin == Vector2.zero &&
                            labelRect.anchorMax == Vector2.one &&
                            labelRect.sizeDelta == Vector2.zero &&
                            labelRect.pivot == new Vector2(0.5f, 0.5f) &&
                            labelRect.anchoredPosition == Vector2.zero;

        bool rendered = RenderedLabelIsCentred(label, buttonRect, out Vector3 textCentre, out Vector3 buttonCentre);

        Check(checkName, aligned && coversButton && rendered,
            "the label '" + label.text + "' is centred in the button (alignment " + label.alignment +
            ", label covers the button " + coversButton + ", text centre " + textCentre + ", button centre " + buttonCentre + ")");
    }

    /// <summary>
    /// Compares the middle of the generated text with the middle of the button.
    /// The text generator works in the local space of the label, so the
    /// generated corners are moved into world space before they are compared
    /// with the corners of the button.
    /// </summary>
    private static bool RenderedLabelIsCentred(Text label, RectTransform buttonRect,
        out Vector3 textCentre, out Vector3 buttonCentre)
    {
        textCentre = Vector3.zero;
        buttonCentre = Vector3.zero;

        if (buttonRect == null)
        {
            return true;
        }

        TextGenerator generator = label.cachedTextGenerator;
        if (generator.vertexCount == 0)
        {
            return true;
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < generator.vertexCount; i++)
        {
            Vector3 vertex = generator.verts[i].position;
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        Vector3 localCentre = (min + max) * 0.5f;
        textCentre = label.rectTransform.TransformPoint(localCentre);

        var corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        buttonCentre = (corners[0] + corners[2]) * 0.5f;

        // The line box of the text is centred in the button, but the glyphs
        // themselves sit on the baseline inside that line box, so they are a
        // few pixels off the middle. A quarter of the font size is well below
        // the offset a wrong alignment would cause.
        float tolerance = Mathf.Max(2f, label.fontSize * 0.25f);
        return Mathf.Abs(textCentre.x - buttonCentre.x) <= tolerance &&
               Mathf.Abs(textCentre.y - buttonCentre.y) <= tolerance;
    }

    // ------------------------------------------------------------- helpers ---

    private void ClickButton(Button button)
    {
        if (button != null)
        {
            button.onClick.Invoke();
        }
    }

    private static Button FindButton(string name)
    {
        var buttonObject = GameObject.Find(name);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private static int DifficultyOf(string name)
    {
        var buttonObject = GameObject.Find(name);
        var difficulty = buttonObject != null ? buttonObject.GetComponent<DifficultyButtonX>() : null;
        return difficulty != null ? difficulty.difficulty : 0;
    }

    /// <summary>The skull is the one prefab that carries the Bad tag.</summary>
    private bool SkullHasBadTag()
    {
        if (_gameManager == null || _gameManager.targetPrefabs == null)
        {
            return false;
        }

        foreach (var prefab in _gameManager.targetPrefabs)
        {
            if (prefab != null && prefab.name == "Skull")
            {
                return prefab.CompareTag("Bad");
            }
        }

        return false;
    }

    private void CountSpawns()
    {
        if (!_measuring)
        {
            return;
        }

        foreach (var target in FindTargets())
        {
            if (_seenTargetIds.Add(target.GetInstanceID()))
            {
                _spawnCount++;
            }
        }
    }

    /// <summary>
    /// Plays the game: every food that pops up is clicked right away, so the
    /// round does not end while the probe measures the spawn rate. The food
    /// the probe watches itself is left alone.
    /// </summary>
    private void AutoClickTargets()
    {
        if (_gameManager == null || !_gameManager.isGameActive)
        {
            return;
        }

        foreach (var target in FindTargets())
        {
            if (_managedTargets.Contains(target) || target.pointValue < 0)
            {
                continue;
            }

            if (!_clickedTargetIds.Add(target.GetInstanceID()))
            {
                continue;
            }

            string before = ScoreText();
            int points = target.pointValue;
            bool clicked = InvokeClick(target);
            string after = ScoreText();

            if (!_clickSeen && clicked)
            {
                _clickSeen = true;
                _clickedFood = target.gameObject;
                _clickedFoodPoints = points;
                _scoreBeforeClick = before;
                _scoreAfterClick = after;
            }
        }
    }

    private static bool InvokeClick(TargetX target)
    {
        if (target == null || MouseDownCallback == null)
        {
            return false;
        }

        MouseDownCallback.Invoke(target, null);
        return true;
    }

    private static List<TargetX> FindTargets()
    {
        return UnityEngine.Object.FindObjectsOfType<TargetX>().ToList();
    }

    /// <summary>Spawns one of the food prefabs the game manager uses.</summary>
    private GameObject SpawnFood(string prefabName)
    {
        if (_gameManager == null || _gameManager.targetPrefabs == null)
        {
            return null;
        }

        foreach (var prefab in _gameManager.targetPrefabs)
        {
            if (prefab != null && prefab.name == prefabName)
            {
                return Instantiate(prefab);
            }
        }

        return null;
    }

    private string ScoreText()
    {
        return _gameManager != null && _gameManager.scoreText != null ? _gameManager.scoreText.text : string.Empty;
    }

    private string TimeText()
    {
        return _gameManager != null && _gameManager.timeText != null ? _gameManager.timeText.text : string.Empty;
    }

    private static int ParseScore(string text)
    {
        return ParseNumber(text);
    }

    private static int ParseTime(string text)
    {
        return ParseNumber(text);
    }

    private static int ParseNumber(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return int.MinValue;
        }

        int colon = text.IndexOf(':');
        if (colon < 0)
        {
            return int.MinValue;
        }

        int value;
        return int.TryParse(text.Substring(colon + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : int.MinValue;
    }

    private void Check(string name, bool ok, string detail)
    {
        _results.Add((ok ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private void Finish()
    {
        _finished = true;

        int failed = _results.Count(result => result.StartsWith("FAIL"));

        var report = new StringBuilder();
        report.AppendLine("Challenge 5 - Whack-a-Food probe");
        report.AppendLine(new string('-', 60));
        foreach (var result in _results)
        {
            report.AppendLine(result);
        }
        report.AppendLine(new string('-', 60));
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "checks: {0}  failed: {1}", _results.Count, failed));

        string path = SessionState.GetString(Challenge5Probe.ReportKey, "challenge5-probe.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("PROBE-REPORT-BEGIN\n" + report + "PROBE-REPORT-END");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
