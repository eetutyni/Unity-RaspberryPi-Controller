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
/*Updated with Sensor Relay Functionality*/

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
    [SerializeField] private Button refreshSensorButton;
    [SerializeField] private Button relayDataButton;
    [SerializeField] private TextMeshProUGUI temperatureText;
    [SerializeField] private TextMeshProUGUI humidityText;
    [SerializeField] private TextMeshProUGUI pressureText;
    [SerializeField] private TMP_Dropdown targetPiDropdown;
    [SerializeField] private float sensorRefreshInterval = 2.0f;

    private bool isPiSelected = false;
    private float lastSensorRefreshTime;
    private bool isRefreshingSensors;
    private string lastSensorData = "";

    void Start()
    {
        DiscoverRaspberryPis();

        commandPanel.SetActive(false);

        toggleLEDButton.onClick.AddListener(() => OnLEDToggleButtonClick());
        refreshSensorButton.onClick.AddListener(RefreshSensorData);
        relayDataButton.onClick.AddListener(OnRelayDataButtonClick);

        // Initialize sensor UI
        temperatureText.text = "Temperature: --";
        humidityText.text = "Humidity: --";
        pressureText.text = "Pressure: --";

        // Initialize dropdown
        targetPiDropdown.ClearOptions();
        targetPiDropdown.interactable = false;
        relayDataButton.interactable = false;
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }

        CheckConnectionStatus();

        // Automatic sensor refresh
        if (selectedPi != null && selectedPi.IsConnected &&
            Time.time - lastSensorRefreshTime > sensorRefreshInterval &&
            !isRefreshingSensors)
        {
            RefreshSensorData();
        }
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

                        mainThreadActions.Enqueue(() => {
                            AddPiToUI(newPi);
                            UpdateTargetPiDropdown();
                        });
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

    void UpdateTargetPiDropdown()
    {
        targetPiDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (var pi in discoveredPis)
        {
            if (selectedPi == null || pi.IPAddress != selectedPi.IPAddress)
            {
                options.Add(pi.IPAddress);
            }
        }

        targetPiDropdown.AddOptions(options);
        targetPiDropdown.interactable = options.Count > 0;
        relayDataButton.interactable = options.Count > 0 && selectedPi != null && selectedPi.IsConnected;
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

            selectedPi = pi;
            uiElements.Select();
            isPiSelected = true;
            Debug.Log($"Selected Raspberry Pi: {pi.IPAddress}");

            commandPanel.SetActive(true);
            selectedPiText.text = $"Selected Pi: {pi.IPAddress}";

            // Enable/disable buttons based on connection status
            toggleLEDButton.interactable = pi.IsConnected;
            refreshSensorButton.interactable = pi.IsConnected;
            relayDataButton.interactable = pi.IsConnected && discoveredPis.Count > 1;
        }

        uiElements.UpdateButtonText(isPiSelected && selectedPi == pi);
        UpdateTargetPiDropdown();
    }

    void ConnectToRaspberryPi(RaspberryPi pi, PiUIButton uiElements)
    {
        try
        {
            pi.TcpClient = new TcpClient(pi.IPAddress, 5001);
            pi.TcpStream = pi.TcpClient.GetStream();
            pi.IsConnected = true;
            Debug.Log($"Connected to Raspberry Pi at {pi.IPAddress}");
            uiElements.UpdateConnectionStatus(true);

            toggleLEDButton.interactable = true;
            refreshSensorButton.interactable = true;
            relayDataButton.interactable = discoveredPis.Count > 1;

            // Immediately refresh sensor data on connect
            mainThreadActions.Enqueue(() => RefreshSensorData());
            UpdateTargetPiDropdown();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to Raspberry Pi at {pi.IPAddress}: {e.Message}");
            pi.IsConnected = false;
            uiElements.UpdateConnectionStatus(false);

            toggleLEDButton.interactable = false;
            refreshSensorButton.interactable = false;
            relayDataButton.interactable = false;
        }
    }

    public void OnLEDToggleButtonClick()
    {
        if (selectedPi != null && selectedPi.IsConnected)
        {
            selectedPi.IsLedOn = !selectedPi.IsLedOn;
            string command = selectedPi.IsLedOn ? "LED_ON" : "LED_OFF";
            SendCommand(selectedPi, command);

            toggleLEDButton.GetComponentInChildren<TextMeshProUGUI>().text =
                selectedPi.IsLedOn ? "Turn LED Off" : "Turn LED On";
        }
        else
        {
            Debug.LogWarning("No Raspberry Pi selected or connected.");
        }
    }

    public void OnRelayDataButtonClick()
    {
        if (selectedPi == null || !selectedPi.IsConnected || string.IsNullOrEmpty(lastSensorData))
        {
            Debug.LogWarning("No data to relay or no Pi selected");
            return;
        }

        if (targetPiDropdown.options.Count == 0)
        {
            Debug.LogWarning("No target Pi available for relaying");
            return;
        }

        string targetIp = targetPiDropdown.options[targetPiDropdown.value].text;
        RaspberryPi targetPi = discoveredPis.Find(p => p.IPAddress == targetIp && p.IsConnected);

        if (targetPi != null)
        {
            RelaySensorData(selectedPi, targetPi, lastSensorData);
        }
        else
        {
            Debug.LogWarning($"Target Pi {targetIp} is not connected");
        }
    }

    public void RefreshSensorData()
    {
        if (selectedPi != null && selectedPi.IsConnected && !isRefreshingSensors)
        {
            isRefreshingSensors = true;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    SendCommand(selectedPi, "GET_SENSOR_DATA");

                    byte[] buffer = new byte[1024];
                    int bytesRead = selectedPi.TcpStream.Read(buffer, 0, buffer.Length);
                    string sensorData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    lastSensorData = sensorData; // Store for potential relaying

                    var parts = sensorData.Split(',');
                    float temp = 0, hum = 0, pres = 0;

                    foreach (var part in parts)
                    {
                        if (part.StartsWith("TEMP:"))
                            float.TryParse(part.Substring(5), out temp);
                        else if (part.StartsWith("HUM:"))
                            float.TryParse(part.Substring(4), out hum);
                        else if (part.StartsWith("PRES:"))
                            float.TryParse(part.Substring(5), out pres);
                    }

                    mainThreadActions.Enqueue(() =>
                    {
                        temperatureText.text = $"Temperature: {temp:F1}°C";
                        humidityText.text = $"Humidity: {hum:F1}%";
                        pressureText.text = $"Pressure: {pres:F1} hPa";
                        isRefreshingSensors = false;
                        lastSensorRefreshTime = Time.time;
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading sensor data: {e.Message}");
                    mainThreadActions.Enqueue(() =>
                    {
                        temperatureText.text = "Temperature: --";
                        humidityText.text = "Humidity: --";
                        pressureText.text = "Pressure: --";
                        isRefreshingSensors = false;

                        selectedPi.IsConnected = false;
                        UpdateConnectionStatusUI(selectedPi);
                        toggleLEDButton.interactable = false;
                        refreshSensorButton.interactable = false;
                        relayDataButton.interactable = false;
                    });
                }
            });
        }
    }

    void RelaySensorData(RaspberryPi sourcePi, RaspberryPi targetPi, string sensorData)
    {
        try
        {
            string command = $"DISPLAY_SENSOR_DATA:{sensorData}";
            byte[] data = Encoding.UTF8.GetBytes(command);
            targetPi.TcpStream.Write(data, 0, data.Length);
            Debug.Log($"Relayed sensor data from {sourcePi.IPAddress} to {targetPi.IPAddress}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error relaying data to {targetPi.IPAddress}: {e.Message}");
            targetPi.IsConnected = false;
            UpdateConnectionStatusUI(targetPi);
            UpdateTargetPiDropdown();
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
                    if (pi.TcpClient == null || !pi.TcpClient.Connected)
                    {
                        pi.IsConnected = false;
                        UpdateConnectionStatusUI(pi);

                        if (selectedPi == pi)
                        {
                            toggleLEDButton.interactable = false;
                            refreshSensorButton.interactable = false;
                            relayDataButton.interactable = false;
                        }
                    }
                }
                catch (Exception)
                {
                    pi.IsConnected = false;
                    UpdateConnectionStatusUI(pi);

                    if (selectedPi == pi)
                    {
                        toggleLEDButton.interactable = false;
                        refreshSensorButton.interactable = false;
                        relayDataButton.interactable = false;
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
            UpdateConnectionStatusUI(pi);

            if (selectedPi == pi)
            {
                toggleLEDButton.interactable = false;
                refreshSensorButton.interactable = false;
                relayDataButton.interactable = false;
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
            UpdateConnectionStatusUI(pi);
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