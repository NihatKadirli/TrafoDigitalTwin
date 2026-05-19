using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BreakerController : MonoBehaviour
{
    const string UnauthorizedAlert = "CRITICAL ALERT: Unauthorized Breaker Operation Detected";
    const string MaintenanceCloseAlert = "DANGER: Breaker Closed During Maintenance Mode";

    [Header("Breaker State")]
    public string breakerId = "BRK-01";
    public bool maintenanceMode;

    [Header("Existing Controllers")]
    public CircuitBreakerController circuitBreaker;
    public SCADAHMIController hmiController;
    public AlarmPanelController alarmPanelController;
    public SCADATerminalController terminalController;

    [Header("SCADA UI")]
    public TMP_Text breakerStatusText;
    public TMP_Text commandSourceText;
    public TMP_Text lastCommandTimeText;
    public TMP_Text alarmText;
    public Image alarmPanelImage;
    public GameObject alarmPanelObject;
    public Color alarmPanelAttackColor = new Color(0.26f, 0.035f, 0.045f, 0.98f);

    [Header("Visual Objects")]
    public Transform breakerVisual;
    public Vector3 closedEulerAngles = Vector3.zero;
    public Vector3 openEulerAngles = new Vector3(0f, 0f, -35f);
    public Vector3 closedLocalPosition;
    public Vector3 openLocalPosition;
    public bool usePositionFallback;
    public Renderer[] energyLineRenderers;
    public Image[] energyLineImages;
    public Color energizedColor = new Color(0.31f, 0.95f, 0.48f, 1f);
    public Color deEnergizedColor = new Color(1f, 0.24f, 0.20f, 1f);

    [Header("Optional Animation")]
    public Animator breakerAnimator;
    public string openTrigger = "OpenBreaker";
    public string closeTrigger = "CloseBreaker";

    [Header("Optional Camera Focus")]
    public Camera focusCamera;
    public Transform cameraFocusTarget;
    public Vector3 cameraFocusOffset = new Vector3(0f, 3f, -6f);

    public string LastBreakerStatus { get; private set; } = "CLOSED";
    public string LastCommandSource { get; private set; } = "operator";
    public string LastCommandTime { get; private set; } = "--";
    public bool HasActiveSecurityAlarm { get; private set; }

    void Awake()
    {
        EnsureReferences();
    }

    void Start()
    {
        if (breakerVisual != null && closedLocalPosition == Vector3.zero)
            closedLocalPosition = breakerVisual.localPosition;

        RefreshScadaFields();
        ApplyEnergyLineState(LastBreakerStatus == "CLOSED");
    }

    public void HandleBreakerCommandJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        BreakerCommandMessage message;
        try
        {
            message = JsonUtility.FromJson<BreakerCommandMessage>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BreakerController] Breaker command JSON parse failed: {ex.Message} | Payload: {json}");
            return;
        }

        HandleBreakerCommand(message);
    }

    public void HandleBreakerCommand(BreakerCommandMessage message)
    {
        EnsureReferences();

        if (message == null)
        {
            Debug.LogWarning("[BreakerController] Empty breaker command received.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.breakerId) &&
            !string.Equals(message.breakerId, breakerId, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[BreakerController] Command ignored for breakerId={message.breakerId}; expected {breakerId}.");
            return;
        }

        string command = Normalize(message.command);
        string source = string.IsNullOrWhiteSpace(message.source) ? "unknown" : message.source.Trim();
        string timestamp = string.IsNullOrWhiteSpace(message.timestamp)
            ? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            : message.timestamp.Trim();

        if (command != "OPEN" && command != "CLOSE")
        {
            Debug.LogWarning($"[BreakerController] Unsupported breaker command: {message.command}");
            return;
        }

        LastBreakerStatus = command == "OPEN" ? "OPEN" : "CLOSED";
        LastCommandSource = source;
        LastCommandTime = timestamp;

        ApplyBreakerState(command, source);
        RunIdsChecks(command, source);
        RefreshScadaFields();

        if (hmiController != null)
            hmiController.RefreshAll();
    }

    void ApplyBreakerState(string command, string source)
    {
        bool closed = command == "CLOSE";
        string reason = $"{source} MQTT {command} command";

        if (circuitBreaker != null)
        {
            if (closed)
                circuitBreaker.Close(reason);
            else
                circuitBreaker.Open(reason);
        }
        else
        {
            Debug.LogWarning("[BreakerController] CircuitBreakerController is not assigned; only fallback visuals will be updated.");
        }

        ApplyEnergyLineState(closed);
        ApplyBreakerVisual(closed);

        AppendScadaLog($"BREAKER {command} COMMAND RECEIVED");
        AppendScadaLog($"SOURCE: {source.ToUpperInvariant()}");
    }

    void RunIdsChecks(string command, string source)
    {
        HasActiveSecurityAlarm = false;

        if (string.Equals(source, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            HasActiveSecurityAlarm = true;
            RaiseCriticalAlarm(UnauthorizedAlert);
            AppendScadaLog("CRITICAL: UNAUTHORIZED SWITCHING ATTACK DETECTED");
            CameraFocus();
        }

        if (maintenanceMode && command == "CLOSE")
        {
            HasActiveSecurityAlarm = true;
            RaiseCriticalAlarm(MaintenanceCloseAlert);
            AppendScadaLog("CRITICAL: BREAKER CLOSED DURING MAINTENANCE MODE");
            CameraFocus();
        }
    }

    void RaiseCriticalAlarm(string message)
    {
        if (alarmPanelController != null)
            alarmPanelController.AddAlarm(message, AlarmPanelController.AlarmSeverity.Critical);

        if (alarmPanelObject != null)
            alarmPanelObject.SetActive(true);

        if (alarmPanelImage != null)
            alarmPanelImage.color = alarmPanelAttackColor;

        if (alarmText != null)
            alarmText.text = message;

        Debug.LogWarning($"[BreakerController][IDS] {message}");
    }

    void ApplyEnergyLineState(bool closed)
    {
        Color targetColor = closed ? energizedColor : deEnergizedColor;

        if (energyLineRenderers != null)
        {
            foreach (Renderer line in energyLineRenderers)
            {
                if (line == null)
                    continue;

                line.material.color = targetColor;
            }
        }

        if (energyLineImages != null)
        {
            foreach (Image line in energyLineImages)
            {
                if (line != null)
                    line.color = targetColor;
            }
        }
    }

    void ApplyBreakerVisual(bool closed)
    {
        if (breakerAnimator != null)
        {
            breakerAnimator.SetTrigger(closed ? closeTrigger : openTrigger);
            return;
        }

        if (breakerVisual == null)
        {
            Debug.LogWarning("[BreakerController] Breaker visual and Animator are not assigned; physical breaker motion is skipped.");
            return;
        }

        if (usePositionFallback)
            breakerVisual.localPosition = closed ? closedLocalPosition : openLocalPosition;
        else
            breakerVisual.localRotation = Quaternion.Euler(closed ? closedEulerAngles : openEulerAngles);
    }

    void RefreshScadaFields()
    {
        if (breakerStatusText != null)
        {
            breakerStatusText.text = LastBreakerStatus;
            breakerStatusText.color = LastBreakerStatus == "CLOSED" ? SCADAHMIController.NormalColor : SCADAHMIController.FaultColor;
        }

        if (commandSourceText != null)
        {
            commandSourceText.text = LastCommandSource;
            commandSourceText.color = string.Equals(LastCommandSource, "unauthorized", StringComparison.OrdinalIgnoreCase)
                ? SCADAHMIController.FaultColor
                : SCADAHMIController.NormalColor;
        }

        if (lastCommandTimeText != null)
            lastCommandTimeText.text = LastCommandTime;
    }

    void AppendScadaLog(string message)
    {
        string stamp = DateTime.Now.ToString("HH:mm:ss");

        if (terminalController != null)
            terminalController.WriteExternalLine($"[{stamp}] {message}");
        else
            Debug.Log($"[{stamp}] {message}");
    }

    public void CameraFocus()
    {
        // Cinemachine varsa burada ilgili virtual camera'nin Follow/LookAt hedefi cameraFocusTarget olarak atanabilir.
        // Proje Cinemachine paketi olmadan da derlenebilsin diye dogrudan Cinemachine tiplerine referans verilmiyor.
        Transform target = cameraFocusTarget != null ? cameraFocusTarget : breakerVisual;
        Camera cameraToMove = focusCamera != null ? focusCamera : Camera.main;

        if (target == null || cameraToMove == null)
            return;

        cameraToMove.transform.position = target.position + cameraFocusOffset;
        cameraToMove.transform.LookAt(target);
    }

    void EnsureReferences()
    {
        if (circuitBreaker == null)
            circuitBreaker = GetComponent<CircuitBreakerController>() ?? FindFirstObjectByType<CircuitBreakerController>();
        if (hmiController == null)
            hmiController = GetComponent<SCADAHMIController>() ?? FindFirstObjectByType<SCADAHMIController>();
        if (alarmPanelController == null)
            alarmPanelController = GetComponent<AlarmPanelController>() ?? FindFirstObjectByType<AlarmPanelController>();
        if (terminalController == null)
            terminalController = GetComponent<SCADATerminalController>() ?? FindFirstObjectByType<SCADATerminalController>();
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }
}

[Serializable]
public class BreakerCommandMessage
{
    public string breakerId;
    public string command;
    public string source;
    public string timestamp;
}
