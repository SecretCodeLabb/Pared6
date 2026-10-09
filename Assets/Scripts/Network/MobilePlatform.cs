using UnityEngine;

/// <summary>
/// Runtime settings for phones / tablets (Android APK). Nothing changes on desktop.
/// </summary>
public static class MobilePlatform
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (!Application.isMobilePlatform)
            return;

        // Mobile defaults to 30 FPS; the board camera and the roulette look much better at 60.
        Application.targetFrameRate = 60;

        // A LAN game must not drop because the screen turned off while waiting for a turn.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Landscape only (also set in the Android player settings).
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
