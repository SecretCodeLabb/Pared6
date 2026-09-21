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
        // Muestra la IP local en pantalla por defecto para que el Host la conozca
        ipAddressText.text = "Tu IP: " + GetLocalIPAddress();
    }

    public void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // El Host debe escuchar en 0.0.0.0 (acepta conexiones de cualquier red local)
        transport.SetConnectionData("0.0.0.0", port);

        if (NetworkManager.Singleton.StartHost())
        {
            ipAddressText.text = "Hosteando en: " + GetLocalIPAddress();
        }
    }

    public void StartClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // Usa la IP del input, o localhost si está vacío
        string joinIp = string.IsNullOrEmpty(ipInputField.text) ? "127.0.0.1" : ipInputField.text;

        // El cliente se conecta a la IP ingresada
        transport.SetConnectionData(joinIp, port);
        NetworkManager.Singleton.StartClient();
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}