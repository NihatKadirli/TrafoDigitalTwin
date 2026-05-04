using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class SimpleMQTTReceiver : MonoBehaviour
{
    
    [Header("UI References")]
    public Text temperatureText;      // Sıcaklık göstergesi
    public Text voltageText;          // Gerilim göstergesi
    public Text loadText;             // Yük göstergesi
    public Text statusText;           // Durum göstergesi
    public GameObject alarmPanel;     // Alarm paneli
    
    [Header("MQTT Settings")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;
    public string topic = "TM1/Trafo/sensor";
    
    // MQTT bağlantısı için
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread mqttThread;
    private bool isConnected = false;
    private bool isRunning = true;
    
    // Son alınan veriler
    private float lastTemperature = 72f;   // Başlangıç değeri
    private float lastVoltage = 154f;       // Başlangıç değeri
    private float lastLoad = 80f;           // Başlangıç değeri
    private float lastMessageTime = 0;      // Time.time için float
    
    // ===== SALDIRI YÖNETİMİ İÇİN YENİ DEĞİŞKENLER =====
    private bool isUnderAttack = false;
    private string attackType = "";
    private float attackValue = 0;
    private bool dataLossSimulated = false;
    
    void Start()
    {
        // UI referanslarını bul (eğer atanmadıysa)
        if (temperatureText == null)
            temperatureText = GameObject.Find("TemperatureValue")?.GetComponent<Text>();
        if (voltageText == null)
            voltageText = GameObject.Find("VoltageValue")?.GetComponent<Text>();
        if (loadText == null)
            loadText = GameObject.Find("LoadValue")?.GetComponent<Text>();
        if (statusText == null)
            statusText = GameObject.Find("StatusValue")?.GetComponent<Text>();
        
        // MQTT bağlantısını başlat
        ConnectToMQTT();
        
        // Alarm panelini gizle
        if (alarmPanel != null)
            alarmPanel.SetActive(false);
        
        // Son mesaj zamanını başlat
        lastMessageTime = Time.time;
        
        Debug.Log("SimpleMQTTReceiver Başlatıldı - Saldırı simülasyonu için hazır");
    }
    
    void ConnectToMQTT()
    {
        try
        {
            tcpClient = new TcpClient(brokerAddress, brokerPort);
            stream = tcpClient.GetStream();
            
            // MQTT CONNECT paketini gönder
            SendConnectPacket();
            
            // MQTT SUBSCRIBE paketini gönder
            SendSubscribePacket();
            
            // Dinleme thread'ini başlat
            mqttThread = new Thread(ListenForMessages);
            mqttThread.Start();
            
            isConnected = true;
            Debug.Log($"✅ MQTT Broker'a bağlandı: {brokerAddress}:{brokerPort}");
            
            if (statusText != null)
                statusText.text = "Bağlı";
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ MQTT bağlantı hatası: {e.Message}");
            if (statusText != null)
                statusText.text = "Bağlantı Yok";
        }
    }
    
    void SendConnectPacket()
    {
        // MQTT CONNECT paketi (basitleştirilmiş)
        byte[] connectPacket = new byte[] {
            0x10,       // CONNECT kontrol paketi
            0x0E,       // Kalan uzunluk
            0x00, 0x04, // Protokol adı uzunluğu
            0x4D, 0x51, 0x54, 0x54, // "MQTT"
            0x04,       // Protokol seviyesi
            0x02,       // Bağlantı flag'leri (temiz oturum)
            0x00, 0x3C, // Keep alive (60 saniye)
            0x00, 0x0A, // Client ID uzunluğu
            0x55, 0x6E, 0x69, 0x74, 0x79, 0x54, 0x72, 0x61, 0x66, 0x6F // "UnityTrafo"
        };
        
        stream.Write(connectPacket, 0, connectPacket.Length);
    }
    
    void SendSubscribePacket()
    {
        // Topic'i byte array'e çevir
        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
        
        // SUBSCRIBE paketi oluştur
        List<byte> subscribePacket = new List<byte>();
        subscribePacket.Add(0x82); // SUBSCRIBE kontrol paketi
        
        // Kalan uzunluk (packet ID 2 byte + topic uzunluğu 2 byte + topic + QoS 1 byte)
        int remainingLength = 2 + 2 + topicBytes.Length + 1;
        subscribePacket.Add((byte)remainingLength);
        
        // Packet ID (1)
        subscribePacket.Add(0x00);
        subscribePacket.Add(0x01);
        
        // Topic uzunluğu
        subscribePacket.Add((byte)(topicBytes.Length >> 8));
        subscribePacket.Add((byte)(topicBytes.Length & 0xFF));
        
        // Topic
        subscribePacket.AddRange(topicBytes);
        
        // QoS (0)
        subscribePacket.Add(0x00);
        
        stream.Write(subscribePacket.ToArray(), 0, subscribePacket.Count);
        Debug.Log($"📡 Abone olundu: {topic}");
    }
    
    void ListenForMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ProcessMQTTMessage(buffer, bytesRead);
                    }
                }
                Thread.Sleep(10);
            }
            catch (Exception e)
            {
                Debug.LogError($"MQTT dinleme hatası: {e.Message}");
                break;
            }
        }
    }
    
    void ProcessMQTTMessage(byte[] data, int length)
    {
        try
        {
            // MQTT PUBLISH paketini parse et
            string payload = "";
            
            // Paket tipini kontrol et (PUBLISH = 0x30)
            if ((data[0] & 0xF0) == 0x30)
            {
                // Topic ve payload'ı ayır
                int index = 2; // Fixed header'dan sonra
                
                // Topic uzunluğunu oku
                int topicLen = (data[index] << 8) | data[index + 1];
                index += 2;
                
                // Topic'i atla
                index += topicLen;
                
                // Payload'u oku
                payload = Encoding.UTF8.GetString(data, index, length - index);
                
                // JSON'u parse et
                ParseSensorData(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Mesaj işleme hatası: {e.Message}");
        }
    }
    
    void ParseSensorData(string jsonData)
    {
        // DoS saldırısı kontrolü - veri akışı kesildiyse işleme
        if (dataLossSimulated)
        {
            // Debug log'u çok fazla mesaj oluşturmasın diye sadece bazen logla
            if (Time.frameCount % 60 == 0)
                Debug.Log("[DoS] Veri akışı kesildi - UI güncellenmiyor");
            return;
        }
        
        try
        {
            // JSON'u parse et
            var data = JsonUtility.FromJson<TrafoData>(jsonData);
            
            // Son mesaj zamanını güncelle
            lastMessageTime = Time.time;
            
            // Saldırı altında değilsek normal verileri kaydet
            if (!isUnderAttack)
            {
                lastTemperature = data.sicaklik;
                lastVoltage = data.gerilim_primer / 1000f; // kV'a çevir
                lastLoad = data.yuk_seviyesi;
            }
            
            // UI'ı güncelle (ana thread'de çalıştır)
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                UpdateUI();
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON parse hatası: {e.Message}\nVeri: {jsonData}");
        }
    }
    
    void UpdateUI()
    {
        // Sıcaklık güncelle (saldırı altındaysa sahte değeri göster)
        float displayTemp = isUnderAttack && attackType == "temperature" ? attackValue : lastTemperature;
        
        if (temperatureText != null)
        {
            temperatureText.text = $"{displayTemp:F1}°C";
            if (displayTemp > 85)
            {
                temperatureText.color = Color.red;
                ShowAlarm("🔥 KRİTİK SICAKLIK!");
            }
            else if (displayTemp > 70)
            {
                temperatureText.color = Color.yellow;
            }
            else
            {
                temperatureText.color = Color.white;
            }
        }
        
        // Gerilim güncelle (saldırı altındaysa sahte değeri göster)
        float displayVoltage = isUnderAttack && attackType == "voltage" ? attackValue : lastVoltage;
        
        if (voltageText != null)
        {
            voltageText.text = $"{displayVoltage:F1} kV";
            if (displayVoltage < 150 || displayVoltage > 165)
            {
                voltageText.color = Color.red;
            }
            else
            {
                voltageText.color = Color.white;
            }
        }
        
        // Yük güncelle (saldırı altındaysa sahte değeri göster)
        float displayLoad = isUnderAttack && attackType == "load" ? attackValue : lastLoad;
        
        if (loadText != null)
        {
            loadText.text = $"%{displayLoad:F0}";
            if (displayLoad > 100)
            {
                loadText.color = Color.red;
            }
            else if (displayLoad > 90)
            {
                loadText.color = Color.yellow;
            }
            else
            {
                loadText.color = Color.white;
            }
        }
        
        // Status text güncelle (saldırı durumu)
        if (statusText != null)
        {
            if (isUnderAttack)
            {
                if (attackType == "dos")
                    statusText.text = "⚠️ DoS SALDIRISI ALTINDA!";
                else
                    statusText.text = $"⚠️ FDI SALDIRISI ALTINDA! ({attackType})";
                statusText.color = Color.red;
            }
            else
            {
                statusText.text = isConnected ? "✅ Normal Çalışıyor" : "❌ Bağlantı Yok";
                statusText.color = isConnected ? Color.green : Color.red;
            }
        }
    }
    
    void ShowAlarm(string message)
    {
        if (alarmPanel != null && !alarmPanel.activeSelf)
        {
            alarmPanel.SetActive(true);
            Text alarmText = alarmPanel.GetComponentInChildren<Text>();
            if (alarmText != null)
                alarmText.text = message;
            
            StartCoroutine(HideAlarmAfterSeconds(5));
        }
    }
    
    IEnumerator HideAlarmAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (alarmPanel != null)
            alarmPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        isRunning = false;
        if (mqttThread != null && mqttThread.IsAlive)
            mqttThread.Join(1000);
        
        if (stream != null)
            stream.Close();
        if (tcpClient != null)
            tcpClient.Close();
    }
    
    // ===== TERMINALCONTROLLER İÇİN GEREKLİ METODLAR =====
    
    /// <summary>
    /// Saldırı modunu ayarlar - TerminalController tarafından çağrılır
    /// </summary>
    public void SetAttackMode(bool active, string type, float value)
    {
        isUnderAttack = active;
        attackType = type;
        attackValue = value;
        
        if (!active)
        {
            // Saldırı bitti, normale dön
            dataLossSimulated = false;
            Debug.Log("[MQTT] ✅ Saldırı sona erdi, normale dönülüyor.");
            
            // Alarm panelini kapat
            if (alarmPanel != null)
                alarmPanel.SetActive(false);
        }
        else if (type == "dos")
        {
            // DoS saldırısı - veri akışını kes
            dataLossSimulated = true;
            Debug.Log("[MQTT] 🔴 DoS Saldırısı başladı! Veri akışı kesiliyor.");
        }
        else
        {
            // FDI saldırısı
            dataLossSimulated = false;
            string typeName = type == "temperature" ? "Sıcaklık" : (type == "voltage" ? "Gerilim" : "Yük");
            Debug.Log($"[MQTT] 🔴 FDI Saldırısı başladı! {typeName} = {value}");
        }
        
        // UI'ı hemen güncelle
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            UpdateUI();
        });
    }
    
    /// <summary>
    /// Son alınan sıcaklık değerini döndürür
    /// </summary>
    public float GetLastTemperature()
    {
        if (isUnderAttack && attackType == "temperature")
            return attackValue;
        return lastTemperature;
    }
    
    /// <summary>
    /// Son alınan gerilim değerini döndürür
    /// </summary>
    public float GetLastVoltage()
    {
        if (isUnderAttack && attackType == "voltage")
            return attackValue;
        return lastVoltage;
    }
    
    /// <summary>
    /// Son alınan yük değerini döndürür
    /// </summary>
    public float GetLastLoad()
    {
        if (isUnderAttack && attackType == "load")
            return attackValue;
        return lastLoad;
    }
    
    /// <summary>
    /// Son mesajın alındığı zamanı döndürür (Time.time cinsinden)
    /// </summary>
    public float GetLastMessageTime()
    {
        return lastMessageTime;
    }
    
    /// <summary>
    /// MQTT bağlantı durumunu döndürür
    /// </summary>
    public bool IsConnected()
    {
        return isConnected && (Time.time - lastMessageTime) < 10f;
    }
    
    /// <summary>
    /// Saldırı altında mı değil mi döndürür
    /// </summary>
    public bool IsUnderAttack()
    {
        return isUnderAttack;
    }
    
    /// <summary>
    /// Aktif saldırı tipini döndürür
    /// </summary>
    public string GetAttackType()
    {
        return attackType;
    }
}

// JSON veri sınıfı (veri.py'deki yapıyla eşleşmeli)
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

// Ana thread'de çalıştırmak için yardımcı sınıf
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private Queue<Action> _actions = new Queue<Action>();
    
    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    public void Enqueue(Action action)
    {
        lock (_actions)
        {
            _actions.Enqueue(action);
        }
    }
    
    void Update()
    {
        lock (_actions)
        {
            while (_actions.Count > 0)
            {
                _actions.Dequeue()?.Invoke();
            }
        }
    }
}