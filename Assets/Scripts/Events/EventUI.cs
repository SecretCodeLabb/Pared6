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