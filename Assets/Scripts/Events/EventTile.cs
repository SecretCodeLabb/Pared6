using UnityEngine;

public class EventTile : MonoBehaviour
{
    [Header("Event")]
    public bool hasEvent = true;

    private bool eventTriggered = false;

    public bool IsTriggered()
    {
        return eventTriggered;
    }

    public void ActivateEvent()
    {
        if (!hasEvent)
            return;

        if (eventTriggered)
            return;

        eventTriggered = true;

        Debug.Log("EVENTO ACTIVADO en: " + gameObject.name);

        EventManager.Instance.TriggerEvent(this);
    }

    public void ResetEvent()
    {
        eventTriggered = false;
    }
}