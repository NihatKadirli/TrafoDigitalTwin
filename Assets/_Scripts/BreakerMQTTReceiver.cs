using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class BreakerMQTTReceiver : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerAddress = "localhost";
    public int brokerPort = 1883;
    public string topic = "substation/breaker/control";
    public string clientId = "UnityBreakerControl";
    public bool logReceivedPayloads = true;

    [Header("Target")]
    public BreakerController breakerController;

    TcpClient tcpClient;
    NetworkStream stream;
    Thread mqttThread;
    volatile bool isRunning;
    volatile bool isConnected;
    UnityMainThreadDispatcher mainThreadDispatcher;
    SynchronizationContext unityContext;

    readonly Queue<string> pendingPayloads = new Queue<string>();
    readonly object pendingPayloadLock = new object();

    void Start()
    {
        if (breakerController == null)
            breakerController = GetComponent<BreakerController>() ?? FindFirstObjectByType<BreakerController>();

        unityContext = SynchronizationContext.Current;
        mainThreadDispatcher = UnityMainThreadDispatcher.Instance();
        Connect();
    }

    void Update()
    {
        while (true)
        {
            string payload = null;
            lock (pendingPayloadLock)
            {
                if (pendingPayloads.Count > 0)
                    payload = pendingPayloads.Dequeue();
            }

            if (payload == null)
                break;

            if (breakerController != null)
                breakerController.HandleBreakerCommandJson(payload);
            else
                Debug.LogWarning($"[BreakerMQTTReceiver] BreakerController is not assigned. Payload ignored: {payload}");
        }
    }

    public bool IsConnected()
    {
        return isConnected && tcpClient != null && tcpClient.Connected;
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
                Debug.LogWarning("[BreakerMQTTReceiver] MQTT CONNACK was not received. Breaker control subscription was not started.");
                return;
            }

            SendSubscribePacket();
            WaitForSubAck();

            isRunning = true;
            mqttThread = new Thread(ListenForMessages);
            mqttThread.IsBackground = true;
            mqttThread.Start();

            isConnected = true;
            Debug.Log($"[BreakerMQTTReceiver] Connected to MQTT broker {brokerAddress}:{brokerPort}, subscribed to {topic}");
        }
        catch (Exception ex)
        {
            isConnected = false;
            Debug.LogWarning($"[BreakerMQTTReceiver] MQTT connection failed: {ex.Message}");
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

        stream.Write(packet.ToArray(), 0, packet.Count);
    }

    void SendSubscribePacket()
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

        stream.Write(packet.ToArray(), 0, packet.Count);
    }

    void ListenForMessages()
    {
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                MqttPacket packet = ReadPacket();
                if (packet != null)
                    ProcessMQTTPacket(packet);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BreakerMQTTReceiver] MQTT listen stopped: {ex.Message}");
                break;
            }
        }

        isConnected = false;
    }

    bool WaitForConnAck()
    {
        MqttPacket packet = ReadPacket();
        if (packet == null || (packet.packetType & 0xF0) != 0x20 || packet.payload.Length < 2)
            return false;

        return packet.payload[1] == 0x00;
    }

    void WaitForSubAck()
    {
        MqttPacket packet = ReadPacket();
        if (packet == null || (packet.packetType & 0xF0) != 0x90)
            Debug.LogWarning("[BreakerMQTTReceiver] SUBACK was not received; continuing to listen anyway.");
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

    void ProcessMQTTPacket(MqttPacket packet)
    {
        try
        {
            byte[] data = packet.payload;
            int length = data.Length;

            if ((packet.packetType & 0xF0) != 0x30 || length < 2)
                return;

            int index = 0;

            if (index + 2 > length)
                return;

            int topicLength = (data[index] << 8) | data[index + 1];
            index += 2;

            if (index + topicLength > length)
                return;

            string receivedTopic = Encoding.UTF8.GetString(data, index, topicLength);
            index += topicLength;

            if (!string.Equals(receivedTopic, topic, StringComparison.Ordinal))
                return;

            string payload = Encoding.UTF8.GetString(data, index, length - index);

            if (logReceivedPayloads)
                Debug.Log($"[BreakerMQTTReceiver] Received on {receivedTopic}: {payload}");

            if (unityContext != null)
            {
                unityContext.Post(_ => DispatchPayload(payload), null);
                return;
            }

            if (mainThreadDispatcher != null)
            {
                mainThreadDispatcher.Enqueue(() => DispatchPayload(payload));
                return;
            }

            lock (pendingPayloadLock)
            {
                pendingPayloads.Enqueue(payload);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BreakerMQTTReceiver] MQTT packet parse failed: {ex.Message}");
        }
    }

    void DispatchPayload(string payload)
    {
        if (breakerController != null)
            breakerController.HandleBreakerCommandJson(payload);
        else
            Debug.LogWarning($"[BreakerMQTTReceiver] BreakerController is not assigned. Payload ignored: {payload}");
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
}
