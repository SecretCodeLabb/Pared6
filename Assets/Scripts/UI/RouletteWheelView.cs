using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Visual roulette wheel. All looks live in the RouletteWheel prefab (Prefabs/UI) and can be
/// restyled freely: change sprites, colours, fonts, sizes or layout, as long as the references
/// below stay assigned. Segments are cloned from <see cref="segmentTemplate"/> (an Image set to
/// Filled / Radial 360 / origin Top / clockwise, with a TMP label child) and coloured from
/// Resources/RouletteConfig. Gameplay never depends on this script: it only displays what the
/// Host decided (RouletteManager) and raises <see cref="SpinClicked"/>.
/// </summary>
public class RouletteWheelView : MonoBehaviour
{
    [Header("Required references")]
    [Tooltip("Rotates during the spin. Segments are created inside it.")]
    [SerializeField] private RectTransform wheel;
    [Tooltip("Disabled template: Image (Filled, Radial360, Top, clockwise) + child TextMeshProUGUI label.")]
    [SerializeField] private Image segmentTemplate;
    [SerializeField] private Button spinButton;

    [Header("Optional texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text spinButtonLabel;

    [Header("Look & feel")]
    [Tooltip("Label distance from the centre, as a fraction of the wheel radius.")]
    [Range(0.2f, 1f)] [SerializeField] private float labelRadius = 0.64f;
    [Tooltip("Rotate labels along the radius (true) or keep them tangent (false).")]
    [SerializeField] private bool radialLabels = true;
    [Tooltip("Full turns before stopping.")]
    [Min(1)] [SerializeField] private int extraTurns = 5;
    [Tooltip("How far inside its segment the wheel may stop (0 = always centre).")]
    [Range(0f, 0.45f)] [SerializeField] private float stopJitter = 0.3f;
    [SerializeField] private AnimationCurve spinCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("Texts (Spanish)")]
    [SerializeField] private string title = "¡RULETA!";
    [SerializeField] private string spinLabel = "GIRAR";
    [SerializeField] private string waitingFormat = "{0} va a girar la ruleta...";
    [SerializeField] private string localTurnText = "¡Te toca girar la ruleta! (Espacio)";
    [Tooltip("Appended while waiting; {0} = seconds left before the wheel spins by itself.")]
    [SerializeField] private string autoSpinFormat = "\n<size=75%>gira sola en {0} s</size>";

    /// <summary>Raised when the local player presses the spin button.</summary>
    public event Action SpinClicked;

    private readonly List<float> segmentCenters = new List<float>();
    private readonly List<float> segmentSizes = new List<float>();
    private RouletteConfig builtFor;
    private Coroutine spinRoutine;

    public bool IsSpinning => spinRoutine != null;

    private void Awake()
    {
        if (wheel == null || segmentTemplate == null || spinButton == null)
            Debug.LogError("RouletteWheelView: assign wheel, segmentTemplate and spinButton in the prefab.");

        if (spinButton != null)
        {
            spinButton.onClick.AddListener(() => SpinClicked?.Invoke());

            // Restyling safety: the button's graphic must receive clicks, otherwise the
            // full-screen dimmer behind it swallows them and the wheel can never be spun.
            if (spinButton.targetGraphic != null)
                spinButton.targetGraphic.raycastTarget = true;
        }

        if (segmentTemplate != null)
            segmentTemplate.gameObject.SetActive(false);

        if (titleText != null)
            titleText.text = title;

        if (spinButtonLabel != null)
            spinButtonLabel.text = spinLabel;
    }

    // =========================================================
    // BUILD
    // =========================================================

    /// <summary>Creates one segment per option (equal or weight-sized).</summary>
    public void Build(RouletteConfig config)
    {
        if (config == null || wheel == null || segmentTemplate == null || builtFor == config)
            return;

        builtFor = config;
        segmentCenters.Clear();
        segmentSizes.Clear();

        foreach (Transform child in wheel)
        {
            if (child != segmentTemplate.transform)
                Destroy(child.gameObject);
        }

        int count = config.options.Count;
        float total = config.TotalWeight;
        float start = 0f;

        for (int i = 0; i < count; i++)
        {
            RouletteOption option = config.options[i];
            float size = config.segmentSizeByWeight && total > 0f
                ? 360f * Mathf.Max(0f, option.weight) / total
                : 360f / count;

            Image segment = Instantiate(segmentTemplate, wheel);
            segment.name = "Segment_" + i;
            segment.gameObject.SetActive(true);
            segment.color = option.color;
            segment.type = Image.Type.Filled;
            segment.fillMethod = Image.FillMethod.Radial360;
            segment.fillOrigin = (int)Image.Origin360.Top;
            segment.fillClockwise = true;
            segment.fillAmount = size / 360f;

            // Rotating clockwise by 'start' puts this slice at [start, start + size] from the top.
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -start);

            TMP_Text label = segment.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = option.label;
                PlaceLabel(label.rectTransform, size * 0.5f, start + size * 0.5f);
            }

            segmentCenters.Add(start + size * 0.5f);
            segmentSizes.Add(size);
            start += size;
        }

