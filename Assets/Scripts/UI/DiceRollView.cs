using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big dice in the middle of the screen: tumbles (random faces, wobble) for the roll animation
/// and lands on the result with a punch. Built at runtime by GameHUD; sprites from FxConfig
/// (dice face + pip), so the look can be replaced without code. Purely visual.
/// </summary>
public class DiceRollView : MonoBehaviour
{
    private const float Size = 170f;
    private const float HoldSeconds = 1.1f;

    // Pip layout per face on a 3x3 grid (-1..1).
    private static readonly Vector2[][] Faces =
    {
        new Vector2[0],
        new[] { new Vector2(0, 0) },
        new[] { new Vector2(-1, 1), new Vector2(1, -1) },
        new[] { new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1) },
        new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) },
        new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-1, -1), new Vector2(1, -1) },
        new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(-1, -1), new Vector2(1, -1) },
    };

    private CanvasGroup group;
    private RectTransform dice;
    private Image face;
    private Image shadow;
    private Image[] pips;
    private TextMeshProUGUI faceNumber;
    private TextMeshProUGUI label;
    private Coroutine routine;

    public static DiceRollView Create(Transform parent)
    {
        RectTransform root = UIFactory.CreateRect(parent, "DiceRoll");
        UIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(420f, 320f));
        DiceRollView view = root.gameObject.AddComponent<DiceRollView>();
        view.Build();
        return view;
    }

    private void Build()
    {
        FxConfig config = FxConfig.Instance;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        shadow = UIFactory.CreatePanel(transform, "Shadow", new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = config != null ? config.softCircle : null;
        shadow.raycastTarget = false;
        UIFactory.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -Size * 0.62f), new Vector2(Size * 1.2f, 40f));
        shadow.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        face = UIFactory.CreatePanel(transform, "Dice", Color.white);
        face.sprite = config != null ? config.diceFace : null;
        face.raycastTarget = false;
        dice = UIFactory.Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(Size, Size));
        dice.pivot = new Vector2(0.5f, 0.5f);

        pips = new Image[6];

        for (int i = 0; i < pips.Length; i++)
        {
            Image pip = UIFactory.CreatePanel(dice, "Pip" + i, new Color(0.12f, 0.1f, 0.1f));
            pip.sprite = config != null ? config.pip : null;
            pip.raycastTarget = false;
            UIFactory.Place(pip.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size * 0.2f, Size * 0.2f));
            pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pips[i] = pip;
        }

        // Faces above 6 (custom dice) show the number instead of pips.
        faceNumber = UIFactory.CreateText(dice, "Number", "", 90f);
        UIFactory.Stretch(faceNumber.rectTransform);
        faceNumber.color = new Color(0.12f, 0.1f, 0.1f);
        faceNumber.fontStyle = FontStyles.Bold;

        label = UIFactory.CreateText(transform, "Label", "", 40f);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(420f, 56f));
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;

        gameObject.SetActive(false);
    }

    /// <summary>Tumbles for <paramref name="rollSeconds"/>, lands on <paramref name="result"/> and shows the label.</summary>
    public void Play(int result, float rollSeconds, Color tint, string resultLabel)
    {
        gameObject.SetActive(true);

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine(result, Mathf.Max(0.3f, rollSeconds), tint, resultLabel));
    }

    private IEnumerator PlayRoutine(int result, float rollSeconds, Color tint, string resultLabel)
    {
        group.alpha = 1f;
        label.text = "";
        face.color = Color.Lerp(Color.white, tint, 0.18f);

        int lastFace = 0;
        float nextFace = 0f;

        // Tumble: thrown in from below, spinning, faces flicker and slow down.
        for (float t = 0f; t < rollSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / rollSeconds;

            if (t >= nextFace)
            {
                int f;
                do { f = Random.Range(1, 7); } while (f == lastFace);
                lastFace = f;
                ShowFace(f);
                nextFace = t + Mathf.Lerp(0.05f, 0.16f, k);
            }

            float bounce = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 90f;
            dice.anchoredPosition = new Vector2(Mathf.Sin(k * 9f) * 18f * (1f - k), 20f + bounce);
            dice.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 720f * (1f - k) + Mathf.Sin(t * 25f) * 8f * (1f - k));
            dice.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, k * 4f));
            shadow.rectTransform.localScale = Vector3.one * (1f - bounce / 200f);
            yield return null;
        }

        // Land: final face + punch.
        ShowFace(result);
        dice.anchoredPosition = new Vector2(0f, 20f);
        dice.localRotation = Quaternion.identity;
        label.text = resultLabel;
        label.color = tint;

        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float punch = Mathf.Sin(t / 0.25f * Mathf.PI) * 0.35f;
            dice.localScale = Vector3.one * (1f + punch);
            label.rectTransform.localScale = Vector3.one * (1f + punch * 0.6f);
            yield return null;
        }

        dice.localScale = Vector3.one;
        label.rectTransform.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(HoldSeconds);

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.3f;
            yield return null;
        }

        routine = null;
        gameObject.SetActive(false);
    }

    private void ShowFace(int value)
    {
        bool usePips = value >= 1 && value <= 6;
        Vector2[] layout = usePips ? Faces[value] : Faces[0];
        float step = Size * 0.27f;

        for (int i = 0; i < pips.Length; i++)
        {
            bool on = i < layout.Length;
            pips[i].enabled = on;

            if (on)
                pips[i].rectTransform.anchoredPosition = layout[i] * step;
        }

        faceNumber.text = usePips ? "" : value.ToString();
    }
}
