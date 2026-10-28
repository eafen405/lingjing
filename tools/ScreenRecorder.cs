using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Records the running game to a folder of PNG frames.
///
/// The desktop this repository is built on locks the foreground window, so the
/// player build cannot be filmed from the outside. Recording from inside Unity
/// sidesteps that: the camera is rendered into a RenderTexture and the pixels
/// are read back, which works head-less.
///
/// Run head-less (the editor needs a graphics device, so do NOT pass -nographics
/// and do NOT pass -quit):
///   Unity.exe -batchmode -projectPath &lt;project&gt; \
///             -executeMethod ScreenRecorder.Run -logFile &lt;log&gt; -- \
///             "&lt;scene&gt;" "&lt;output folder&gt;" &lt;frames&gt; &lt;width&gt; &lt;height&gt; "&lt;driver&gt;"
///
/// The driver argument is "&lt;game object&gt;|&lt;method&gt;|&lt;every frames&gt;", for example
/// "Player|SpawnDog|45". It calls a public method on the game object so the
/// recording shows the game being played. Legacy Input cannot be faked in a
/// head-less session, so the method that the key press would call is called
/// directly instead - that is the same method the play mode probe uses.
///
/// Lives under Assets/Editor so it never reaches a player build.
/// </summary>
[InitializeOnLoad]
public static class ScreenRecorder
{
    private const string SceneKey = "ScreenRecorder.Scene";
    private const string OutputKey = "ScreenRecorder.Output";
    private const string FramesKey = "ScreenRecorder.Frames";
    private const string WidthKey = "ScreenRecorder.Width";
    private const string HeightKey = "ScreenRecorder.Height";
    private const string DriverKey = "ScreenRecorder.Driver";

    static ScreenRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        int separator = Array.IndexOf(args, "--");
        string scene = separator >= 0 && separator + 1 < args.Length ? args[separator + 1] : "Assets/Scenes/MainMenu.unity";
        string output = separator >= 0 && separator + 2 < args.Length ? args[separator + 2] : "recording";
        int frames = separator >= 0 && separator + 3 < args.Length ? int.Parse(args[separator + 3]) : 300;
        int width = separator >= 0 && separator + 4 < args.Length ? int.Parse(args[separator + 4]) : 1280;
        int height = separator >= 0 && separator + 5 < args.Length ? int.Parse(args[separator + 5]) : 720;
        string driver = separator >= 0 && separator + 6 < args.Length ? args[separator + 6] : "";

        SessionState.SetString(SceneKey, scene);
        SessionState.SetString(OutputKey, output);
        SessionState.SetInt(FramesKey, frames);
        SessionState.SetInt(WidthKey, width);
        SessionState.SetInt(HeightKey, height);
        SessionState.SetString(DriverKey, driver);

        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene(scene);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        var host = new GameObject("ScreenRecorderAgent");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<ScreenRecorderAgent>();
    }
}

/// <summary>Renders the camera into a texture and writes one PNG per frame.</summary>
public class ScreenRecorderAgent : MonoBehaviour
{
    private string _output;
    private int _frames;
    private int _width;
    private int _height;
    private int _captured;
    private Camera _camera;
    private RenderTexture _target;
    private Texture2D _readback;

    private string _driverObject = "";
    private string _driverMethod = "";
    private int _driverEvery = 0;
    private int _driverIndex;

    private void Start()
    {
        _output = SessionState.GetString("ScreenRecorder.Output", "recording");
        _frames = SessionState.GetInt("ScreenRecorder.Frames", 300);
        _width = SessionState.GetInt("ScreenRecorder.Width", 1280);
        _height = SessionState.GetInt("ScreenRecorder.Height", 720);

        string driver = SessionState.GetString("ScreenRecorder.Driver", "");
        string[] parts = driver.Split('|');
        if (parts.Length >= 2)
        {
            _driverObject = parts[0];
            _driverMethod = parts[1];
            if (parts.Length > 2)
            {
                int.TryParse(parts[2], out _driverEvery);
            }
        }

        if (!Directory.Exists(_output))
        {
            Directory.CreateDirectory(_output);
        }

        // The play mode probe of the same project would exit the editor while
        // the recording is still running.
        var probe = GameObject.Find("Challenge2ProbeAgent");
        if (probe != null)
        {
            Destroy(probe);
        }

        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.Log("SCREEN-RECORDER no main camera, nothing to record");
            EditorApplication.Exit(1);
            return;
        }

        _target = new RenderTexture(_width, _height, 24);
        _readback = new Texture2D(_width, _height, TextureFormat.RGB24, false);
    }

    /// <summary>Calls the method a key press would call, so the game is played.</summary>
    private void DriveTheGame()
    {
        if (_driverEvery <= 0 || _driverObject.Length == 0 || _driverMethod.Length == 0)
        {
            return;
        }

        if (_captured % _driverEvery != 0)
        {
            return;
        }

        var target = GameObject.Find(_driverObject);
        if (target == null)
        {
            return;
        }

        var components = target.GetComponents<MonoBehaviour>();
        foreach (var component in components)
        {
            if (component == null)
            {
                continue;
            }

            System.Reflection.MethodInfo method = component.GetType().GetMethod(_driverMethod);
            if (method == null || method.GetParameters().Length != 0)
            {
                continue;
            }

            method.Invoke(component, null);
            return;
        }
    }

    private void OnDestroy()
    {
        if (_target != null)
        {
            _target.Release();
            Destroy(_target);
        }
        if (_readback != null)
        {
            Destroy(_readback);
        }
    }

    private void Update()
    {
        if (_captured >= _frames)
        {
            EditorApplication.Exit(0);
            return;
        }

        DriveTheGame();

        var previous = RenderTexture.active;
        var previousTarget = _camera.targetTexture;
        _camera.targetTexture = _target;
        _camera.Render();
        RenderTexture.active = _target;
        _readback.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
        _readback.Apply();
        RenderTexture.active = previous;
        _camera.targetTexture = previousTarget;

        byte[] png = _readback.EncodeToPNG();
        string path = Path.Combine(_output, string.Format("frame_{0:00000}.png", _captured));
        File.WriteAllBytes(path, png);
        _captured++;
    }
}
