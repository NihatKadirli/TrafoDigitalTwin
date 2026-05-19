using System;
using System.Collections;
using System.Threading;
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
    public TerminalPencereYonetici terminalWindowManager;

    [Header("SCADA UI")]
    public TMP_Text breakerStatusText;
    public TMP_Text commandSourceText;
    public TMP_Text lastCommandTimeText;
    public TMP_Text alarmText;
    public Image alarmPanelImage;
    public GameObject alarmPanelObject;
    public Color alarmPanelAttackColor = new Color(0.26f, 0.035f, 0.045f, 0.98f);

    [Header("Visual Objects")]
    public string breakerRootObjectName = "circuit_breaker";
    public string breakerSwitchObjectName = "SigortaSalter";
    public Transform breakerVisual;
    public Transform breakerSwitchPivot;
    public bool autoCreateSwitchPivot = false;
    public bool autoComputeSwitchHinge = true;
    public Vector3 switchOpenAxis = Vector3.forward;
    public Vector3 switchLocalOpenAxis = Vector3.right;
    public float switchOpenAngle = 42f;
    public float switchAnimationSeconds = 1.0f;
    public bool useSwitchWorldSlideMotion = true;
    public Vector3 switchClosedWorldOffset = new Vector3(0f, 0.05f, 0f);
    public Vector3 switchOpenWorldOffset = new Vector3(0f, -0.05f, 0f);
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
    public Transform cameraFocusViewpoint;
    public string safeFocusReferenceObjectName = "FPS_Player";
    public string fallbackFocusReferenceObjectName = "vCam_ScadaOdasi";
    public Vector3 cameraFocusOffset = new Vector3(-1.25f, 0.65f, -1.65f);
    public float breakerFocusDistance = 0.42f;
    public float breakerFocusHeight = 0.18f;
    public float breakerFocusFieldOfView = 55f;
    public float breakerFocusHoldSeconds = 3f;
    public bool focusCameraOnEveryBreakerOperation = true;
    public bool closeTerminalOnBreakerOperation = true;
    public bool disableCinemachineBrainDuringBreakerFocus = true;

    public string LastBreakerStatus { get; private set; } = "CLOSED";
    public string LastCommandSource { get; private set; } = "operator";
    public string LastCommandTime { get; private set; } = "--";
    public bool HasActiveSecurityAlarm { get; private set; }

    Quaternion closedPivotRotation;
    Quaternion closedPivotWorldRotation;
    Quaternion closedSwitchLocalRotation;
    Vector3 closedSwitchLocalPosition;
    Vector3 closedSwitchWorldPosition;
    Vector3 computedSwitchOpenAxisWorld = Vector3.forward;
    Coroutine switchAnimation;
    bool focusSequenceActive;
    MonoBehaviour animationHost;
    SynchronizationContext unityContext;
    UnityMainThreadDispatcher mainThreadDispatcher;
    Timer focusRestoreTimer;

    void Awake()
    {
        unityContext = SynchronizationContext.Current;
        EnsureReferences();
    }

    void Start()
    {
        unityContext = SynchronizationContext.Current;
        mainThreadDispatcher = UnityMainThreadDispatcher.Instance();
        ResolveSceneBreakerModel();

        if (breakerVisual != null && closedLocalPosition == Vector3.zero)
            closedLocalPosition = breakerVisual.localPosition;
        if (breakerVisual != null)
        {
            closedSwitchLocalRotation = breakerVisual.localRotation;
            closedSwitchLocalPosition = breakerVisual.localPosition;
            closedSwitchWorldPosition = breakerVisual.position;

            if (useSwitchWorldSlideMotion)
                SetSwitchVisualImmediate(true);
        }
        if (breakerSwitchPivot != null)
        {
            closedPivotRotation = breakerSwitchPivot.localRotation;
            closedPivotWorldRotation = breakerSwitchPivot.rotation;
        }

        RefreshScadaFields();
        ApplyEnergyLineState(LastBreakerStatus == "CLOSED");
    }

    void OnDestroy()
    {
        focusRestoreTimer?.Dispose();
        focusRestoreTimer = null;
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
        CloseTerminalForFieldOperation();
        if (focusCameraOnEveryBreakerOperation)
            StartBreakerFocusSequence();

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
        }

        if (maintenanceMode && command == "CLOSE")
        {
            HasActiveSecurityAlarm = true;
            RaiseCriticalAlarm(MaintenanceCloseAlert);
            AppendScadaLog("CRITICAL: BREAKER CLOSED DURING MAINTENANCE MODE");
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

        if (breakerSwitchPivot != null)
        {
            Quaternion targetRotation = closed
                ? closedPivotWorldRotation
                : Quaternion.AngleAxis(switchOpenAngle, computedSwitchOpenAxisWorld.normalized) * closedPivotWorldRotation;

            if (switchAnimation != null)
                GetAnimationHost().StopCoroutine(switchAnimation);
            switchAnimation = GetAnimationHost().StartCoroutine(AnimateSwitchPivot(targetRotation));
            return;
        }

        if (breakerVisual == null)
        {
            Debug.LogWarning("[BreakerController] Breaker visual and Animator are not assigned; physical breaker motion is skipped.");
            return;
        }

        if (usePositionFallback)
        {
            ApplyLegacyBreakerVisual(closed);
            return;
        }

        if (useSwitchWorldSlideMotion)
        {
            Vector3 targetWorldPosition = closedSwitchWorldPosition + (closed ? switchClosedWorldOffset : switchOpenWorldOffset);

            if (switchAnimation != null)
                GetAnimationHost().StopCoroutine(switchAnimation);
            switchAnimation = GetAnimationHost().StartCoroutine(AnimateSwitchWorld(targetWorldPosition));
            return;
        }

        Quaternion targetLocalRotation = closed
            ? closedSwitchLocalRotation
            : closedSwitchLocalRotation * Quaternion.AngleAxis(switchOpenAngle, GetSafeLocalSwitchAxis());

        if (switchAnimation != null)
            GetAnimationHost().StopCoroutine(switchAnimation);
        switchAnimation = GetAnimationHost().StartCoroutine(AnimateSwitchLocal(targetLocalRotation, closed ? closedSwitchLocalPosition : closedLocalPosition));
    }

    IEnumerator AnimateSwitchLocal(Quaternion targetLocalRotation, Vector3 targetLocalPosition)
    {
        Quaternion startRotation = breakerVisual.localRotation;
        Vector3 startPosition = breakerVisual.localPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, switchAnimationSeconds);

        while (elapsed < duration)
        {
            if (breakerVisual == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            breakerVisual.localRotation = Quaternion.Slerp(startRotation, targetLocalRotation, t);
            breakerVisual.localPosition = Vector3.Lerp(startPosition, targetLocalPosition, t);
            yield return null;
        }

        if (breakerVisual != null)
        {
            breakerVisual.localRotation = targetLocalRotation;
            breakerVisual.localPosition = targetLocalPosition;
        }

        switchAnimation = null;
    }

    Vector3 GetSafeLocalSwitchAxis()
    {
        Vector3 axis = switchLocalOpenAxis;
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.right;
        return axis.normalized;
    }

    void ApplyLegacyBreakerVisual(bool closed)
    {
        if (usePositionFallback)
            breakerVisual.localPosition = closed ? closedLocalPosition : openLocalPosition;
        else
            breakerVisual.localRotation = Quaternion.Euler(closed ? closedEulerAngles : openEulerAngles);
    }

    void SetSwitchVisualImmediate(bool closed)
    {
        if (breakerVisual == null)
            return;

        breakerVisual.localRotation = closedSwitchLocalRotation;
        breakerVisual.localPosition = closedSwitchLocalPosition;
        breakerVisual.position = closedSwitchWorldPosition + (closed ? switchClosedWorldOffset : switchOpenWorldOffset);
    }

    IEnumerator AnimateSwitchWorld(Vector3 targetWorldPosition)
    {
        Vector3 startPosition = breakerVisual.position;
        Quaternion startRotation = breakerVisual.localRotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, switchAnimationSeconds);

        while (elapsed < duration)
        {
            if (breakerVisual == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            breakerVisual.localRotation = Quaternion.Slerp(startRotation, closedSwitchLocalRotation, t);
            breakerVisual.position = Vector3.Lerp(startPosition, targetWorldPosition, t);
            yield return null;
        }

        if (breakerVisual != null)
        {
            breakerVisual.localRotation = closedSwitchLocalRotation;
            breakerVisual.position = targetWorldPosition;
        }

        switchAnimation = null;
    }

    IEnumerator AnimateSwitchPivot(Quaternion targetRotation)
    {
        Quaternion startRotation = breakerSwitchPivot.rotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, switchAnimationSeconds);

        while (elapsed < duration)
        {
            if (breakerSwitchPivot == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            breakerSwitchPivot.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        if (breakerSwitchPivot != null)
            breakerSwitchPivot.rotation = targetRotation;
        switchAnimation = null;
    }

    MonoBehaviour GetAnimationHost()
    {
        if (animationHost != null)
            return animationHost;

        BreakerRuntimeCoroutineHost existing = FindFirstObjectByType<BreakerRuntimeCoroutineHost>();
        if (existing != null)
        {
            animationHost = existing;
            return animationHost;
        }

        GameObject hostObject = new GameObject("BreakerRuntimeCoroutineHost");
        DontDestroyOnLoad(hostObject);
        animationHost = hostObject.AddComponent<BreakerRuntimeCoroutineHost>();
        return animationHost;
    }

    BreakerRuntimeCoroutineHost GetRuntimeHost()
    {
        return GetAnimationHost() as BreakerRuntimeCoroutineHost;
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

        if (disableCinemachineBrainDuringBreakerFocus)
        {
            Behaviour brain = cameraToMove.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
                brain.enabled = false;
        }

        cameraToMove.fieldOfView = breakerFocusFieldOfView;
        Vector3 lookPoint = GetBreakerLookPoint(target);
        cameraToMove.transform.position = GetSafeFocusPosition(target, cameraToMove.transform.position);
        cameraToMove.transform.LookAt(lookPoint);
    }

    void StartBreakerFocusSequence()
    {
        Transform target = cameraFocusTarget != null ? cameraFocusTarget : breakerVisual;
        Camera cameraToMove = focusCamera != null ? focusCamera : Camera.main;

        if (target == null || cameraToMove == null)
            return;

        Transform cameraTransform = cameraToMove.transform;
        Transform originalParent = cameraTransform.parent;
        Vector3 originalLocalPosition = cameraTransform.localPosition;
        Quaternion originalLocalRotation = cameraTransform.localRotation;
        Vector3 originalWorldPosition = cameraTransform.position;
        Quaternion originalWorldRotation = cameraTransform.rotation;
        float originalFieldOfView = cameraToMove.fieldOfView;

        Behaviour brain = cameraToMove.GetComponent("CinemachineBrain") as Behaviour;
        bool brainWasEnabled = brain != null && brain.enabled;

        FPSKontrol fpsKontrol = FindFirstObjectByType<FPSKontrol>(FindObjectsInactive.Include);
        bool fpsWasEnabled = fpsKontrol != null && fpsKontrol.enabled;

        if (fpsKontrol != null)
            fpsKontrol.enabled = false;

        if (disableCinemachineBrainDuringBreakerFocus && brain != null)
            brain.enabled = false;

        cameraTransform.SetParent(null, true);
        cameraToMove.fieldOfView = breakerFocusFieldOfView;
        Vector3 lookPoint = GetBreakerLookPoint(target);
        cameraTransform.position = GetSafeFocusPosition(target, originalWorldPosition);
        cameraTransform.LookAt(lookPoint);

        Debug.Log("[BreakerController] Breaker focus started. Camera will restore after focus hold.");

        focusSequenceActive = true;
        focusRestoreTimer?.Dispose();
        focusRestoreTimer = new Timer(_ =>
        {
            SynchronizationContext context = unityContext;
            if (context != null)
                context.Post(__ => RestoreBreakerFocus(cameraToMove, cameraTransform, originalParent, originalLocalPosition, originalLocalRotation, originalWorldPosition, originalWorldRotation, originalFieldOfView, brain, brainWasEnabled, fpsKontrol, fpsWasEnabled), null);
            else if (mainThreadDispatcher != null)
                mainThreadDispatcher.Enqueue(() => RestoreBreakerFocus(cameraToMove, cameraTransform, originalParent, originalLocalPosition, originalLocalRotation, originalWorldPosition, originalWorldRotation, originalFieldOfView, brain, brainWasEnabled, fpsKontrol, fpsWasEnabled));
        }, null, (int)(Mathf.Max(breakerFocusHoldSeconds, switchAnimationSeconds) * 1000f), Timeout.Infinite);
    }

    void RestoreBreakerFocus(
        Camera cameraToMove,
        Transform cameraTransform,
        Transform originalParent,
        Vector3 originalLocalPosition,
        Quaternion originalLocalRotation,
        Vector3 originalWorldPosition,
        Quaternion originalWorldRotation,
        float originalFieldOfView,
        Behaviour brain,
        bool brainWasEnabled,
        FPSKontrol fpsKontrol,
        bool fpsWasEnabled)
    {
        if (!focusSequenceActive || cameraToMove == null || cameraTransform == null)
            return;

        cameraToMove.fieldOfView = originalFieldOfView;
        cameraTransform.SetParent(originalParent, true);

        if (originalParent != null)
        {
            cameraTransform.localPosition = originalLocalPosition;
            cameraTransform.localRotation = originalLocalRotation;
        }
        else
        {
            cameraTransform.position = originalWorldPosition;
            cameraTransform.rotation = originalWorldRotation;
        }

        if (brain != null)
            brain.enabled = brainWasEnabled;

        if (fpsKontrol != null)
        {
            fpsKontrol.enabled = fpsWasEnabled;
            if (fpsWasEnabled)
                fpsKontrol.SetTerminalDurumu(false);
        }

        focusRestoreTimer?.Dispose();
        focusRestoreTimer = null;
        focusSequenceActive = false;
        Debug.Log("[BreakerController] Breaker focus finished. Camera restored to previous user view.");
    }

    Vector3 GetSafeFocusPosition(Transform target, Vector3 viewerWorldPosition)
    {
        Vector3 lookPoint = GetBreakerLookPoint(target);
        if (cameraFocusViewpoint != null)
            return cameraFocusViewpoint.position;

        Vector3 focusDirection = GetInteriorFocusDirection(lookPoint, viewerWorldPosition, target);
        return lookPoint + (focusDirection * Mathf.Max(0.25f, breakerFocusDistance)) + (Vector3.up * breakerFocusHeight);
    }

    Vector3 GetInteriorFocusDirection(Vector3 lookPoint, Vector3 viewerWorldPosition, Transform target)
    {
        Transform safeReference = FindFocusReference(safeFocusReferenceObjectName);
        if (safeReference == null)
            safeReference = FindFocusReference(fallbackFocusReferenceObjectName);

        Vector3 sourcePosition = safeReference != null ? safeReference.position : viewerWorldPosition;
        Vector3 direction = sourcePosition - lookPoint;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = GetBreakerFrontDirection(target);

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = cameraFocusOffset;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = -target.forward;

        return direction.normalized;
    }

    Transform FindFocusReference(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject referenceObject = GameObject.Find(objectName);
        return referenceObject != null ? referenceObject.transform : null;
    }

    void CloseTerminalForFieldOperation()
    {
        if (!closeTerminalOnBreakerOperation)
            return;

        if (terminalWindowManager == null)
            terminalWindowManager = FindFirstObjectByType<TerminalPencereYonetici>(FindObjectsInactive.Include);

        if (terminalWindowManager == null || terminalWindowManager.terminalPencere == null)
            return;

        if (terminalWindowManager.terminalPencere.gameObject.activeSelf)
            terminalWindowManager.ToggleTerminal();
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
        if (terminalWindowManager == null)
            terminalWindowManager = GetComponent<TerminalPencereYonetici>() ?? FindFirstObjectByType<TerminalPencereYonetici>(FindObjectsInactive.Include);
    }

    void ResolveSceneBreakerModel()
    {
        if (breakerVisual == null)
        {
            GameObject switchObject = GameObject.Find(breakerSwitchObjectName);
            if (switchObject != null)
                breakerVisual = switchObject.transform;
        }

        if (cameraFocusTarget == null)
        {
            GameObject rootObject = GameObject.Find(breakerRootObjectName);
            if (rootObject != null)
                cameraFocusTarget = rootObject.transform;
        }

        if (focusCamera == null)
            focusCamera = Camera.main;

        if (autoCreateSwitchPivot && breakerVisual != null && breakerSwitchPivot == null)
            breakerSwitchPivot = CreateSwitchPivot(breakerVisual);
    }

    Transform CreateSwitchPivot(Transform switchTransform)
    {
        Transform originalParent = switchTransform.parent;
        GameObject pivotObject = new GameObject($"{switchTransform.name}_Pivot");
        Transform pivot = pivotObject.transform;

        Vector3 hingeWorldPosition = autoComputeSwitchHinge
            ? GetSwitchHingeWorldPosition(switchTransform)
            : switchTransform.position;

        pivot.SetParent(originalParent, true);
        pivot.position = hingeWorldPosition;
        pivot.rotation = switchTransform.rotation;
        pivot.localScale = Vector3.one;

        switchTransform.SetParent(pivot, true);
        closedPivotRotation = pivot.localRotation;
        closedPivotWorldRotation = pivot.rotation;
        computedSwitchOpenAxisWorld = ComputeSwitchOpenAxisWorld(switchTransform, hingeWorldPosition);

        Debug.Log($"[BreakerController] Runtime switch hinge pivot created for {switchTransform.name}. Hinge={hingeWorldPosition}, Axis={computedSwitchOpenAxisWorld}, OpenAngle={switchOpenAngle}");
        return pivot;
    }

    Vector3 GetSwitchHingeWorldPosition(Transform switchTransform)
    {
        Renderer switchRenderer = switchTransform.GetComponentInChildren<Renderer>();
        if (switchRenderer == null)
            return switchTransform.position;

        Bounds switchBounds = switchRenderer.bounds;
        Vector3 bodyCenter = GetBreakerBodyBounds().center;
        return switchBounds.ClosestPoint(bodyCenter);
    }

    Vector3 ComputeSwitchOpenAxisWorld(Transform switchTransform, Vector3 hingeWorldPosition)
    {
        Renderer switchRenderer = switchTransform.GetComponentInChildren<Renderer>();
        Vector3 handleDirection = switchRenderer != null
            ? switchRenderer.bounds.center - hingeWorldPosition
            : switchTransform.position - hingeWorldPosition;

        handleDirection.y = 0f;
        if (handleDirection.sqrMagnitude < 0.0001f)
            handleDirection = switchTransform.right;
        handleDirection.Normalize();

        Vector3 axis = Vector3.Cross(handleDirection, Vector3.up);
        if (axis.sqrMagnitude < 0.0001f)
            axis = transform.TransformDirection(switchOpenAxis);
        return axis.normalized;
    }

    Vector3 GetBreakerLookPoint(Transform target)
    {
        Bounds bounds = GetCompleteBreakerBounds(target);
        if (bounds.size.sqrMagnitude > 0.0001f)
            return bounds.center;

        return target.position + Vector3.up * 0.15f;
    }

    Bounds GetCompleteBreakerBounds(Transform target)
    {
        Renderer[] renderers = target != null ? target.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
        bool hasBounds = false;
        Bounds combined = new Bounds(target != null ? target.position : Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return combined;
    }

    Vector3 GetBreakerFrontDirection(Transform target)
    {
        Renderer switchRenderer = breakerVisual != null ? breakerVisual.GetComponentInChildren<Renderer>() : null;
        if (switchRenderer != null)
        {
            Vector3 bodyCenter = GetBreakerBodyBounds().center;
            Vector3 direction = switchRenderer.bounds.center - bodyCenter;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;
        }

        Vector3 fallback = cameraFocusOffset;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;

        return -target.forward;
    }

    Bounds GetBreakerBodyBounds()
    {
        Transform root = cameraFocusTarget != null ? cameraFocusTarget : breakerVisual;
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();

        bool hasBounds = false;
        Bounds combined = new Bounds(root != null ? root.position : Vector3.zero, Vector3.one);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || (breakerVisual != null && renderer.transform == breakerVisual))
                continue;

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return combined;
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

public class BreakerRuntimeCoroutineHost : MonoBehaviour
{
    class ScheduledAction
    {
        public float executeAt;
        public Action action;
    }

    readonly System.Collections.Generic.List<ScheduledAction> scheduledActions =
        new System.Collections.Generic.List<ScheduledAction>();

    public void Schedule(float delaySeconds, Action action)
    {
        if (action == null)
            return;

        scheduledActions.Add(new ScheduledAction
        {
            executeAt = Time.realtimeSinceStartup + Mathf.Max(0f, delaySeconds),
            action = action
        });
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        for (int i = scheduledActions.Count - 1; i >= 0; i--)
        {
            if (now < scheduledActions[i].executeAt)
                continue;

            Action action = scheduledActions[i].action;
            scheduledActions.RemoveAt(i);
            action.Invoke();
        }
    }
}
