using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬을 열고 바로 Play 모드로 들어가는 개발 도구.
/// 메뉴: Tools > Project > Play ConvenienceStore
/// 외부 트리거: Library/AutoPlay/request.txt 에 씬 경로를 써 두고 스크립트를 다시 로드시키면 그 씬을 실행하고
/// 2초 뒤 Game 뷰를 Library/AutoPlay/screenshot.png 로 캡처합니다. Library/ 는 Git에 포함되지 않습니다.
/// </summary>
[InitializeOnLoad]
public static class AutoPlay
{
    private const string RequestPath = "Library/AutoPlay/request.txt";
    private const string ScreenshotPath = "Library/AutoPlay/screenshot.png";
    private const string ShotPendingKey = "AutoPlay.shotPending";
    /// <summary>이 파일이 있으면 첫 캡처 뒤에도 15초마다 Library/AutoPlay/shot_N.png 를 여덟 장 더 찍습니다.</summary>
    private const string SeriesPath = "Library/AutoPlay/series.txt";
    private static int _seriesShot;
    private static float _nextSeriesTime;

    static AutoPlay()
    {
        if (SessionState.GetBool(ShotPendingKey, false))
        {
            EditorApplication.update += ScreenshotWhenReady;
        }

        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // 재생 중이면 먼저 정지하고, 편집 모드로 돌아온 뒤 요청을 처리한다.
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.ExitPlaymode();
            return;
        }
        EditorApplication.delayCall += ProcessRequest;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += ProcessRequest;
    }

    private static void ProcessRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string scenePath = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        Run(scenePath, captureScreenshot: true);
    }

    [MenuItem("Tools/Project/Play ConvenienceStore")]
    public static void PlayConvenienceStore() => Run("Assets/Scenes/ConvenienceStore.unity", captureScreenshot: false);

    private static void Run(string scenePath, bool captureScreenshot)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != scenePath)
        {
            if (active.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        SessionState.SetBool(ShotPendingKey, captureScreenshot);
        EditorApplication.EnterPlaymode();
    }

    private static void ScreenshotWhenReady()
    {
        if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2f) return;
        EditorApplication.update -= ScreenshotWhenReady;
        SessionState.SetBool(ShotPendingKey, false);
        ScreenCapture.CaptureScreenshot(ScreenshotPath);
        Debug.Log($"[AutoPlay] Screenshot requested -> {ScreenshotPath}");
        if (!File.Exists(SeriesPath)) return;
        _seriesShot = 0;
        _nextSeriesTime = Time.timeSinceLevelLoad + 15f;
        EditorApplication.update += SeriesScreenshots;
    }

    private static void SeriesScreenshots()
    {
        if (!EditorApplication.isPlaying || _seriesShot >= 8)
        {
            EditorApplication.update -= SeriesScreenshots;
            return;
        }
        if (Time.timeSinceLevelLoad < _nextSeriesTime) return;
        _nextSeriesTime += 15f;
        _seriesShot++;
        ScreenCapture.CaptureScreenshot($"Library/AutoPlay/shot_{_seriesShot}.png");
    }
}
