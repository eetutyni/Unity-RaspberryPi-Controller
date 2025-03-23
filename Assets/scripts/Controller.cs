using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
/*Written by Eetu Tyni, March 2025*/

public class Controller : MonoBehaviour
{
    private UdpClient udpClient;
    private List<RaspberryPi> discoveredPis = new List<RaspberryPi>();
    private RaspberryPi selectedPi;
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    [SerializeField] Transform piListContainer; 
    [SerializeField] GameObject piUIPrefab; 

    [SerializeField] private GameObject commandPanel; 
    [SerializeField] private TextMeshProUGUI selectedPiText;
    [SerializeField] private Button toggleLEDButton; 

    private bool isPiSelected = false; 

    void Start()
    {
        DiscoverRaspberryPis();

        commandPanel.SetActive(false);

        toggleLEDButton.onClick.AddListener(() => OnLEDToggleButtonClick());
    }

    void Update()
    {

        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }

        CheckConnectionStatus();
    }

    void DiscoverRaspberryPis()
    {
        try
        {
            Debug.Log("Starting discovery...");

            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            string discoveryMessage = "DISCOVER_RASPBERRY_PI";
            byte[] data = Encoding.UTF8.GetBytes(discoveryMessage);
            udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, 5000));
            Debug.Log("Discovery request sent.");

            Thread discoveryThread = new Thread(ReceiveResponses);
            discoveryThread.Start();
            Debug.Log("Discovery thread started.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error during discovery: " + e.Message);
        }
    }

    void ReceiveResponses()
    {
        Debug.Log("Listening for responses...");

        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                Debug.Log("Waiting for data...");

                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string response = Encoding.UTF8.GetString(data);
                Debug.Log($"Received data: {response} from {remoteEndPoint.Address}");

                if (response == "RASPBERRY_PI_RESPONSE")
                {
                    string piIP = remoteEndPoint.Address.ToString();

                    if (!discoveredPis.Exists(pi => pi.IPAddress == piIP))
                    {

                        var newPi = new RaspberryPi { IPAddress = piIP, IsConnected = false, IsLedOn = false };
                        discoveredPis.Add(newPi);
                        Debug.Log($"Received data: {response} from {remoteEndPoint.Address}");

                        mainThreadActions.Enqueue(() => AddPiToUI(newPi));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error receiving response: " + e.Message);
                break;
            }
        }
    }

    void AddPiToUI(RaspberryPi pi)
    {

        GameObject piUI = Instantiate(piUIPrefab, piListContainer);
        var uiElements = piUI.GetComponent<PiUIButton>();

        uiElements.ipText.text = $"Raspberry Pi: {pi.IPAddress}";


        uiElements.UpdateConnectionStatus(pi.IsConnected); 


        uiElements.connectButton.onClick.AddListener(() => ConnectToRaspberryPi(pi, uiElements));


        uiElements.selectButton.onClick.AddListener(() => SelectPi(pi, uiElements));
    }

    public void SelectPi(RaspberryPi pi, PiUIButton uiElements)
    {
        if (isPiSelected && selectedPi == pi)
        {

            uiElements.Deselect();
            selectedPi = null;
            isPiSelected = false;
            Debug.Log($"Deselected Raspberry Pi: {pi.IPAddress}");

            commandPanel.SetActive(false);
        }
        else
        {

            if (selectedPi != null)
            {
                var previousUI = piListContainer.Find(selectedPi.IPAddress)?.GetComponent<PiUIButton>();
                if (previousUI != null)
                {
                    previousUI.Deselect(); 
                }
            }

            // Select the new Pi
            selectedPi = pi;
            uiElements.Select(); 
            isPiSelected = true;
            Debug.Log($"Selected Raspberry Pi: {pi.IPAddress}");

            // Show the Command Panel and update the selected Pi text
            commandPanel.SetActive(true);
            selectedPiText.text = $"Selected Pi: {pi.IPAddress}";
        }

        uiElements.UpdateButtonText(isPiSelected && selectedPi == pi);
    }

    void ConnectToRaspberryPi(RaspberryPi pi, PiUIButton uiElements)
    {
        try
        {
            pi.TcpClient = new TcpClient(pi.IPAddress, 5001);
            pi.TcpStream = pi.TcpClient.GetStream();
            pi.IsConnected = true; 
            Debug.Log($"Connected to Raspberry Pi at {pi.IPAddress}");
            uiElements.UpdateConnectionStatus(true); // Update UI

            // Enable the Toggle LED button
            toggleLEDButton.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to Raspberry Pi at {pi.IPAddress}: {e.Message}");
            pi.IsConnected = false; 
            uiElements.UpdateConnectionStatus(false); // Update UI

            // Disable the Toggle LED button
            toggleLEDButton.interactable = false;
        }
    }

    public void OnLEDToggleButtonClick()
    {
        if (selectedPi != null && selectedPi.IsConnected)
        {
            // Toggle the LED state
            selectedPi.IsLedOn = !selectedPi.IsLedOn;

            // Send the command
            string command = selectedPi.IsLedOn ? "LED_ON" : "LED_OFF";
            SendCommand(selectedPi, command);

            // Update the button text
            toggleLEDButton.GetComponentInChildren<TextMeshProUGUI>().text = selectedPi.IsLedOn ? "Turn LED Off" : "Turn LED On";
        }
        else
        {
            Debug.LogWarning("No Raspberry Pi selected or connected.");
        }
    }

    void CheckConnectionStatus()
    {
        foreach (var pi in discoveredPis)
        {
            if (pi.IsConnected)
            {
                try
                {
                    // Check if the connection is still alive
                    if (pi.TcpClient == null || !pi.TcpClient.Connected)
                    {
                        pi.IsConnected = false;
                        UpdateConnectionStatusUI(pi);

                        // Disable the Toggle LED button if the selected Pi is disconnected
                        if (selectedPi == pi)
                        {
                            toggleLEDButton.interactable = false;
                        }
                    }
                }
                catch (Exception)
                {
                    pi.IsConnected = false;
                    UpdateConnectionStatusUI(pi);

                    // Disable the Toggle LED button if the selected Pi is disconnected
                    if (selectedPi == pi)
                    {
                        toggleLEDButton.interactable = false;
                    }
                }
            }
        }
    }

    void UpdateConnectionStatusUI(RaspberryPi pi)
    {
        var uiElements = piListContainer.Find(pi.IPAddress)?.GetComponent<PiUIButton>();
        if (uiElements != null)
        {
            uiElements.UpdateConnectionStatus(pi.IsConnected);
        }
    }

    public void SendCommand(RaspberryPi pi, string command)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(command);
            pi.TcpStream.Write(data, 0, data.Length);
            Debug.Log($"Sent command: {command} to {pi.IPAddress}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending command to {pi.IPAddress}: {e.Message}");
            pi.IsConnected = false; 
            UpdateConnectionStatusUI(pi); // Update UI

            // Disable the Toggle LED button if the selected Pi is disconnected
            if (selectedPi == pi)
            {
                toggleLEDButton.interactable = false;
            }
        }
    }

    void OnDestroy()
    {
        udpClient?.Close();
        foreach (var pi in discoveredPis)
        {
            pi.TcpStream?.Close();
            pi.TcpClient?.Close();
            pi.IsConnected = false;
            UpdateConnectionStatusUI(pi); // Update UI
        }
    }
}
[System.Serializable]
public class RaspberryPi
{
    public string IPAddress;
    public TcpClient TcpClient;
    public NetworkStream TcpStream;
    public bool IsConnected; 
    public bool IsLedOn;
}