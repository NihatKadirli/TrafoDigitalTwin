using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System;

public class UniversalMQTTReceiver : MonoBehaviour
{
    [Header("🌐 MQTT Settings")]
    public string brokerIP = "127.0.0.1";
    public int brokerPort = 1883;
    public string topic = "TM1/Trafo/sensor";
    
    [Header("🖥️ Platform Detection")]
    public bool useTMP = true;
    
    [Header("📊 UI Elements")]
    public TMP_Text tmpTemperature;
    public TMP_Text tmpVoltage;
    public TMP_Text tmpLoad;
    public TMP_Text tmpStatus;
    
    public Text uiTemperature;
    public Text uiVoltage;
    public Text uiLoad;
    public Text uiStatus;
    
    [Header("🔧 Connection")]
    public Image statusLight;
    
    // Thread-safe
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private Thread mqttThread;
    private bool isRunning = false;
    
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
    
    void Start()
    {
        Debug.Log("🌍 Universal MQTT Receiver Starting...");
        
        // Auto-detect platform
        #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            useTMP = false;
            Debug.Log("🍎 macOS detected - Using regular UI Text");
        #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            useTMP = true;
            Debug.Log("🪟 Windows detected - Using TMP Text");
        #endif
        
        // Default data
        currentData.sicaklik = 50f;
        currentData.yuk_seviyesi = 80f;
        currentData.gerilim_primer = 155000f;
        
        // Start connection
        StartMQTT();
        
        // Initial UI update
        UpdateUI();
    }
    
    void StartMQTT()
    {
        isRunning = true;
        mqttThread = new Thread(MQTTListener);
        mqttThread.IsBackground = true;
        mqttThread.Start();
        
        UpdateStatus("🔌 Connecting...", Color.yellow);
    }
    
    void MQTTListener()
    {
        try
        {
            Debug.Log("Connecting to MQTT broker...");
            
            using (TcpClient client = new TcpClient(brokerIP, brokerPort))
            using (NetworkStream stream = client.GetStream())
            {
                // Simple MQTT Connect
                SendSimpleConnect(stream);
                Thread.Sleep(100);
                
                // Simple Subscribe
                SendSimpleSubscribe(stream, topic);
                Thread.Sleep(100);
                
                // Connection successful
                UpdateStatus("✅ Connected", Color.green);
                Debug.Log("Subscribed to topic: " + topic);
                
                // Listen for messages
                byte[] buffer = new byte[4096];
                while (isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessMQTTData(data);
                        }
                    }
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("MQTT Error: " + e.Message);
            UpdateStatus("❌ Error: " + e.Message, Color.red);
        }
    }
    
    void SendSimpleConnect(NetworkStream stream)
    {
        string clientId = "UnityClient";
        byte[] packet = new byte[14 + clientId.Length];
        
        packet[0] = 0x10;
        packet[1] = (byte)(12 + clientId.Length);
        packet[2] = 0x00; packet[3] = 0x04;
        packet[4] = (byte)'M'; packet[5] = (byte)'Q';
        packet[6] = (byte)'T'; packet[7] = (byte)'T';
        packet[8] = 0x04;
        packet[9] = 0x02;
        packet[10] = 0x00; packet[11] = 0x3C;
        packet[12] = 0x00;
        packet[13] = (byte)clientId.Length;
        
        Encoding.UTF8.GetBytes(clientId, 0, clientId.Length, packet, 14);
        stream.Write(packet, 0, packet.Length);
    }
    
    void SendSimpleSubscribe(NetworkStream stream, string topic)
    {
        byte[] packet = new byte[5 + topic.Length];
        
        packet[0] = 0x82;
        packet[1] = (byte)(3 + topic.Length);
        packet[2] = 0x00; packet[3] = 0x01;
        packet[4] = 0x00;
        packet[5] = (byte)topic.Length;
        
        Encoding.UTF8.GetBytes(topic, 0, topic.Length, packet, 6);
        packet[6 + topic.Length] = 0x00;
        
        stream.Write(packet, 0, packet.Length);
    }
    
    void ProcessMQTTData(string rawData)
    {
        int jsonStart = rawData.IndexOf('{');
        if (jsonStart >= 0)
        {
            string jsonMessage = rawData.Substring(jsonStart);
            messageQueue.Enqueue(jsonMessage);
        }
    }
    
    void Update()
    {
        // Process messages
        while (!messageQueue.IsEmpty)
        {
            if (messageQueue.TryDequeue(out string jsonMessage))
            {
                ProcessTrafoMessage(jsonMessage);
            }
        }
        
        // Test key
        if (Input.GetKeyDown(KeyCode.T))
        {
            CreateTestData();
        }
    }
    
    void ProcessTrafoMessage(string jsonMessage)
    {
        try
        {
            TrafoData newData = JsonUtility.FromJson<TrafoData>(jsonMessage);
            currentData = newData;
            
            UpdateUI();
            UpdateStatus("📡 Receiving data...", Color.green);
            
            Debug.Log($"Data: {currentData.sicaklik}°C | {currentData.yuk_seviyesi}%");
        }
        catch (Exception e)
        {
            Debug.LogWarning("JSON Parse Error: " + e.Message);
        }
    }
    
    void UpdateUI()
    {
        if (useTMP) // TMP for Windows
        {
            if (tmpTemperature != null)
                tmpTemperature.text = $"🌡️ {currentData.sicaklik:F1}°C";
            if (tmpVoltage != null)
                tmpVoltage.text = $"⚡ {currentData.gerilim_primer / 1000:F1} kV";
            if (tmpLoad != null)
                tmpLoad.text = $"📊 {currentData.yuk_seviyesi:F1}%";
        }
        else // Regular UI for Mac
        {
            if (uiTemperature != null)
                uiTemperature.text = $"🌡️ {currentData.sicaklik:F1}°C";
            if (uiVoltage != null)
                uiVoltage.text = $"⚡ {currentData.gerilim_primer / 1000:F1} kV";
            if (uiLoad != null)
                uiLoad.text = $"📊 {currentData.yuk_seviyesi:F1}%";
        }
    }
    
    void UpdateStatus(string message, Color color)
    {
        if (useTMP && tmpStatus != null)
        {
            tmpStatus.text = message;
            tmpStatus.color = color;
        }
        else if (!useTMP && uiStatus != null)
        {
            uiStatus.text = message;
            uiStatus.color = color;
        }
        
        if (statusLight != null)
            statusLight.color = color;
    }
    
    void CreateTestData()
    {
        currentData.sicaklik = UnityEngine.Random.Range(40f, 95f);
        currentData.yuk_seviyesi = UnityEngine.Random.Range(60f, 100f);
        currentData.gerilim_primer = UnityEngine.Random.Range(154000f, 160000f);
        
        UpdateUI();
        Debug.Log("Test data created");
    }
    
    void OnDestroy()
    {
        isRunning = false;
        if (mqttThread != null && mqttThread.IsAlive)
        {
            mqttThread.Join(1000);
        }
    }
}