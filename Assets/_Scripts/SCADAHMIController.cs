using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SCADAHMIController : MonoBehaviour
{
    [System.Serializable]
    public class ComponentStatusBinding
    {
        public string componentName;
        public TMP_Text statusText;
        public TMP_Text detailText;
        public Image statusLight;
    }

    public static readonly Color NormalColor = new Color(0.31f, 0.95f, 0.48f, 1f);
    public static readonly Color FaultColor = new Color(1f, 0.24f, 0.20f, 1f);
    public static readonly Color WarningColor = new Color(1f, 0.82f, 0.25f, 1f);
    public static readonly Color OfflineColor = new Color(0.45f, 0.50f, 0.54f, 1f);

    [Header("Controllers")]
    public IEDController iedController;
    public CircuitBreakerController circuitBreaker;
    public AlarmPanelController alarmPanel;
    public SCADATerminalController terminalController;
    public SimpleMQTTReceiver mqttReceiver;

    [Header("Mimic Component Status")]
    public List<ComponentStatusBinding> componentBindings = new List<ComponentStatusBinding>();

    [Header("IED Panel")]
    public TMP_Text iedCurrentText;
    public TMP_Text iedThresholdText;
    public TMP_Text iedTripStatusText;
    public TMP_Text iedStNumText;
    public TMP_Text iedSqNumText;
    public TMP_Text iedLastGooseText;
    public TMP_Text iedAttackText;

    [Header("Breaker Panel")]
    public TMP_Text breakerStateText;
    public TMP_Text breakerDetailText;
    public Image breakerStateIndicator;

    [Header("Header")]
    public TMP_Text systemModeText;
    public TMP_Text protocolText;
    public TMP_Text clockText;

    [Header("Settings")]
    public float telemetryRefreshSeconds = 1f;
    public bool useMqttCurrentWhenNormal = true;

    private float nextTelemetryRefresh;
    private int lastClockSecond = -1;

    void Awake()
    {
        EnsureReferences();
    }

    void Start()
    {
        WireControllerEvents();
        RefreshAll();
    }

    void Update()
    {
        if (clockText != null)
        {
            System.DateTime now = System.DateTime.Now;
            if (now.Second != lastClockSecond)
            {
                lastClockSecond = now.Second;
                clockText.text = now.ToString("HH:mm:ss");
            }
        }

        if (Time.time >= nextTelemetryRefresh)
        {
            nextTelemetryRefresh = Time.time + telemetryRefreshSeconds;
            UpdateCurrentFromTelemetry();
            RefreshAll();
        }
    }

    public void EnsureReferences()
    {
        if (iedController == null)
            iedController = GetComponent<IEDController>() ?? FindFirstObjectByType<IEDController>();
        if (circuitBreaker == null)
            circuitBreaker = GetComponent<CircuitBreakerController>() ?? FindFirstObjectByType<CircuitBreakerController>();
        if (alarmPanel == null)
            alarmPanel = GetComponent<AlarmPanelController>() ?? FindFirstObjectByType<AlarmPanelController>();
        if (terminalController == null)
            terminalController = GetComponent<SCADATerminalController>() ?? FindFirstObjectByType<SCADATerminalController>();
        if (mqttReceiver == null)
            mqttReceiver = FindFirstObjectByType<SimpleMQTTReceiver>();
    }

    public void SetFault(bool active)
    {
        EnsureReferences();

        if (active)
        {
            iedController.FaultOn(false);
            circuitBreaker.Trip("GOOSE Trip sent by IED");
            alarmPanel.AddAlarm("Fault detected", AlarmPanelController.AlarmSeverity.Critical);
            alarmPanel.AddAlarm("GOOSE Trip sent", AlarmPanelController.AlarmSeverity.Critical);
            alarmPanel.AddAlarm("Breaker opened", AlarmPanelController.AlarmSeverity.Critical);
        }
        else
        {
            iedController.FaultOff();
            alarmPanel.AddAlarm("Fault cleared", AlarmPanelController.AlarmSeverity.Info);
        }

        RefreshAll();
    }

    public void ManualTrip()
    {
        EnsureReferences();
        iedController.ManualTrip();
        circuitBreaker.Trip("Manual SCADA trip command");
        alarmPanel.AddAlarm("GOOSE Trip sent", AlarmPanelController.AlarmSeverity.Critical);
        alarmPanel.AddAlarm("Breaker opened", AlarmPanelController.AlarmSeverity.Critical);
        RefreshAll();
    }

    public void ResetSystem()
    {
        EnsureReferences();
        iedController.ResetProtection();
        circuitBreaker.Close("Reset command; breaker closed");
        alarmPanel.ClearAlarms();
        alarmPanel.AddAlarm("System reset to normal", AlarmPanelController.AlarmSeverity.Info);
        RefreshAll();
    }

    public void SimulateGooseDataAttack()
    {
        EnsureReferences();
        iedController.SimulateGooseDataAttack();
        circuitBreaker.Trip("Spoofed GOOSE data opened breaker");
        alarmPanel.AddAlarm("Attack detected", AlarmPanelController.AlarmSeverity.Critical);
        alarmPanel.AddAlarm("Fake GOOSE Trip sent", AlarmPanelController.AlarmSeverity.Critical);
        alarmPanel.AddAlarm("Breaker opened", AlarmPanelController.AlarmSeverity.Critical);
        RefreshAll();
    }

    public void SimulateGooseDosAttack()
    {
        EnsureReferences();
        iedController.SimulateGooseDos();
        circuitBreaker.Close("GOOSE blocked; breaker did not receive trip");
        alarmPanel.AddAlarm("Fault detected", AlarmPanelController.AlarmSeverity.Critical);
        alarmPanel.AddAlarm("GOOSE communication blocked", AlarmPanelController.AlarmSeverity.Critical);
        alarmPanel.AddAlarm("Attack detected", AlarmPanelController.AlarmSeverity.Critical);
        RefreshAll();
    }

    public IEnumerable<string> GetStatusLines()
    {
        EnsureReferences();

        yield return "<color=#0FD19E><b>SCADA/HMI STATUS</b></color>";
        yield return "----------------------------------------------------------------";
        yield return $"Transformer        : {GetComponentStatus("Transformer")}";
        yield return $"Busbar             : {GetComponentStatus("Busbar")}";
        yield return $"Circuit Breaker    : {circuitBreaker.GetStateLabel()}";
        yield return $"IED Protection     : {iedController.GetProtectionStateLabel()}";
        yield return $"CT / Current Sensor: {GetComponentStatus("CT / Current Sensor")}";
        yield return $"Network Switch     : {GetComponentStatus("Network Switch")}";
        yield return $"SCADA Server       : {GetComponentStatus("SCADA Server")}";
        yield return $"Current            : {iedController.currentAmpere:F0} A";
        yield return $"Trip Threshold     : {iedController.tripThreshold:F0} A";
        yield return $"GOOSE stNum/sqNum  : {iedController.gooseStNum}/{iedController.gooseSqNum}";
        yield return $"Last GOOSE         : {iedController.lastGooseMessage}";
        yield return "";
    }

    public void RefreshAll()
    {
        EnsureReferences();

        if (iedController != null)
            iedController.RefreshUI();
        if (circuitBreaker != null)
            circuitBreaker.RefreshUI();
        if (alarmPanel != null)
            alarmPanel.RefreshUI();

        RefreshPanelTexts();
        RefreshComponentBindings();
        RefreshHeader();
    }

    void WireControllerEvents()
    {
        if (iedController != null)
            iedController.OnStateChanged += RefreshAll;
        if (circuitBreaker != null)
            circuitBreaker.OnStateChanged += RefreshAll;
    }

    void UpdateCurrentFromTelemetry()
    {
        if (!useMqttCurrentWhenNormal || mqttReceiver == null || iedController == null)
            return;

        if (iedController.faultDetected || iedController.attackDetected || iedController.tripStatus)
            return;

        float primaryCurrent = mqttReceiver.GetLastPrimaryCurrent();
        if (primaryCurrent > 1f)
            iedController.SetMeasuredCurrent(primaryCurrent);
    }

    void RefreshPanelTexts()
    {
        if (iedController != null)
        {
            if (iedCurrentText != null)
                iedCurrentText.text = $"{iedController.currentAmpere:F0} A";
            if (iedThresholdText != null)
                iedThresholdText.text = $"{iedController.tripThreshold:F0} A";
            if (iedTripStatusText != null)
                iedTripStatusText.text = iedController.tripStatus ? "TRIP ACTIVE" : "NO TRIP";
            if (iedStNumText != null)
                iedStNumText.text = iedController.gooseStNum.ToString();
            if (iedSqNumText != null)
                iedSqNumText.text = iedController.gooseSqNum.ToString();
            if (iedLastGooseText != null)
                iedLastGooseText.text = iedController.lastGooseMessage;
            if (iedAttackText != null)
                iedAttackText.text = iedController.attackDetected ? "ATTACK DETECTED" : "No attack";
        }

        if (circuitBreaker != null)
        {
            if (breakerStateText != null)
                breakerStateText.text = circuitBreaker.GetStateLabel();
            if (breakerDetailText != null)
                breakerDetailText.text = circuitBreaker.lastOperation;
            if (breakerStateIndicator != null)
                breakerStateIndicator.color = circuitBreaker.GetStateColor();
        }
    }

    void RefreshComponentBindings()
    {
        foreach (ComponentStatusBinding binding in componentBindings)
        {
            if (binding == null)
                continue;

            string status = GetComponentStatus(binding.componentName);
            Color color = GetComponentColor(binding.componentName);

            if (binding.statusText != null)
                binding.statusText.text = status;
            if (binding.statusLight != null)
                binding.statusLight.color = color;
        }
    }

    void RefreshHeader()
    {
        if (systemModeText != null)
        {
            if (iedController != null && iedController.attackDetected)
                systemModeText.text = "CYBER SECURITY EVENT";
            else if (iedController != null && (iedController.faultDetected || iedController.tripStatus))
                systemModeText.text = "PROTECTION EVENT";
            else
                systemModeText.text = "NORMAL OPERATION";
        }

        if (protocolText != null)
            protocolText.text = "IEC 61850 MMS monitored | GOOSE protection event simulation";
    }

    string GetComponentStatus(string componentName)
    {
        if (iedController == null || circuitBreaker == null)
            return "UNKNOWN";

        switch (componentName)
        {
            case "Transformer":
                return iedController.faultDetected ? "FAULT" : "NORMAL";
            case "Busbar":
                return iedController.faultDetected ? "FAULT" : "NORMAL";
            case "Circuit Breaker":
                return circuitBreaker.GetStateLabel();
            case "IED Protection Relay":
                return iedController.GetProtectionStateLabel();
            case "CT / Current Sensor":
                return iedController.attackDetected ? "SUSPECT" :
                       iedController.faultDetected ? "FAULT" : "NORMAL";
            case "Network Switch":
                return iedController.gooseBlocked ? "OFFLINE" : "ONLINE";
            case "SCADA Server":
                return "ONLINE";
            default:
                return "NORMAL";
        }
    }

    Color GetComponentColor(string componentName)
    {
        string status = GetComponentStatus(componentName);
        switch (status)
        {
            case "FAULT":
            case "TRIP":
            case "OPEN":
                return FaultColor;
            case "SUSPECT":
            case "GOOSE BLOCKED":
            case "ATTACK DETECTED":
                return WarningColor;
            case "OFFLINE":
                return OfflineColor;
            default:
                return NormalColor;
        }
    }
}
