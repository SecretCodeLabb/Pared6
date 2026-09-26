using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Net;
using System.Net.Sockets;

public class NetworkConnectUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI ipAddressText;
    [SerializeField] private TMP_InputField ipInputField;

    [Header("Network Settings")]
    [SerializeField] private ushort port = 7777;

    private void Start()
    {
        if (ipAddressText != null)
        {
            ipAddressText.text = "Tu IP: " + GetLocalIPAddress();
        }
    }

    public void StartHost()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("No se encontró NetworkManager.");
            return;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("El NetworkManager ya se esta ejecutando. No se puede iniciar otro Host.");
            return;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el NetworkManager.");
            return;
        }

        transport.SetConnectionData("0.0.0.0", port);

        if (networkManager.StartHost())
        {
            string localIP = GetLocalIPAddress();

            if (ipAddressText != null)
            {
                ipAddressText.text = "Hosteando en: " + localIP;
            }

            Debug.Log("Host iniciado correctamente.");
            Debug.Log("IP LAN: " + localIP);
            Debug.Log("Puerto: " + port);
        }
        else
        {
            Debug.LogError("No se pudo iniciar el Host.");
        }
    }

    public void StartClient()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("No se encontró NetworkManager.");
            return;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("El NetworkManager ya está conectado o escuchando.");
            return;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el NetworkManager.");
            return;
        }

        string joinIp = "127.0.0.1";

        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            joinIp = ipInputField.text.Trim();
        }

        // Conectarse a la IP del Host
        transport.SetConnectionData(joinIp, port);

        if (networkManager.StartClient())
        {
            Debug.Log("Intentando conectar al Host: " + joinIp + ":" + port);
        }
        else
        {
            Debug.LogError("No se pudo iniciar el Cliente.");
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            string hostName = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(hostName);

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error obteniendo la IP local: " + e.Message);
        }

        return "127.0.0.1";
    }
}