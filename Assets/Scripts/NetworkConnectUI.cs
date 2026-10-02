using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkConnectUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI ipAddressText;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;

    [Header("Network Settings")]
    [SerializeField] private ushort port = 7777;

    private bool eventsSubscribed = false;

    private void Awake()
    {
        ResolveUIReferences();
    }

    private void Start()
    {
        EnsureManager();
        SubscribeToManager();
        UpdateInitialUI();
    }

    private void OnEnable()
    {
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void EnsureManager()
    {
        if (NetworkConnectionManager.Instance == null)
        {
            var managerObj = new GameObject("NetworkConnectionManager");
            managerObj.AddComponent<NetworkConnectionManager>();
        }
    }

    public void ResolveUIReferences()
    {
        if (ipAddressText == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name.Contains("IP", StringComparison.OrdinalIgnoreCase) ||
                    t.text.Contains("IP", StringComparison.OrdinalIgnoreCase))
                {
                    ipAddressText = t;
                    break;
                }
            }
        }

        if (ipInputField == null)
        {
            ipInputField = GetComponentInChildren<TMP_InputField>(true);
        }

        // Buscar o enlazar botones existentes
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string btnName = btn.gameObject.name.ToLower();
            var textComp = btn.GetComponentInChildren<TextMeshProUGUI>();
            string text = textComp != null ? textComp.text.ToLower() : "";

            if (hostButton == null && (btnName.Contains("host") || text.Contains("host") || text.Contains("crear")))
            {
                hostButton = btn;
            }
            else if (clientButton == null && (btnName.Contains("client") || text.Contains("client") || text.Contains("unirse") || btnName.Contains("(1)")))
            {
                clientButton = btn;
            }
            else if (disconnectButton == null && (btnName.Contains("disconnect") || text.Contains("desconectar")))
            {
                disconnectButton = btn;
            }
        }

        // Crear DisconnectButton si no existe
        if (disconnectButton == null)
        {
            CreateDisconnectButton();
        }

        // Crear StatusText si no existe
        if (statusText == null)
        {
            CreateStatusText();
        }

        // Configurar listeners de botones
        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(StartHost);
            hostButton.onClick.AddListener(StartHost);
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveListener(StartClient);
            clientButton.onClick.AddListener(StartClient);
        }

        if (disconnectButton != null)
        {
            disconnectButton.onClick.RemoveListener(Disconnect);
            disconnectButton.onClick.AddListener(Disconnect);
            disconnectButton.gameObject.SetActive(false);
        }
    }

    private void CreateDisconnectButton()
    {
        GameObject btnObj = new GameObject("DisconnectButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -120f);
        rect.sizeDelta = new Vector2(220f, 45f);

        var img = btnObj.GetComponent<Image>();
        img.color = new Color(0.85f, 0.2f, 0.2f, 1f); // Rojo elegante

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "DESCONECTAR";
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        disconnectButton = btnObj.GetComponent<Button>();
        btnObj.SetActive(false);
    }

    private void CreateStatusText()
    {
        GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(transform, false);

        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -190f);
        rect.sizeDelta = new Vector2(500f, 50f);

        statusText = textObj.GetComponent<TextMeshProUGUI>();
        statusText.fontSize = 22;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = Color.white;
        statusText.text = "";
    }

    private void SubscribeToManager()
    {
        if (eventsSubscribed) return;
        if (NetworkConnectionManager.Instance == null) return;

        NetworkConnectionManager.Instance.OnConnected += HandleConnected;
        NetworkConnectionManager.Instance.OnDisconnected += HandleDisconnected;
        NetworkConnectionManager.Instance.OnConnectionFailed += HandleConnectionFailed;
        NetworkConnectionManager.Instance.OnPlayerCountChanged += HandlePlayerCountChanged;
        eventsSubscribed = true;
    }

    private void UnsubscribeFromManager()
    {
        if (!eventsSubscribed) return;
        if (NetworkConnectionManager.Instance == null) return;

        NetworkConnectionManager.Instance.OnConnected -= HandleConnected;
        NetworkConnectionManager.Instance.OnDisconnected -= HandleDisconnected;
        NetworkConnectionManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
        NetworkConnectionManager.Instance.OnPlayerCountChanged -= HandlePlayerCountChanged;
        eventsSubscribed = false;
    }

    public void UpdateInitialUI()
    {
        string localIP = NetworkConnectionManager.Instance != null
            ? NetworkConnectionManager.Instance.GetLocalIPAddress()
            : GetLocalIPAddress();

        if (ipAddressText != null)
        {
            ipAddressText.text = "Tu IP: " + localIP;
        }

        if (ipInputField != null && string.IsNullOrWhiteSpace(ipInputField.text))
        {
            ipInputField.text = "127.0.0.1";
        }

        SetStatus("", Color.white);
        SetButtonsInteractable(true);

        if (hostButton != null) hostButton.gameObject.SetActive(true);
        if (clientButton != null) clientButton.gameObject.SetActive(true);
        if (ipInputField != null) ipInputField.interactable = true;
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
    }

    public void StartHost()
    {
        EnsureManager();
        SubscribeToManager();

        SetStatus("Iniciando Host...", Color.yellow);
        SetButtonsInteractable(false);

        if (!NetworkConnectionManager.Instance.StartHost())
        {
            SetButtonsInteractable(true);
        }
    }

    public void StartClient()
    {
        EnsureManager();
        SubscribeToManager();

        string joinIp = "127.0.0.1";
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            joinIp = ipInputField.text.Trim();
        }

        SetStatus($"Conectando a {joinIp}:{port}...", Color.yellow);
        SetButtonsInteractable(false);

        if (!NetworkConnectionManager.Instance.StartClient(joinIp, port))
        {
            SetButtonsInteractable(true);
        }
    }

    public void Disconnect()
    {
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.Shutdown();
        }
        else
        {
            HandleDisconnected("Desconectado de la partida.");
        }
    }

    private void HandleConnected()
    {
        int count = NetworkConnectionManager.Instance != null
            ? NetworkConnectionManager.Instance.ConnectedPlayerCount
            : 1;

        UpdateConnectedView(count);
    }

    private void HandlePlayerCountChanged(int count)
    {
        if (NetworkConnectionManager.Instance != null && NetworkConnectionManager.Instance.IsConnected)
        {
            UpdateConnectedView(count);
        }
    }

    private void UpdateConnectedView(int count)
    {
        bool isHost = NetworkConnectionManager.Instance != null &&
                      Unity.Netcode.NetworkManager.Singleton != null &&
                      Unity.Netcode.NetworkManager.Singleton.IsHost;

        string role = isHost ? "Host" : "Cliente";
        int max = NetworkConnectionManager.Instance != null ? NetworkConnectionManager.Instance.MaxPlayers : 4;

        if (isHost && ipAddressText != null)
        {
            ipAddressText.text = "Hosteando en: " + NetworkConnectionManager.Instance.CurrentIP;
        }

        SetStatus($"Conectado ({role}) — {count}/{max} jugador(es)", new Color(0.2f, 0.95f, 0.2f));

        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (clientButton != null) clientButton.gameObject.SetActive(false);
        if (ipInputField != null) ipInputField.interactable = false;

        if (disconnectButton != null)
        {
            disconnectButton.gameObject.SetActive(true);
            disconnectButton.interactable = true;
        }
    }

    private void HandleDisconnected(string reason)
    {
        SetStatus(reason, Color.white);
        UpdateInitialUI();
    }

    private void HandleConnectionFailed(string error)
    {
        SetStatus("Error: " + error, new Color(1f, 0.35f, 0.35f));
        SetButtonsInteractable(true);
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        else if (ipAddressText != null && !string.IsNullOrEmpty(message))
        {
            ipAddressText.text = message;
            ipAddressText.color = color;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null) hostButton.interactable = interactable;
        if (clientButton != null) clientButton.interactable = interactable;
        if (ipInputField != null) ipInputField.interactable = interactable;
    }

    public string GetLocalIPAddress()
    {
        try
        {
            string hostName = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(hostName);

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error obteniendo la IP local: " + e.Message);
        }

        return "127.0.0.1";
    }
}