using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SCADATerminalController : MonoBehaviour
{
    [Header("Controllers")]
    public SCADAHMIController hmiController;
    public TerminalController legacyTerminal;

    [Header("Optional UI References")]
    public TMP_Text outputText;
    public TMP_InputField inputField;
    public Button sendButton;

    public bool TryProcessCommand(string command, Action<string> writeLine, Action clearOutput)
    {
        if (string.IsNullOrWhiteSpace(command))
            return true;

        EnsureReferences();

        if (hmiController == null)
        {
            writeLine?.Invoke("> SCADA/HMI controller bulunamadi. Terminal_Canvas uzerindeki script baglantilarini kontrol edin.");
            return false;
        }

        string cmd = command.Trim().ToLowerInvariant();
        string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return true;

        switch (parts[0])
        {
            case "help":
                PrintHelp(writeLine);
                return true;

            case "status":
                PrintStatus(writeLine);
                return true;

            case "fault":
                if (parts.Length > 1 && parts[1] == "on")
                {
                    hmiController.SetFault(true);
                    writeLine("> Fault simulation enabled. IED detected overcurrent and sent GOOSE Trip.");
                    return true;
                }
                if (parts.Length > 1 && parts[1] == "off")
                {
                    hmiController.SetFault(false);
                    writeLine("> Fault simulation cleared. Protection values returned below threshold.");
                    return true;
                }
                writeLine("> Usage: fault on | fault off");
                return true;

            case "trip":
                hmiController.ManualTrip();
                writeLine("> Manual trip executed. Circuit Breaker opened.");
                return true;

            case "reset":
                hmiController.ResetSystem();
                writeLine("> SCADA/HMI state reset. Breaker closed and alarms cleared.");
                return true;

            case "attack":
                return ProcessAttack(parts, writeLine);

            case "clear":
                clearOutput?.Invoke();
                return true;

            default:
                return false;
        }
    }

    public void WriteExternalLine(string line)
    {
        if (outputText == null)
        {
            Debug.Log(line);
            return;
        }

        outputText.text += line + "\n";
    }

    public void StartCoolingFalseDataAttack()
    {
        EnsureReferences();
        if (hmiController == null)
        {
            WriteExternalLine("> Cooling FDI attack could not start: SCADA/HMI controller is missing.");
            return;
        }

        hmiController.StartCoolingFalseDataAttack();
        WriteExternalLine("> False Temperature Injection selected from SCADA UI.");
    }

    public void StopCoolingFalseDataAttack()
    {
        EnsureReferences();
        if (hmiController == null)
        {
            WriteExternalLine("> Cooling FDI restore could not run: SCADA/HMI controller is missing.");
            return;
        }

        hmiController.StopCoolingFalseDataAttack();
        WriteExternalLine("> False Temperature Injection recovery selected from SCADA UI.");
    }

    public void EnsureReferences()
    {
        if (hmiController == null)
            hmiController = GetComponent<SCADAHMIController>() ?? FindFirstObjectByType<SCADAHMIController>();
        if (legacyTerminal == null)
            legacyTerminal = GetComponent<TerminalController>() ?? FindFirstObjectByType<TerminalController>();
    }

    bool ProcessAttack(string[] parts, Action<string> writeLine)
    {
        if (parts.Length < 2)
        {
            writeLine("> Usage: attack goose_data | attack goose_dos | attack false_temp | attack false_temp_stop");
            return true;
        }

        switch (parts[1])
        {
            case "goose_data":
                hmiController.SimulateGooseDataAttack();
                writeLine("> Spoofed IEC 61850 GOOSE Trip simulated. Breaker opened without real fault.");
                return true;

            case "goose_dos":
                hmiController.SimulateGooseDosAttack();
                writeLine("> GOOSE DoS simulated. IED detected fault, but trip message was blocked.");
                return true;

            case "false_temp":
            case "false_temperature":
            case "cooling_fdi":
                hmiController.StartCoolingFalseDataAttack();
                writeLine("> False Temperature Injection command published to veri.py.");
                writeLine("> SCADA displayedTemperature stays near 55-60 C while realTemperature rises.");
                return true;

            case "false_temp_stop":
            case "false_temperature_stop":
            case "cooling_fdi_stop":
                hmiController.StopCoolingFalseDataAttack();
                writeLine("> False Temperature Injection recovery command published to veri.py.");
                return true;

            default:
                return false;
        }
    }

    void PrintHelp(Action<string> writeLine)
    {
        writeLine("");
        writeLine("<color=#0FD19E><b>SCADA/HMI COMMANDS</b></color>");
        writeLine("  status             Show breaker, IED, GOOSE and component state");
        writeLine("  fault on           Raise current above trip threshold and send GOOSE Trip");
        writeLine("  fault off          Clear the overcurrent condition");
        writeLine("  trip               Open the breaker with a manual SCADA trip command");
        writeLine("  reset              Return IED, breaker and alarms to normal");
        writeLine("  attack goose_data  Simulate spoofed GOOSE Trip data");
        writeLine("  attack goose_dos   Simulate GOOSE communication blocking");
        writeLine("  attack false_temp Start False Temperature Injection");
        writeLine("  attack false_temp_stop  Restore temperature data");
        writeLine("  clear              Clear this embedded command console");
        writeLine("");
    }

    void PrintStatus(Action<string> writeLine)
    {
        foreach (string line in hmiController.GetStatusLines())
            writeLine(line);
    }
}
