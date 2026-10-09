using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes a screen-space canvas work on any screen (desktop, tablets, phones with notches):
/// - CanvasScaler in Expand mode: the reference resolution always fits (wider screens get extra
///   width, taller screens extra height) so nothing designed for 1920x1080 is cut off.
/// - Optional bigger UI on small physical screens (<see cref="phoneScale"/>), for layouts built
///   to fit the smaller logical canvas (the game HUD fits 1280x720).
/// - Safe area: content is moved into a "SafeArea" child that avoids notches / rounded corners.
///   Full-screen backgrounds and dimmers stay outside so they still cover the whole screen.
/// Added automatically to every scene canvas (no scene edits); runtime canvases add it themselves.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvas : MonoBehaviour
{
    // Physical screen height (inches, landscape) for "phone" and "large screen".
    private const float PhoneInches = 3.2f;
    private const float LargeInches = 5f;

    [Tooltip("UI scale on phones (1 = same proportions as on a monitor).")]
    [Min(1f)] public float phoneScale = 1f;
    [Tooltip("Move the content inside the device safe area.")]
    public bool useSafeArea = true;

    private CanvasScaler scaler;
    private Vector2 designResolution;
    private RectTransform safeArea;
    private Rect appliedSafeArea;
    private Vector2Int appliedScreen;

    /// <summary>Testing in the Editor: treat the screen as a phone (or a big screen).</summary>
    public static bool? ForcePhone;

    /// <summary>Container to add content to (the safe area once built, otherwise the canvas).</summary>
    public RectTransform Content => safeArea != null ? safeArea : (RectTransform)transform;

    /// <summary>0 on a big screen, 1 on a phone.</summary>
    public static float PhoneFactor
    {
        get
        {
            if (ForcePhone.HasValue)
                return ForcePhone.Value ? 1f : 0f;

            float dpi = Screen.dpi;

            if (dpi <= 1f)
                return Application.isMobilePlatform ? 1f : 0f;

            float inches = Mathf.Min(Screen.width, Screen.height) / dpi;
            return Mathf.InverseLerp(LargeInches, PhoneInches, inches);
        }
    }

    public static bool IsTouchDevice => Application.isMobilePlatform || ForcePhone == true;

    public static ResponsiveCanvas Ensure(Canvas canvas, float phoneScale = 1f)
    {
        ResponsiveCanvas responsive = canvas.GetComponent<ResponsiveCanvas>();

        if (responsive == null)
        {
            if (canvas.GetComponent<CanvasScaler>() == null)
                canvas.gameObject.AddComponent<CanvasScaler>();

            responsive = canvas.gameObject.AddComponent<ResponsiveCanvas>();
        }

        responsive.phoneScale = Mathf.Max(responsive.phoneScale, phoneScale);
        responsive.Apply(true);
        return responsive;
    }

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        designResolution = scaler.referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private void Start()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        if (scaler == null)
            Awake();

        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        Rect area = Screen.safeArea;

        if (!force && screen == appliedScreen && area == appliedSafeArea)
            return;

        appliedScreen = screen;
        appliedSafeArea = area;

        float scale = Mathf.Lerp(1f, phoneScale, PhoneFactor);
        scaler.referenceResolution = designResolution / scale;

        if (useSafeArea)
            ApplySafeArea(screen, area);
    }

    private void ApplySafeArea(Vector2Int screen, Rect area)
    {
        if (screen.x <= 0 || screen.y <= 0)
            return;

        if (safeArea == null)
            BuildSafeArea();

        safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
        safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }

    private void BuildSafeArea()
    {
        GameObject go = new GameObject("SafeArea", typeof(RectTransform));
        safeArea = (RectTransform)go.transform;
        safeArea.SetParent(transform, false);

        List<Transform> children = new List<Transform>();

        foreach (Transform child in transform)
        {
            if (child != safeArea)
                children.Add(child);
        }

        // Keep the drawing order: the safe area takes the place of the first moved child.
        int insertAt = -1;

        foreach (Transform child in children)
        {
            if (IsFullScreen(child as RectTransform))
                continue;

            if (insertAt < 0)
                insertAt = child.GetSiblingIndex();

            child.SetParent(safeArea, false);
        }

        if (insertAt >= 0)
            safeArea.SetSiblingIndex(Mathf.Min(insertAt, transform.childCount - 1));
    }

    /// <summary>Backgrounds / dimmers stretched over the whole canvas.</summary>
    private static bool IsFullScreen(RectTransform rect)
    {
        return rect != null &&
               rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
               rect.offsetMin.sqrMagnitude < 1f && rect.offsetMax.sqrMagnitude < 1f &&
               rect.GetComponent<Selectable>() == null;
    }

    // =========================================================
    // AUTOMATIC SETUP FOR SCENE CANVASES
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupSceneCanvases();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupSceneCanvases();
    }

    private static void SetupSceneCanvases()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.GetComponent<ResponsiveCanvas>() == null)
                canvas.gameObject.AddComponent<ResponsiveCanvas>();
        }
    }
}
