using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

[InitializeOnLoad]
public static class SetupNetworkEditor
{
    static SetupNetworkEditor()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorPrefs.GetBool("PPOP_NetworkSetupDone_T02", false))
            {
                SetupNetwork();
                EditorPrefs.SetBool("PPOP_NetworkSetupDone_T02", true);
            }
        };
    }

    [MenuItem("PPOP/Setup Network")]
    public static void SetupNetwork()
    {
        Debug.Log("[PPOP] Iniciando configuración de red para LobbyScene...");

        string lobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        Scene scene = EditorSceneManager.OpenScene(lobbyScenePath, OpenSceneMode.Single);

        // 1. Configurar NetworkManager + UnityTransport
        NetworkManager networkManager = Object.FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            GameObject nmObj = new GameObject("NetworkManager");
            networkManager = nmObj.AddComponent<NetworkManager>();
            Undo.RegisterCreatedObjectUndo(nmObj, "Create NetworkManager");
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.TickRate = 30;

        // 2. Configurar NetworkConnectionManager
        NetworkConnectionManager connManager = networkManager.GetComponent<NetworkConnectionManager>();
        if (connManager == null)
        {
            connManager = networkManager.gameObject.AddComponent<NetworkConnectionManager>();
        }

        // 3. Configurar UI en Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            NetworkConnectUI connectUI = canvas.GetComponent<NetworkConnectUI>();
            if (connectUI == null)
            {
                connectUI = canvas.gameObject.AddComponent<NetworkConnectUI>();
            }

            connectUI.ResolveUIReferences();
        }

        // 4. Actualizar Build Settings con todas las escenas requeridas
        UpdateBuildSettings();

        // 5. Guardar la escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[PPOP] Configuración de red (T02) completada con éxito en LobbyScene y Build Settings actualizados.");
    }

    private static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/LobbyScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/CharacterSelection.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Mapa.unity", true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[PPOP] EditorBuildSettings actualizado con las 3 escenas.");
    }
}
