using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TerminalController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text OutputText;
    public TMP_InputField InputField;
    public TMP_Text StatusBar;
    public GameObject WarningPanel;
    public Button SendButton;
    
    [Header("MQTT")]
    public SimpleMQTTReceiver mqttReceiver;

    [Header("SCADA/HMI")]
    public SCADATerminalController scadaTerminalController;
    
    private List<string> history = new List<string>();
    private readonly List<string> outputLines = new List<string>();
    private const int MaxVisibleOutputLines = 34;
    private int historyIndex = 0;
    private bool isAttackRunning = false;
    private ScrollRect scrollRect;
    
    // Kullanıcının scroll pozisyonunu korumak için
    private bool autoScroll = true;  // Otomatik scroll aktif mi?
    
    
    private bool liveTelemetry = true;
    private int lastDisplayedTelemetryCounter = -1;
private int lastSubmitFrame = -1;
private float lastScrollPosition = 1f;  // Son scroll pozisyonu
    
    void Start()
    {
        FindElements();
        SetupInput();
        SetupButton();
        InitializeTerminal();
        StartCoroutine(UpdateStatus());
        
        // ScrollRect event'lerini dinle
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
        }
    }
    
void FindElements()
    {
        if (OutputText == null)
        {
            GameObject obj = GameObject.Find("OutputText");
            if (obj != null) OutputText = obj.GetComponent<TMP_Text>();
        }

        if (InputField == null)
            InputField = GetComponentInChildren<TMP_InputField>(true);

        if (StatusBar == null)
            StatusBar = GameObject.Find("StatusBar")?.GetComponent<TMP_Text>();

        if (WarningPanel == null)
            WarningPanel = GameObject.Find("WarningPanel");

        if (SendButton == null)
            SendButton = GameObject.Find("SendButton")?.GetComponent<Button>();

        if (mqttReceiver == null)
            mqttReceiver = FindFirstObjectByType<SimpleMQTTReceiver>();

        if (scadaTerminalController == null)
            scadaTerminalController = GetComponent<SCADATerminalController>() ?? FindFirstObjectByType<SCADATerminalController>();

        if (OutputText != null)
            scrollRect = OutputText.GetComponentInParent<ScrollRect>();
    }
    
    // Kullanıcı scroll yaptığında çağrılır
void OnScrollChanged(Vector2 position)
    {
        // ScrollRect'te alt konum 0'dır. Kullanici yukari cikarsa otomatik takip kapanir.
        if (position.y <= 0.02f)
            autoScroll = true;
        else if (position.y > 0.08f)
            autoScroll = false;

        lastScrollPosition = position.y;
    }
    
void SetupInput()
    {
        if (InputField == null) return;

        InputField.lineType = TMP_InputField.LineType.SingleLine;
        InputField.onSubmit.RemoveAllListeners();
        InputField.onEndEdit.RemoveAllListeners();
        InputField.onSubmit.AddListener(_ => SubmitCurrentInput());
        InputField.onEndEdit.AddListener(_ =>
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SubmitCurrentInput();
        });
        InputField.Select();
        InputField.ActivateInputField();
    }
    
void SetupButton()
    {
        if (SendButton == null) return;

        SendButton.onClick.RemoveAllListeners();
        SendButton.onClick.AddListener(SubmitCurrentInput);
    }
    
void InitializeTerminal()
    {
        if (OutputText != null)
            OutputText.text = "";
        outputLines.Clear();

        WriteLine("<color=#0FD19E><b>TRAFO MERKEZI SCADA TERMINALI</b></color>");
        WriteLine("<color=#728087>operator@scada-room  |  egitim/simulasyon  |  oturum: local</color>");
        WriteLine("================================================================");
        WriteLine("Baglanti: MQTT localhost:1883  |  Alan: TM1 / Trafo sensorleri");
        WriteLine("Rol     : Operator konsolu, siber olay simülasyonu ve telemetri izleme");
        WriteLine("----------------------------------------------------------------");
        WriteLine("Komut listesi icin <color=#0FD19E>help</color> yazin.");
        WriteLine("Onerilen akis: <color=#B9FBCB>status</color> -> <color=#B9FBCB>fault on</color> -> <color=#B9FBCB>reset</color> -> <color=#B9FBCB>attack goose_data</color>");
        WriteLine("");

        if (WarningPanel != null)
            WarningPanel.SetActive(false);
    }
    
void OnCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            FocusInput();
            return;
        }

        WriteLine("");
        WriteLine($"scada> {command}");

        ProcessCommand(command.ToLower().Trim());

        history.Add(command);
        historyIndex = history.Count;
        FocusInput();
    }
    
    void ProcessCommand(string cmd)
    {
        string[] parts = cmd.Split(' ');

        if (scadaTerminalController != null &&
            scadaTerminalController.TryProcessCommand(cmd, WriteLine, ClearTerminal))
        {
            UpdateStatusBar();
            return;
        }
        
        switch (parts[0])
        {
            case "help":
                WriteLine("");
                WriteLine("<color=#0FD19E>KOMUTLAR</color>");
                WriteLine("  help               Tum komutlari listeler");
                WriteLine("  status             MQTT baglanti ve trafo ozetini gosterir");
                WriteLine("  data               veri.py tarafindan gelen son telemetri paketini gosterir");
                WriteLine("  telemetry          data komutunun aynisi");
                WriteLine("  live on            Gelen Python verilerini terminale otomatik yazdirir");
                WriteLine("  live off           Otomatik telemetri yazimini kapatir");
                WriteLine("  scan               SCADA ag yuzeyi ve risk ozetini listeler");
                WriteLine("  attack fdi temp    Sicaklik sensorunde FDI senaryosu baslatir");
                WriteLine("  attack dos medium  MQTT veri akisinda orta seviye DoS senaryosu baslatir");
                WriteLine("  stop               Aktif senaryoyu durdurur");
                WriteLine("  clear              Terminal ciktisini temizler");
                WriteLine("");
                break;
                
            

            case "data":
            case "telemetry":
                PrintTelemetrySnapshot();
                break;

            case "live":
                if (parts.Length > 1 && parts[1] == "off")
                {
                    liveTelemetry = false;
                    WriteLine("> Canli telemetri yazimi kapatildi.");
                }
                else
                {
                    liveTelemetry = true;
                    WriteLine("> Canli telemetri yazimi acildi.");
                    PrintTelemetrySnapshot();
                }
                break;
case "status":
                PrintStatus();
                break;

            case "scan":
                PrintScan();
                break;
                
            case "clear":
                ClearTerminal();
                break;
                
            case "attack":
                if (parts.Length > 1)
                {
                    if (parts[1] == "fdi" && parts.Length > 2 && parts[2] == "temp")
                        StartFDIAttack();
                    else if (parts[1] == "dos" && parts.Length > 2 && parts[2] == "medium")
                        StartDoSAttack();
                    else
                        WriteLine("> Geçersiz saldırı. 'help' yazın.");
                }
                else
                {
                    WriteLine("> Saldırı tipi belirtin. 'help' yazın.");
                }
                break;
                
            case "stop":
                StopAttack();
                break;
                
            default:
                WriteLine($"> Bilinmeyen komut: {cmd}");
                break;
        }
        
        UpdateStatusBar();
    }

