using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[InitializeOnLoad]
public static class FixSceneInputSystem
{
    static FixSceneInputSystem()
    {
        EditorApplication.delayCall += () =>
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    Object.DestroyImmediate(standalone);
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                    Debug.Log("EventSystem actualizado automáticamente al nuevo Input System (InputSystemUIInputModule).");
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
                }
            }
        };
    }
}
