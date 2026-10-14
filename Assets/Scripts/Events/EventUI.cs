using UnityEngine;
using TMPro;

public class EventUI : MonoBehaviour
{
    public static EventUI Instance;

    [Header("UI")]
    public GameObject eventPanel;
    public TMP_Text eventText;

    [Header("Display")]
    public float displayTime = 2f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (eventPanel != null)
            eventPanel.SetActive(false);
    }

    public void ShowEventMessage()
    {
        if (eventPanel == null)
            return;

        if (eventText != null)
            eventText.text = "EVENTO ACTIVADO";

        eventPanel.SetActive(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterTime());
    }

    /// <summary>Shows any text in the existing event panel for the given seconds (network game messages).</summary>
    public void ShowMessage(string message, float seconds)
    {
        if (eventPanel == null)
            return;

        if (eventText != null)
            eventText.text = message;

        eventPanel.SetActive(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterTime(seconds));

        // Keep the scale chosen by ScaleToFit on small screens; the pop is relative to it.
        if (popCoroutine != null)
            StopCoroutine(popCoroutine);
        else
            popBase = eventPanel.transform.localScale;

        popCoroutine = StartCoroutine(Pop());
    }

    private Coroutine popCoroutine;
    private Vector3 popBase = Vector3.one;

    /// <summary>Enters with an overshoot (0.6 -> 1.12 -> 1) so the announcement grabs attention.</summary>
    private System.Collections.IEnumerator Pop()
    {
        Transform panel = eventPanel.transform;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.28f)
        {
            float s = t < 0.6f ? Mathf.Lerp(0.6f, 1.12f, t / 0.6f) : Mathf.Lerp(1.12f, 1f, (t - 0.6f) / 0.4f);
            panel.localScale = popBase * s;
            yield return null;
        }

        panel.localScale = popBase;
        popCoroutine = null;
    }

    private System.Collections.IEnumerator HideAfterTime()
    {
        yield return HideAfterTime(displayTime);
    }

    private System.Collections.IEnumerator HideAfterTime(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        eventPanel.SetActive(false);

        hideCoroutine = null;
    }
}