void PrintStatus()
    {
        WriteLine("");
        WriteLine("<color=#0FD19E><b>SISTEM DURUMU</b></color>");
        WriteLine("----------------------------------------------------------------");
        if (mqttReceiver != null)
        {
            WriteLine($"MQTT broker      : {(mqttReceiver.IsConnected() ? "<color=#8EF58C>BAGLI</color>" : "<color=#FF6961>BAGLI DEGIL / VERI BEKLENIYOR</color>")}");
            WriteLine($"Topic            : TM1/Trafo/sensor");
            WriteLine($"Paket sayisi     : {mqttReceiver.GetMessageCounter()}");
            WriteLine($"Trafo sicakligi  : {mqttReceiver.GetLastTemperature():F1} C");
            WriteLine($"Yag sicakligi    : {mqttReceiver.GetLastOilTemperature():F1} C");
            WriteLine($"Primer gerilim   : {mqttReceiver.GetLastVoltage():F1} kV");
            WriteLine($"Sekonder gerilim : {mqttReceiver.GetLastSecondaryVoltage():F1} kV");
            WriteLine($"Primer akim      : {mqttReceiver.GetLastPrimaryCurrent():F1} A");
            WriteLine($"Sekonder akim    : {mqttReceiver.GetLastSecondaryCurrent():F1} A");
            WriteLine($"Yuklenme         : %{mqttReceiver.GetLastLoad():F1}");
        }
        else
        {
            WriteLine("MQTT broker      : <color=#FFB84D>SimpleMQTTReceiver bulunamadi</color>");
            WriteLine("Telemetri        : veri.py calissa bile Unity alicisi yok.");
        }
        WriteLine($"Siber senaryo    : {(isAttackRunning ? "<color=#FF6961>AKTIF</color>" : "<color=#8EF58C>NORMAL</color>")}");
        WriteLine($"Canli yazim      : {(liveTelemetry ? "ACIK" : "KAPALI")}");
        WriteLine("");
    }

void PrintTelemetrySnapshot()
    {
        WriteLine("");
        WriteLine("<color=#0FD19E><b>PYTHON TELEMETRI PAKETI</b></color>");
        WriteLine("----------------------------------------------------------------");
        if (mqttReceiver == null)
        {
            WriteLine("SimpleMQTTReceiver bulunamadi. MQTT_Manager objesini kontrol edin.");
            WriteLine("");
            return;
        }

        if (mqttReceiver.GetMessageCounter() <= 0)
        {
            WriteLine("veri.py tarafindan henuz paket alinmadi.");
            WriteLine("Beklenen kaynak: Assets/_Scripts/veri.py -> TM1/Trafo/sensor");
            WriteLine("Mosquitto ve veri.py calistiginda bu alan guncellenecek.");
            WriteLine("");
            return;
        }

        WriteLine($"Paket #{mqttReceiver.GetMessageCounter()} | sicaklik={mqttReceiver.GetLastTemperature():F1} C | yag={mqttReceiver.GetLastOilTemperature():F1} C | gerilim={mqttReceiver.GetLastVoltage():F1} kV | yuk=%{mqttReceiver.GetLastLoad():F1}");
        WriteLine($"Akim primer={mqttReceiver.GetLastPrimaryCurrent():F1} A | akim sekonder={mqttReceiver.GetLastSecondaryCurrent():F1} A | sekonder gerilim={mqttReceiver.GetLastSecondaryVoltage():F1} kV");
        string raw = mqttReceiver.GetLastRawJson();
        if (!string.IsNullOrEmpty(raw))
            WriteLine($"JSON: {raw}");
        WriteLine("");
    }

void PrintLiveTelemetryIfNew()
    {
        if (!liveTelemetry || mqttReceiver == null) return;

        int counter = mqttReceiver.GetMessageCounter();
        if (counter <= 0 || counter == lastDisplayedTelemetryCounter) return;

        lastDisplayedTelemetryCounter = counter;
        WriteLine($"<color=#728087>[veri.py #{counter}]</color> sicaklik={mqttReceiver.GetLastTemperature():F1} C | yag={mqttReceiver.GetLastOilTemperature():F1} C | gerilim={mqttReceiver.GetLastVoltage():F1} kV | yuk=%{mqttReceiver.GetLastLoad():F1}", true);
    }


void PrintScan()
    {
        WriteLine("");
        WriteLine("<color=#0FD19E><b>SCADA AG TARAMASI</b></color>");
        WriteLine("----------------------------------------------------------------");
        WriteLine("IP          VARLIK          SERVIS          DURUM");
        WriteLine("10.10.0.11  HMI-01          502/tcp         Modbus izleniyor");
        WriteLine("10.10.0.21  RTU-TRAFO       1883/tcp        MQTT telemetri aktif");
        WriteLine("10.10.0.31  HISTORIAN       8086/tcp        Zaman serisi kaydi aktif");
        WriteLine("10.10.0.41  ENG-WS          22/tcp          Bakim istasyonu pasif");
        WriteLine("----------------------------------------------------------------");
        WriteLine("Risk ozeti  : Sensor verisi butunlugu ve MQTT akis surekliligi izlenmeli.");
        WriteLine("Oneri       : FDI ve DoS senaryolarini sirayla calistirip panel etkisini gozlemleyin.");
        WriteLine("");
    }
    
void StartFDIAttack()
    {
        if (isAttackRunning)
        {
            WriteLine("> Zaten aktif senaryo var. Durdurmak icin stop yazin.");
            FocusInput();
            return;
        }

        WriteLine("");
        WriteLine("<color=#FF6961><b>FDI SENARYOSU BASLATILDI</b></color>");
        WriteLine("Hedef : Sicaklik sensoru");
        WriteLine("Etki  : MQTT uzerinden sahte yuksek sicaklik degeri enjekte edilir");
        WriteLine("Deger : 95 C");
        WriteLine("Sure  : 10 saniye");

        isAttackRunning = true;

        if (mqttReceiver != null)
        {
            mqttReceiver.SetAttackMode(true, "temperature", 95f);
            WriteLine("Durum : MQTT alicisi saldiri moduna alindi.");
        }
        else
        {
            WriteLine("<color=#FFB84D>Uyari : SimpleMQTTReceiver bulunamadi, sadece terminal senaryosu calisiyor.</color>");
        }

        WriteLine("");
        ShowWarning("FDI SENARYOSU AKTIF: sicaklik verisi manipule ediliyor");
        StartCoroutine(AutoStop(10));
        FocusInput();
    }
    
    void StartDoSAttack()
    {
        if (isAttackRunning)
        {
            WriteLine("> Zaten aktif saldırı var! 'stop' ile durdurun.");
            return;
        }
        
        WriteLine("");
        WriteLine("<color=#FF6961>DoS SENARYOSU BASLATILDI</color>");
        WriteLine("Siddet: MEDIUM");
        WriteLine("Etki  : MQTT telemetri akisi gecici olarak kesilir");
        WriteLine("Sure  : 10 saniye");
        WriteLine("");
        
        isAttackRunning = true;
        
        if (mqttReceiver != null)
            mqttReceiver.SetAttackMode(true, "dos", 0);
        
        ShowWarning("DoS SENARYOSU AKTIF: veri akisi kesiliyor");
        StartCoroutine(AutoStop(10));
    }
    
void StopAttack()
    {
        if (!isAttackRunning)
        {
            WriteLine("> Aktif senaryo yok.");
            FocusInput();
            return;
        }

        WriteLine("");
        WriteLine("<color=#8EF58C><b>SENARYO DURDURULDU</b></color>");
        WriteLine("");

        if (mqttReceiver != null)
            mqttReceiver.SetAttackMode(false, "", 0);

        if (WarningPanel != null)
            WarningPanel.SetActive(false);

        isAttackRunning = false;
        UpdateStatusBar();
        WriteLine("> Sistem normale dondu.");
        FocusInput();
    }
    
    IEnumerator AutoStop(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (isAttackRunning)
            StopAttack();
    }
    
    void ShowWarning(string message)
    {
        if (WarningPanel != null)
        {
            WarningPanel.SetActive(true);
            TMP_Text text = WarningPanel.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = message;
            StartCoroutine(HideWarning());
        }
    }
    
    IEnumerator HideWarning()
    {
        yield return new WaitForSeconds(3);
        if (WarningPanel != null && !isAttackRunning)
            WarningPanel.SetActive(false);
    }
    
void UpdateStatusBar()
    {
        if (StatusBar == null) return;

        if (mqttReceiver == null)
        {
            StatusBar.text = "MQTT ALICI YOK | veri.py beklenemiyor";
            return;
        }

        float temp = mqttReceiver.GetLastTemperature();
        float volt = mqttReceiver.GetLastVoltage();
        float load = mqttReceiver.GetLastLoad();

        string text = $"PY #{mqttReceiver.GetMessageCounter()} | TEMP {temp:F1} C | VOLT {volt:F1} kV | LOAD %{load:F1}";
        if (isAttackRunning) text = "SENARYO AKTIF | " + text;
        if (!mqttReceiver.IsConnected()) text += " | MQTT VERI BEKLENIYOR";

        StatusBar.text = text;
    }
    
IEnumerator UpdateStatus()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            UpdateStatusBar();
        }
    }
    
