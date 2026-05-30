using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoolingFalseDataReceiver : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;
    public string subscriptionTopic = "substation/#";
    public string clientId = "UnityCoolingFalseData";
    public bool logReceivedPayloads = true;

    [Header("Fan Visuals")]
    public Transform[] fanObjects;
    public MonoBehaviour[] fanRotationScripts;
    public bool rotateFansWhenCoolingOn = true;
    public Vector3 fanRotationAxis = Vector3.forward;
    public float fanRotationDegreesPerSecond = 360f;

    [Header("Transformer Heat Visuals")]
    public Renderer[] transformerRenderers;
    public Material transformerMaterial;
    public Color normalColor = Color.white;
    public Color overheatedColor = new Color(1f, 0.28f, 0.04f, 1f);
    public Color normalEmissionColor = Color.black;
    public Color overheatedEmissionColor = new Color(1f, 0.22f, 0.02f, 1f);
    public float criticalTemperature = 80f;

    [Header("Smoke Effect")]
    public ParticleSystem smokeParticle;
    public GameObject smokeObject;

    [Header("SCADA UI")]
    public TMP_Text scadaTemperatureText;
    public TMP_Text realTemperatureLogText;
    public TMP_Text attackLogText;
    public TMP_Text securityLogText;
    public Image alarmPanelImage;
    public Color alarmSuppressedColor = new Color(0.31f, 0.95f, 0.48f, 1f);

    [Header("Controllers")]
    public AlarmPanelController alarmPanelController;
    public SCADATerminalController terminalController;

    TcpClient tcpClient;
    NetworkStream stream;
    Thread mqttThread;
    volatile bool isRunning;
    volatile bool isConnected;
    SynchronizationContext unityContext;

    readonly Queue<MqttMessage> pendingMessages = new Queue<MqttMessage>();
    readonly object pendingMessageLock = new object();
    readonly object streamLock = new object();
    readonly Dictionary<Renderer, Color> originalRendererColors = new Dictionary<Renderer, Color>();
    readonly Dictionary<Renderer, Color> originalRendererEmissionColors = new Dictionary<Renderer, Color>();

    bool coolingOn = true;
    bool smokeOn;
    bool alarmSuppressionOn;
    bool falseDataInjectionActive;
    float fakeTemperature = 42f;
    float realTemperature = 45f;

    public bool IsCoolingOn => coolingOn;
    public bool IsAlarmSuppressionActive => alarmSuppressionOn;
    public bool IsFalseDataInjectionActive => falseDataInjectionActive;
    public float FakeTemperature => fakeTemperature;
    public float RealTemperature => realTemperature;

    void Start()
    {
        unityContext = SynchronizationContext.Current;
        EnsureReferences();
        CaptureOriginalMaterialState();
        ApplyNormalState();
        Connect();
    }

    void Update()
    {
        DrainPendingMessages();

        if (coolingOn && rotateFansWhenCoolingOn && fanObjects != null)
        {
            foreach (Transform fan in fanObjects)
            {
                if (fan != null)
                    fan.Rotate(fanRotationAxis, fanRotationDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }
    }

    public bool IsConnected()
    {
        return isConnected && tcpClient != null && tcpClient.Connected;
    }

    public void PublishStartAttack()
    {
        Publish("substation/attack/type", "false_temperature_injection");
        Publish("substation/attack/temperature/set", "START");
    }

    public void PublishStopAttack()
    {
        Publish("substation/attack/temperature/set", "STOP");
        Publish("substation/attack/type", "none");
    }

    public void HandleMessage(string topic, string payload)
    {
        string value = payload == null ? string.Empty : payload.Trim();

        switch (topic)
        {
            case "substation/attack/type":
                falseDataInjectionActive =
                    string.Equals(value, "cooling_false_data", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "false_temperature_injection", StringComparison.OrdinalIgnoreCase);
                AppendLog(falseDataInjectionActive ? "False Data Injection Active" : "Cooling false data attack cleared");
                break;

            case "substation/attack/temperature/set":
                falseDataInjectionActive =
                    string.Equals(value, "START", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                AppendLog(falseDataInjectionActive ? "False Temperature Injection command sent" : "False Temperature Injection stop command sent");
                break;

            case "substation/cooling/control":
                coolingOn = !string.Equals(value, "OFF", StringComparison.OrdinalIgnoreCase);
                ApplyCoolingState();
                break;

            case "substation/sensor/temperature/fake":
            case "substation/transformer/displayed_temperature":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fake))
                    fakeTemperature = fake;
                ApplyTemperatureText();
                break;

            case "substation/transformer/temperature/real":
            case "substation/transformer/real_temperature":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float real))
                    realTemperature = real;
                ApplyHeatState();
                ApplyTemperatureText();
                AppendLog($"Real transformer temperature: {realTemperature:F0} C");
                break;

            case "substation/effect/smoke":
                smokeOn = string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                ApplySmokeState();
                break;

            case "substation/alarm/suppression":
                alarmSuppressionOn = string.Equals(value, "ON", StringComparison.OrdinalIgnoreCase);
                ApplyAlarmSuppressionState();
                break;
        }
    }

    void EnsureReferences()
    {
        if (alarmPanelController == null)
            alarmPanelController = GetComponent<AlarmPanelController>() ?? FindFirstObjectByType<AlarmPanelController>();
        if (terminalController == null)
            terminalController = GetComponent<SCADATerminalController>() ?? FindFirstObjectByType<SCADATerminalController>();
    }

    void Connect()
    {
        try
        {
            tcpClient = new TcpClient(brokerAddress, brokerPort);
            stream = tcpClient.GetStream();

            SendConnectPacket();
            if (!WaitForConnAck())
            {
                Debug.LogWarning("[CoolingFalseDataReceiver] MQTT CONNACK was not received. Cooling attack subscription was not started.");
                return;
            }

            SendSubscribePacket(subscriptionTopic);
            WaitForSubAck();

            isRunning = true;
            mqttThread = new Thread(ListenForMessages);
            mqttThread.IsBackground = true;
            mqttThread.Start();

            isConnected = true;
            Debug.Log($"[CoolingFalseDataReceiver] Connected to MQTT broker {brokerAddress}:{brokerPort}, subscribed to {subscriptionTopic}");
        }
        catch (Exception ex)
        {
            isConnected = false;
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT connection failed. Check broker {brokerAddress}:{brokerPort}. Error: {ex.Message}");
        }
    }

    void SendConnectPacket()
    {
        byte[] protocolName = Encoding.ASCII.GetBytes("MQTT");
        byte[] clientBytes = Encoding.ASCII.GetBytes(clientId);

        List<byte> body = new List<byte>();
        body.Add(0x00);
        body.Add((byte)protocolName.Length);
        body.AddRange(protocolName);
        body.Add(0x04);
        body.Add(0x02);
        body.Add(0x00);
        body.Add(0x3C);
        body.Add((byte)(clientBytes.Length >> 8));
        body.Add((byte)(clientBytes.Length & 0xFF));
        body.AddRange(clientBytes);

        List<byte> packet = new List<byte>();
        packet.Add(0x10);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        WritePacket(packet);
    }

    void SendSubscribePacket(string topic)
    {
        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);

        List<byte> body = new List<byte>();
        body.Add(0x00);
        body.Add(0x01);
        body.Add((byte)(topicBytes.Length >> 8));
        body.Add((byte)(topicBytes.Length & 0xFF));
        body.AddRange(topicBytes);
        body.Add(0x00);

        List<byte> packet = new List<byte>();
        packet.Add(0x82);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        WritePacket(packet);
    }

    void Publish(string topic, string payload)
    {
        if (!IsConnected() || stream == null)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT publish skipped; broker is not connected. Topic={topic}, Payload={payload}");
            return;
        }

        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        List<byte> body = new List<byte>();
        body.Add((byte)(topicBytes.Length >> 8));
        body.Add((byte)(topicBytes.Length & 0xFF));
        body.AddRange(topicBytes);
        body.AddRange(payloadBytes);

        List<byte> packet = new List<byte>();
        packet.Add(0x30);
        packet.AddRange(EncodeRemainingLength(body.Count));
        packet.AddRange(body);

        try
        {
            WritePacket(packet);
            Debug.Log($"[CoolingFalseDataReceiver] Published {topic}: {payload}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT publish failed for {topic}: {ex.Message}");
        }
    }

    void ListenForMessages()
    {
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                MqttPacket packet = ReadPacket();
                if (packet != null)
                    ProcessMqttPacket(packet);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT listen stopped: {ex.Message}");
                break;
            }
        }

        isConnected = false;
    }

    bool WaitForConnAck()
    {
        MqttPacket packet = ReadPacket();
        return packet != null && (packet.packetType & 0xF0) == 0x20 && packet.payload.Length >= 2 && packet.payload[1] == 0x00;
    }

    void WaitForSubAck()
    {
        MqttPacket packet = ReadPacket();
        if (packet == null || (packet.packetType & 0xF0) != 0x90)
            Debug.LogWarning("[CoolingFalseDataReceiver] SUBACK was not received; continuing to listen anyway.");
    }

    MqttPacket ReadPacket()
    {
        if (stream == null)
            return null;

        int firstByte = stream.ReadByte();
        if (firstByte < 0)
            return null;

        int multiplier = 1;
        int remainingLength = 0;
        int loops = 0;
        byte encodedByte;

        do
        {
            int raw = stream.ReadByte();
            if (raw < 0)
                return null;

            encodedByte = (byte)raw;
            remainingLength += (encodedByte & 127) * multiplier;
            multiplier *= 128;
            loops++;
        }
        while ((encodedByte & 128) != 0 && loops < 4);

        byte[] payload = new byte[remainingLength];
        int offset = 0;
        while (offset < remainingLength)
        {
            int read = stream.Read(payload, offset, remainingLength - offset);
            if (read <= 0)
                return null;
            offset += read;
        }

        return new MqttPacket { packetType = (byte)firstByte, payload = payload };
    }

    void ProcessMqttPacket(MqttPacket packet)
    {
        try
        {
            if ((packet.packetType & 0xF0) != 0x30 || packet.payload.Length < 2)
                return;

            int index = 0;
            int topicLength = (packet.payload[index] << 8) | packet.payload[index + 1];
            index += 2;

            if (index + topicLength > packet.payload.Length)
                return;

            string topic = Encoding.UTF8.GetString(packet.payload, index, topicLength);
            index += topicLength;
            string payload = Encoding.UTF8.GetString(packet.payload, index, packet.payload.Length - index);

            if (!IsCoolingAttackTopic(topic))
                return;

            if (logReceivedPayloads)
                Debug.Log($"[CoolingFalseDataReceiver] Received on {topic}: {payload}");

            if (unityContext != null)
                unityContext.Post(_ => HandleMessage(topic, payload), null);
            else
                EnqueueMessage(topic, payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CoolingFalseDataReceiver] MQTT packet parse failed: {ex.Message}");
        }
    }

    bool IsCoolingAttackTopic(string topic)
    {
        return topic == "substation/attack/type" ||
               topic == "substation/attack/temperature/set" ||
               topic == "substation/cooling/control" ||
               topic == "substation/sensor/temperature/fake" ||
               topic == "substation/transformer/temperature/real" ||
               topic == "substation/transformer/displayed_temperature" ||
               topic == "substation/transformer/real_temperature" ||
               topic == "substation/effect/smoke" ||
               topic == "substation/alarm/suppression";
    }

    void EnqueueMessage(string topic, string payload)
    {
        lock (pendingMessageLock)
        {
            pendingMessages.Enqueue(new MqttMessage { topic = topic, payload = payload });
        }
    }

    void DrainPendingMessages()
    {
        while (true)
        {
            MqttMessage message = null;
            lock (pendingMessageLock)
            {
                if (pendingMessages.Count > 0)
                    message = pendingMessages.Dequeue();
            }

            if (message == null)
                break;

            HandleMessage(message.topic, message.payload);
        }
    }

    void ApplyCoolingState()
    {
        if (fanRotationScripts != null)
        {
            foreach (MonoBehaviour script in fanRotationScripts)
            {
                if (script != null)
                    script.enabled = coolingOn;
            }
        }

        AppendLog(coolingOn ? "Cooling system restored" : "Cooling system disabled");
    }

    void ApplyHeatState()
    {
        bool overheated = realTemperature > criticalTemperature;
        Color color = overheated ? overheatedColor : normalColor;
        Color emission = overheated ? overheatedEmissionColor : normalEmissionColor;

        if (transformerMaterial != null)
            ApplyMaterialHeat(transformerMaterial, color, emission);

        if (transformerRenderers != null)
        {
            foreach (Renderer targetRenderer in transformerRenderers)
            {
                if (targetRenderer == null)
                    continue;

                Material material = targetRenderer.material;
                ApplyMaterialHeat(material, color, emission);
            }
        }

        if (overheated)
        {
            AppendLog(alarmSuppressionOn ? "Critical transformer temperature alarm suppressed" : "Critical transformer temperature alarm active");
            AppendSecurityLog("Data Integrity Attack Detected");
        }
    }

    void ApplyMaterialHeat(Material material, Color color, Color emission)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
        {
            if (emission.maxColorComponent > 0f)
                material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }

    void ApplySmokeState()
    {
        if (smokeObject != null)
            smokeObject.SetActive(smokeOn);

        if (smokeParticle == null)
            return;

        if (smokeOn)
            smokeParticle.Play();
        else
            smokeParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void ApplyAlarmSuppressionState()
    {
        if (alarmSuppressionOn)
        {
            AppendLog("Alarm Suppression Active");
            AppendLog("False Data Injection Active");
            AppendSecurityLog("Data Integrity Attack Detected");

            if (alarmPanelImage != null)
                alarmPanelImage.color = alarmSuppressedColor;
        }
        else
        {
            AppendLog("Alarm suppression cleared");
        }
    }

    void ApplyTemperatureText()
    {
        if (scadaTemperatureText != null)
            scadaTemperatureText.text = $"{fakeTemperature:F0} C";
        if (realTemperatureLogText != null)
            realTemperatureLogText.text = $"Real transformer temperature: {realTemperature:F0} C";
    }

    void ApplyNormalState()
    {
        coolingOn = true;
        smokeOn = false;
        alarmSuppressionOn = false;
        falseDataInjectionActive = false;
        fakeTemperature = 42f;
        realTemperature = 45f;

        ApplyCoolingState();
        RestoreOriginalMaterialState();
        ApplySmokeState();
        ApplyTemperatureText();
    }

    void CaptureOriginalMaterialState()
    {
        if (transformerRenderers == null)
            return;

        foreach (Renderer targetRenderer in transformerRenderers)
        {
            if (targetRenderer == null || originalRendererColors.ContainsKey(targetRenderer))
                continue;

            Material material = targetRenderer.material;
            originalRendererColors[targetRenderer] = GetMaterialColor(material, normalColor);
            originalRendererEmissionColors[targetRenderer] = GetMaterialEmission(material, normalEmissionColor);
        }
    }

    void RestoreOriginalMaterialState()
    {
        foreach (KeyValuePair<Renderer, Color> entry in originalRendererColors)
        {
            if (entry.Key == null)
                continue;

            Material material = entry.Key.material;
            Color emission = originalRendererEmissionColors.ContainsKey(entry.Key)
                ? originalRendererEmissionColors[entry.Key]
                : normalEmissionColor;
            ApplyMaterialHeat(material, entry.Value, emission);
        }

        if (transformerMaterial != null)
            ApplyMaterialHeat(transformerMaterial, normalColor, normalEmissionColor);
    }

    Color GetMaterialColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return fallback;
    }

    Color GetMaterialEmission(Material material, Color fallback)
    {
        if (material != null && material.HasProperty("_EmissionColor"))
            return material.GetColor("_EmissionColor");
        return fallback;
    }

    void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (attackLogText != null)
            attackLogText.text = line;
        if (terminalController != null)
            terminalController.WriteExternalLine(line);
        Debug.Log($"[CoolingFalseDataReceiver] {message}");
    }

    void AppendSecurityLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (securityLogText != null)
            securityLogText.text = line;
        if (terminalController != null)
            terminalController.WriteExternalLine(line);
        Debug.LogWarning($"[CoolingFalseDataReceiver][IDS] {message}");
    }

    void WritePacket(List<byte> packet)
    {
        lock (streamLock)
        {
            if (stream != null)
                stream.Write(packet.ToArray(), 0, packet.Count);
        }
    }

    static List<byte> EncodeRemainingLength(int length)
    {
        List<byte> encoded = new List<byte>();
        do
        {
            byte digit = (byte)(length % 128);
            length /= 128;
            if (length > 0)
                digit |= 128;
            encoded.Add(digit);
        }
        while (length > 0);

        return encoded;
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

    class MqttPacket
    {
        public byte packetType;
        public byte[] payload;
    }

    class MqttMessage
    {
        public string topic;
        public string payload;
    }
}
