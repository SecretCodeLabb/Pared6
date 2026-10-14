using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android (APK) configuration and build.
/// PPOP/Android/Apply Android Settings — player settings for a sideloaded APK (idempotent).
/// PPOP/Android/Build APK               — Builds/Android/Pared.apk
/// PPOP/Android/Build APK (Development) — same with the dev bot / profiler (DevAutoPlayer).
/// Requires the Android Build Support module (with SDK, NDK and OpenJDK) in Unity Hub.
/// </summary>
public static class AndroidBuildSetup
{
    public const string PackageName = "com.ppop.pared";
    public const string ProductName = "Pared";
    public const string OutputPath = "Builds/Android/Pared.apk";

    [MenuItem("PPOP/Android/Apply Android Settings")]
    public static void ApplySettings()
    {
        NamedBuildTarget android = NamedBuildTarget.Android;

        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(android, PackageName);

        // 64-bit ARM (every current phone); IL2CPP is required for ARM64.
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // OpenGL ES 3 only: with Vulkan the board (hundreds of URP/Lit tiles) did not render on Mali GPUs
        // (moto g20), while pawns and UI did. Every Android phone from the last ~10 years has GLES3.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        // Board game: landscape only.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // LAN multiplayer (Unity Transport sockets).
        PlayerSettings.Android.forceInternetPermission = true;

        // Draw behind the notch; ResponsiveCanvas keeps the UI inside the safe area.
        PlayerSettings.Android.renderOutsideSafeArea = true;

        // Plain APK to install by hand (not an .aab for the Play Store).
        EditorUserBuildSettings.buildAppBundle = false;

        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Ajustes de Android aplicados: {PackageName}, IL2CPP ARM64, horizontal, Internet.");
    }

    [MenuItem("PPOP/Android/Build APK")]
    public static void BuildRelease()
    {
        Build(false);
    }

    [MenuItem("PPOP/Android/Build APK (Development)")]
    public static void BuildDevelopment()
    {
        Build(true);
    }

    public static BuildReport Build(bool development)
    {
        return Build(development, OutputPath);
    }

    /// <summary>Builds the APK to any path (e.g. D:/JuegoPared/APK1/Pared.apk).</summary>
    public static BuildReport Build(bool development, string outputPath)
    {
        ApplySettings();

        // Switch the Editor to Android first: an APK built straight from the Windows target (first build
        // after the switch) rendered the board invisible on the phones, while a rebuild worked.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[PPOP] No se pudo cambiar la plataforma a Android.");
            return null;
        }

        string directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development ? BuildOptions.Development : BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"[PPOP] APK generado: {outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s)");
        else
            Debug.LogError($"[PPOP] Build de Android falló: {summary.result} ({summary.totalErrors} errores). ¿Está instalado Android Build Support con SDK/NDK/OpenJDK?");

        return report;
    }

    private static string[] EnabledScenes()
    {
        System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }

        return scenes.ToArray();
    }
}
