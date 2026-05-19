using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CircuitBreakerController : MonoBehaviour
{
    public enum BreakerState
    {
        Closed,
        Open,
        Trip
    }

    [Header("State")]
    public BreakerState currentState = BreakerState.Closed;
    public string lastOperation = "Initial closed state";

    [Header("UI References")]
    public TMP_Text stateText;
    public TMP_Text detailText;
    public Image stateIndicator;
    public Image mimicIndicator;

    public event Action OnStateChanged;

    public bool IsClosed => currentState == BreakerState.Closed;
    public bool IsOpen => currentState == BreakerState.Open || currentState == BreakerState.Trip;
    public bool IsTrip => currentState == BreakerState.Trip;

    public void Close(string reason = "Operator reset")
    {
        currentState = BreakerState.Closed;
        lastOperation = reason;
        NotifyChanged();
    }

    public void Open(string reason = "Breaker opened")
    {
        currentState = BreakerState.Open;
        lastOperation = reason;
        NotifyChanged();
    }

    public void Trip(string reason = "Protection trip")
    {
        currentState = BreakerState.Trip;
        lastOperation = reason;
        NotifyChanged();
    }

    public string GetStateLabel()
    {
        switch (currentState)
        {
            case BreakerState.Closed:
                return "CLOSED";
            case BreakerState.Trip:
                return "TRIP";
            default:
                return "OPEN";
        }
    }

    public Color GetStateColor()
    {
        return currentState == BreakerState.Closed
            ? SCADAHMIController.NormalColor
            : SCADAHMIController.FaultColor;
    }

    public void RefreshUI()
    {
        string label = GetStateLabel();
        Color color = GetStateColor();

        if (stateText != null)
            stateText.text = label;
        if (detailText != null)
            detailText.text = lastOperation;
        if (stateIndicator != null)
            stateIndicator.color = color;
        if (mimicIndicator != null)
            mimicIndicator.color = color;
    }

    void Start()
    {
        RefreshUI();
    }

    void NotifyChanged()
    {
        RefreshUI();
        OnStateChanged?.Invoke();
    }
}
