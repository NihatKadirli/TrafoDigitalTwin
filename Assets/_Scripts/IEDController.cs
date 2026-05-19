using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IEDController : MonoBehaviour
{
    [Header("Protection Values")]
    public float currentAmpere = 420f;
    public float tripThreshold = 900f;
    public bool faultDetected;
    public bool tripStatus;
    public bool attackDetected;
    public bool gooseBlocked;
    public int gooseStNum = 1;
    public int gooseSqNum;
    public string lastGooseMessage = "GOOSE idle";

    [Header("UI References")]
    public TMP_Text currentText;
    public TMP_Text thresholdText;
    public TMP_Text tripStatusText;
    public TMP_Text stNumText;
    public TMP_Text sqNumText;
    public TMP_Text lastGooseText;
    public TMP_Text attackStatusText;
    public Image iedIndicator;

    public event Action OnStateChanged;

    public void SetMeasuredCurrent(float amps)
    {
        currentAmpere = Mathf.Max(0f, amps);

        if (!faultDetected && !attackDetected && !gooseBlocked)
            tripStatus = false;

        NotifyChanged(false);
    }

    public void ResetProtection()
    {
        currentAmpere = 420f;
        faultDetected = false;
        tripStatus = false;
        attackDetected = false;
        gooseBlocked = false;
        lastGooseMessage = "GOOSE reset; protection healthy";
        gooseSqNum++;
        NotifyChanged();
    }

    public void FaultOn(bool blockGoose)
    {
        currentAmpere = Mathf.Max(tripThreshold + 180f, currentAmpere);
        faultDetected = true;
        attackDetected = false;
        gooseBlocked = blockGoose;
        gooseStNum++;
        gooseSqNum++;

        if (blockGoose)
        {
            tripStatus = false;
            lastGooseMessage = $"GOOSE BLOCKED: PTRC.trip not delivered stNum={gooseStNum} sqNum={gooseSqNum}";
        }
        else
        {
            tripStatus = true;
            lastGooseMessage = $"GOOSE Trip sent: PTRC.trip=TRUE stNum={gooseStNum} sqNum={gooseSqNum}";
        }

        NotifyChanged();
    }

    public void FaultOff()
    {
        currentAmpere = 420f;
        faultDetected = false;
        gooseBlocked = false;
        tripStatus = false;
        gooseSqNum++;
        lastGooseMessage = $"GOOSE normal update: PTRC.trip=FALSE stNum={gooseStNum} sqNum={gooseSqNum}";
        NotifyChanged();
    }

    public void ManualTrip()
    {
        attackDetected = false;
        gooseBlocked = false;
        tripStatus = true;
        gooseStNum++;
        gooseSqNum++;
        lastGooseMessage = $"Operator trip command: XCBR.open stNum={gooseStNum} sqNum={gooseSqNum}";
        NotifyChanged();
    }

    public void SimulateGooseDataAttack()
    {
        currentAmpere = 430f;
        faultDetected = false;
        attackDetected = true;
        gooseBlocked = false;
        tripStatus = true;
        gooseStNum++;
        gooseSqNum++;
        lastGooseMessage = $"SPOOFED GOOSE Trip received: PTRC.trip=TRUE stNum={gooseStNum} sqNum={gooseSqNum}";
        NotifyChanged();
    }

    public void SimulateGooseDos()
    {
        FaultOn(true);
        attackDetected = true;
        lastGooseMessage = $"GOOSE communication blocked: trip not delivered stNum={gooseStNum} sqNum={gooseSqNum}";
        NotifyChanged();
    }

    public string GetProtectionStateLabel()
    {
        if (attackDetected)
            return "ATTACK DETECTED";
        if (gooseBlocked)
            return "GOOSE BLOCKED";
        if (tripStatus)
            return "TRIP";
        if (faultDetected)
            return "FAULT";
        return "NORMAL";
    }

    public Color GetProtectionColor()
    {
        if (attackDetected || gooseBlocked)
            return SCADAHMIController.WarningColor;
        if (tripStatus || faultDetected)
            return SCADAHMIController.FaultColor;
        return SCADAHMIController.NormalColor;
    }

    public void RefreshUI()
    {
        if (currentText != null)
            currentText.text = $"{currentAmpere:F0} A";
        if (thresholdText != null)
            thresholdText.text = $"{tripThreshold:F0} A";
        if (tripStatusText != null)
            tripStatusText.text = tripStatus ? "TRIP ACTIVE" : "NO TRIP";
        if (stNumText != null)
            stNumText.text = gooseStNum.ToString();
        if (sqNumText != null)
            sqNumText.text = gooseSqNum.ToString();
        if (lastGooseText != null)
            lastGooseText.text = lastGooseMessage;
        if (attackStatusText != null)
            attackStatusText.text = attackDetected ? "Attack Detected" : "No attack";
        if (iedIndicator != null)
            iedIndicator.color = GetProtectionColor();
    }

    void Start()
    {
        RefreshUI();
    }

    void NotifyChanged(bool raiseEvent = true)
    {
        RefreshUI();
        if (raiseEvent)
            OnStateChanged?.Invoke();
    }
}
