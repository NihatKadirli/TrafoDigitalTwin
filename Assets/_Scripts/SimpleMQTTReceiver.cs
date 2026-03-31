using UnityEngine;
using TMPro;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class SimpleMQTTReceiver : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerIP = "127.0.0.1";
    public int brokerPort = 1883;
    public string topic = "TM1/Trafo/sensor";
    
    [Header("UI Elements")]
    public TMP_Text txtTemperature;
    public TMP_Text txtVoltage;
    public TMP_Text txtLoad;
    public TMP_Text txtStatus;
    
    [Header("UI Colors")]
    public Color normalColor = Color.white;
    public Color warningColor = Color.yellow;
    public Color dangerColor = Color.red;
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Thread-safe systems
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private ConcurrentQueue<System.Action> mainThreadQueue = new ConcurrentQueue<System.Action>();
    private bool isRunning = false;
    private Thread mqttThread;
    
    // Data
    [System.Serializable]
    public class TrafoData
    {
        public float sicaklik;
        public float yag_sicaklik;
        public float gerilim_primer;
        public float gerilim_sekonder;
        public float akim_primer;
        public float akim_sekonder;
        public float yuk_seviyesi;
    }
    
    private TrafoData currentData = new TrafoData();
    private int messageCount = 0;
    private bool isConnected = false;
    
    void Start()
    {
        Log("=== MQTT RECEIVER STARTING ===");
        Log("Platform: " + Application.platform);
        Log("Broker: " + brokerIP + ":" + brokerPort);
        Log("Topic: " + topic);
        
        // Set default data
        currentData.sicaklik = 50.0f;
        currentData.yuk_seviyesi = 80.0f;
        currentData.gerilim_primer = 155000f;
        
        // Update UI
        UpdateUI();
        UpdateStatus("STARTING...", Color.yellow);
        
        // Start MQTT connection
        StartMQTT();
    }
    
    void StartMQTT()
    {
        if (isRunning) return;
        
        isRunning = true;
        mqttThread = new Thread(MQTTWorker);
        mqttThread.IsBackground = true;
        mqttThread.Start();
        
        Log("MQTT thread started");
    }
    
    void MQTTWorker()
    {
        TcpClient client = null;
        NetworkStream stream = null;
        
        try
        {
            Log("Connecting to MQTT broker...");
            
            // Create TCP client
            client = new TcpClient();
            client.Connect(brokerIP, brokerPort);
            stream = client.GetStream();
            
            Log("TCP connection established!");
            
            // Send MQTT Connect with SAFE buffer
            SendMQTTConnectSafe(stream, "UnityTrafoViewer");
            Thread.Sleep(100);
            
            // Send Subscribe with SAFE buffer
            SendMQTTSubscribeSafe(stream, topic);
            Thread.Sleep(100);
            
            // Update status via main thread queue
            EnqueueMainThreadAction(() => {
                UpdateStatus("CONNECTED ✓", connectedColor);
                Log("Successfully subscribed to: " + topic);
            });
            
            isConnected = true;
            
            // Main listening loop
            byte[] buffer = new byte[4096];
            while (isRunning && isConnected && client.Connected)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string rawData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessIncomingData(rawData);
                        }
                    }
                    Thread.Sleep(30);
                }
                catch (System.Exception e)
                {
                    LogWarning("Read error: " + e.Message);
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            LogError("MQTT ERROR: " + e.Message);
            EnqueueMainThreadAction(() => {
                UpdateStatus("ERROR: " + e.Message, disconnectedColor);
            });
        }
        finally
        {
            CleanupConnection(client, stream);
        }
    }
    
    // FIXED: UTF-8 Buffer hatası çözüldü
    void SendMQTTConnectSafe(NetworkStream stream, string clientId)
    {
        byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);
        
        // Use List<byte> for dynamic size
        List<byte> packet = new List<byte>();
        
        // Fixed header
        packet.Add(0x10); // CONNECT
        
        // Variable header
        packet.Add(0x00); // Protocol name length MSB
        packet.Add(0x04); // Protocol name length LSB
        packet.Add((byte)'M');
        packet.Add((byte)'Q');
        packet.Add((byte)'T');
        packet.Add((byte)'T');
        packet.Add(0x04); // Protocol version
        packet.Add(0x02); // Connect flags
        packet.Add(0x00); // Keep alive MSB
        packet.Add(0x3C); // Keep alive LSB (60)
        
        // Payload - Client ID
        packet.Add(0x00); // Client ID length MSB
        packet.Add((byte)clientIdBytes.Length); // Client ID length LSB
        packet.AddRange(clientIdBytes);
        
        // Calculate remaining length
        int remainingLength = packet.Count - 1;
        packet.Insert(1, (byte)remainingLength);
        
        // Send packet
        stream.Write(packet.ToArray(), 0, packet.Count);
    }
    
    // FIXED: UTF-8 Buffer hatası çözüldü
    void SendMQTTSubscribeSafe(NetworkStream stream, string topic)
    {
        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
        
        List<byte> packet = new List<byte>();
        
        // Fixed header
        packet.Add(0x82); // SUBSCRIBE
        
        // Variable header
        packet.Add(0x00); // Packet ID MSB
        packet.Add(0x01); // Packet ID LSB
        
        // Topic
        packet.Add(0x00); // Topic length MSB
        packet.Add((byte)topicBytes.Length); // Topic length LSB
        packet.AddRange(topicBytes);
        
        // QoS
        packet.Add(0x00); // QoS 0
        
        // Calculate remaining length
        int remainingLength = packet.Count - 1;
        packet.Insert(1, (byte)remainingLength);
        
        stream.Write(packet.ToArray(), 0, packet.Count);
    }
    
    void ProcessIncomingData(string rawData)
    {
        // Extract JSON
        int jsonStart = rawData.IndexOf('{');
        int jsonEnd = rawData.LastIndexOf('}');
        
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            string jsonMessage = rawData.Substring(jsonStart, jsonEnd - jsonStart + 1);
            messageQueue.Enqueue(jsonMessage);
            messageCount++;
            
            if (debugMode)
            {
                Log("RAW DATA #" + messageCount + ": " + jsonMessage);
            }
        }
    }
    
    void Update()
    {
        // Process main thread actions FIRST
        while (!mainThreadQueue.IsEmpty)
        {
            if (mainThreadQueue.TryDequeue(out System.Action action))
            {
                try
                {
                    action.Invoke();
                }
                catch (System.Exception e)
                {
                    LogWarning("Main thread action error: " + e.Message);
                }
            }
        }
        
        // Process MQTT messages
        while (!messageQueue.IsEmpty)
        {
            if (messageQueue.TryDequeue(out string jsonMessage))
            {
                ProcessMessage(jsonMessage);
            }
        }
        
        // Test with T key
        if (Input.GetKeyDown(KeyCode.T))
        {
            CreateTestData();
        }
        
        // Quit with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Log("Application quitting...");
            Application.Quit();
        }
    }
    
    void ProcessMessage(string jsonMessage)
    {
        try
        {
            TrafoData newData = JsonUtility.FromJson<TrafoData>(jsonMessage);
            currentData = newData;
            
            UpdateUI();
            
            // Log processed data
            if (debugMode)
            {
                Log($"PROCESSED #{messageCount}: " +
                    $"{currentData.sicaklik:F1}°C, " +
                    $"{currentData.gerilim_primer / 1000:F1}kV, " +
                    $"{currentData.yuk_seviyesi:F1}%");
            }
            
            // Check alarms
            CheckAlarms();
        }
        catch (System.Exception e)
        {
            LogWarning("JSON parse error: " + e.Message);
        }
    }
    
    void UpdateUI()
    {
        // Temperature
        if (txtTemperature != null && txtTemperature.isActiveAndEnabled)
        {
            txtTemperature.text = "SICAKLIK: " + currentData.sicaklik.ToString("F1") + "°C";
            
            if (currentData.sicaklik > 80)
                txtTemperature.color = dangerColor;
            else if (currentData.sicaklik > 70)
                txtTemperature.color = warningColor;
            else
                txtTemperature.color = normalColor;
        }
        
        // Voltage
        if (txtVoltage != null && txtVoltage.isActiveAndEnabled)
        {
            float kV = currentData.gerilim_primer / 1000f;
            txtVoltage.text = "GERILIM: " + kV.ToString("F1") + " kV";
            txtVoltage.color = normalColor;
        }
        
        // Load
        if (txtLoad != null && txtLoad.isActiveAndEnabled)
        {
            txtLoad.text = "YUK: " + currentData.yuk_seviyesi.ToString("F1") + "%";
            
            if (currentData.yuk_seviyesi > 90)
                txtLoad.color = warningColor;
            else
                txtLoad.color = normalColor;
        }
    }
    
    void UpdateStatus(string status, Color color)
    {
        if (txtStatus != null && txtStatus.isActiveAndEnabled)
        {
            txtStatus.text = status;
            txtStatus.color = color;
        }
    }
    
    void CheckAlarms()
    {
        if (currentData.sicaklik > 85)
        {
            LogWarning($"HIGH TEMPERATURE: {currentData.sicaklik:F1}°C");
        }
        
        if (currentData.yuk_seviyesi > 95)
        {
            LogWarning($"HIGH LOAD: {currentData.yuk_seviyesi:F1}%");
        }
    }
    
    void EnqueueMainThreadAction(System.Action action)
    {
        mainThreadQueue.Enqueue(action);
    }
    
    void CreateTestData()
    {
        currentData.sicaklik = UnityEngine.Random.Range(40f, 95f);
        currentData.yuk_seviyesi = UnityEngine.Random.Range(60f, 100f);
        currentData.gerilim_primer = UnityEngine.Random.Range(154000f, 160000f);
        
        UpdateUI();
        
        Log($"TEST DATA: " +
            $"{currentData.sicaklik:F1}°C, " +
            $"{currentData.gerilim_primer / 1000:F1}kV, " +
            $"{currentData.yuk_seviyesi:F1}%");
    }
    
    void CleanupConnection(TcpClient client, NetworkStream stream)
    {
        try
        {
            isConnected = false;
            
            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
            }
            
            if (client != null)
            {
                client.Close();
                client.Dispose();
            }
        }
        catch (System.Exception e)
        {
            LogWarning("Cleanup error: " + e.Message);
        }
    }
    
    // Thread-safe logging methods
    void Log(string message)
    {
        if (debugMode)
        {
            UnityEngine.Debug.Log(message);
        }
    }
    
    void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }
    
    void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }
    
    void OnDestroy()
    {
        isRunning = false;
        isConnected = false;
        
        if (mqttThread != null && mqttThread.IsAlive)
        {
            mqttThread.Join(1000);
        }
        
        Log("MQTT Receiver stopped");
    }
}