void WriteLine(string text)
    {
        WriteLine(text, false);
    }

    void WriteLine(string text, bool forceScroll)
    {
        if (OutputText != null)
        {
            outputLines.Add(text);
            while (outputLines.Count > MaxVisibleOutputLines)
                outputLines.RemoveAt(0);

            OutputText.text = string.Join("\n", outputLines);

            if (forceScroll)
            {
                autoScroll = true;
                ScrollToBottom();
            }
            else if (autoScroll)
            {
                ScrollToBottom();
            }
            else if (scrollRect != null)
            {
                StartCoroutine(RestoreScrollPosition());
            }
        }
        else
        {
            Debug.Log(text);
        }
    }

    void UpdateOutputContentHeight()
    {
        if (OutputText == null || scrollRect == null || scrollRect.content == null)
            return;

        OutputText.ForceMeshUpdate();
        float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        float preferredHeight = OutputText.preferredHeight + 24f;
        float targetHeight = Mathf.Max(viewportHeight, preferredHeight);
        scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        RectTransform textRect = OutputText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    
    IEnumerator RestoreScrollPosition()
    {
        yield return new WaitForEndOfFrame();
        if (scrollRect != null && !autoScroll)
        {
            scrollRect.verticalNormalizedPosition = lastScrollPosition;
        }
    }
    
    void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            StartCoroutine(ScrollCoroutine());
        }
    }
    
    IEnumerator ScrollCoroutine()
    {
        yield return new WaitForEndOfFrame();
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            UpdateOutputContentHeight();
            scrollRect.verticalNormalizedPosition = 0f;
            autoScroll = true;
            lastScrollPosition = 0f;
        }
    }
    
void ClearTerminal()
    {
        outputLines.Clear();
        if (OutputText != null)
            OutputText.text = "";
        WriteLine("Terminal temizlendi. Komut listesi icin help yazin.");

        autoScroll = true;
        ScrollToBottom();
    }
    
void Update()
    {
        PrintLiveTelemetryIfNew();

        if (InputField == null) return;

        if (InputField.isFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            SubmitCurrentInput();

        if (InputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) && history.Count > 0)
            {
                historyIndex = Mathf.Max(0, historyIndex - 1);
                InputField.text = history[historyIndex];
                InputField.caretPosition = InputField.text.Length;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && historyIndex < history.Count - 1)
            {
                historyIndex++;
                InputField.text = history[historyIndex];
                InputField.caretPosition = InputField.text.Length;
            }
        }
    }


void SubmitCurrentInput()
    {
        if (InputField == null) return;
        if (lastSubmitFrame == Time.frameCount) return;
        lastSubmitFrame = Time.frameCount;

        string command = InputField.text;
        InputField.text = "";
        OnCommand(command);
    }

    void FocusInput()
    {
        if (InputField == null) return;
        InputField.Select();
        InputField.ActivateInputField();
        InputField.caretPosition = InputField.text.Length;
    }
}