        // Keep anything drawn above the segments (rim, hub...) on top.
        segmentTemplate.transform.SetAsFirstSibling();
    }

    private void PlaceLabel(RectTransform label, float halfAngle, float absoluteAngle)
    {
        float diameter = Mathf.Min(wheel.rect.width, wheel.rect.height);
        float radius = diameter * 0.5f * labelRadius;
        float rad = halfAngle * Mathf.Deg2Rad;

        // Local to the (already rotated) segment: angle measured clockwise from the top.
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
        label.pivot = new Vector2(0.5f, 0.5f);
        label.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
        // Labels on the left half are turned 180 degrees so none reads upside down at rest.
        float flip = radialLabels && absoluteAngle > 180f ? 180f : 0f;
        label.localRotation = Quaternion.Euler(0f, 0f, -halfAngle + (radialLabels ? 90f : 0f) + flip);
    }

    private void Update()
    {
        // Keyboard shortcut for the same action as the button.
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || spinButton == null || !spinButton.isActiveAndEnabled || !spinButton.interactable)
            return;

        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            SpinClicked?.Invoke();
    }

    // =========================================================
    // STATES
    // =========================================================

    /// <param name="isLocalSpinner">The local player is the one spinning.</param>
    /// <param name="localCanSpin">The local player can press the button right now.</param>
    /// <param name="autoSpinIn">Seconds before the automatic spin, or -1 to hide the countdown.</param>
    public void Show(string playerName, bool isLocalSpinner, bool localCanSpin, float autoSpinIn = -1f)
    {
        gameObject.SetActive(true);

        if (playerText != null)
        {
            string text;

            if (isLocalSpinner)
                text = localCanSpin ? localTurnText : "";
            else
                text = string.Format(waitingFormat, playerName);

            if (autoSpinIn >= 0f && !IsSpinning)
                text += string.Format(autoSpinFormat, Mathf.CeilToInt(autoSpinIn));

            playerText.text = text;
        }

        if (spinButton != null)
        {
            spinButton.gameObject.SetActive(localCanSpin || IsSpinning);
            spinButton.interactable = localCanSpin && !IsSpinning;
        }
    }

    public void Hide()
    {
        if (spinRoutine != null)
        {
            StopCoroutine(spinRoutine);
            spinRoutine = null;
        }

        if (resultText != null)
            resultText.text = "";

        gameObject.SetActive(false);
    }

    /// <summary>Animates the wheel so the given option ends under the pointer (top).</summary>
    public void PlaySpin(int index, float duration)
    {
        if (index < 0 || index >= segmentCenters.Count || wheel == null)
            return;

        gameObject.SetActive(true);

        if (resultText != null)
            resultText.text = "";

        if (spinButton != null)
            spinButton.interactable = false;

        if (spinRoutine != null)
            StopCoroutine(spinRoutine);

        spinRoutine = StartCoroutine(SpinRoutine(index, duration));
    }

    public void ShowResult(string text)
    {
        if (resultText != null)
            resultText.text = text;
    }

    private IEnumerator SpinRoutine(int index, float duration)
    {
        float jitter = UnityEngine.Random.Range(-stopJitter, stopJitter) * segmentSizes[index];

        // A wheel rotated by +θ (counter-clockwise) shows its clockwise angle θ under the top pointer.
        float startAngle = Mathf.Repeat(wheel.localEulerAngles.z, 360f);
        float target = segmentCenters[index] + jitter;
        float endAngle = target + 360f * extraTurns;

        while (endAngle - startAngle < 360f * extraTurns)
            endAngle += 360f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = spinCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            wheel.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startAngle, endAngle, t));
            yield return null;
        }

        wheel.localRotation = Quaternion.Euler(0f, 0f, endAngle);
        spinRoutine = null;
    }
}
