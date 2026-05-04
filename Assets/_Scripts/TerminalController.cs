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
    
    private List<string> history = new List<string>();
    private int historyIndex = 0;
    private bool isAttackRunning = false;
    private ScrollRect scrollRect;
    
    // Kullanıcının scroll pozisyonunu korumak için
    private bool autoScroll = true;  // Otomatik scroll aktif mi?
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
        {
            GameObject obj = GameObject.Find("Text Area");
            if (obj != null) InputField = obj.GetComponent<TMP_InputField>();
        }
        
        if (StatusBar == null)
            StatusBar = GameObject.Find("StatusBar")?.GetComponent<TMP_Text>();
        
        if (WarningPanel == null)
            WarningPanel = GameObject.Find("WarningPanel");
        
        if (SendButton == null)
            SendButton = GameObject.Find("SendButton")?.GetComponent<Button>();
        
        if (mqttReceiver == null)
            mqttReceiver = FindFirstObjectByType<SimpleMQTTReceiver>();
        
        if (OutputText != null)
            scrollRect = OutputText.GetComponentInParent<ScrollRect>();
    }
    
    // Kullanıcı scroll yaptığında çağrılır
    void OnScrollChanged(Vector2 position)
    {
        // Kullanıcı manuel olarak yukarı scroll yaptıysa otomatik scroll'u kapat
        if (position.y < 0.99f)
        {
            autoScroll = false;
        }
        // Kullanıcı en alta scroll yaptıysa otomatik scroll'u tekrar aç
        else if (position.y >= 0.99f)
        {
            autoScroll = true;
        }
        
        lastScrollPosition = position.y;
    }
    
    void SetupInput()
    {
        if (InputField != null)
        {
            InputField.onEndEdit.AddListener(OnCommand);
            InputField.Select();
        }
    }
    
    void SetupButton()
    {
        if (SendButton != null)
        {
            SendButton.onClick.AddListener(() => {
                if (InputField != null) OnCommand(InputField.text);
            });
        }
    }
    
    void InitializeTerminal()
    {
        ClearTerminal();
        WriteLine("╔════════════════════════════════════════════════╗");
        WriteLine("║     TRAFO SİBER GÜVENLİK SİMÜLATÖRÜ v1.0     ║");
        WriteLine("╚════════════════════════════════════════════════╝");
        WriteLine("");
        WriteLine("> Terminal hazır. 'help' yazın.");
        WriteLine("");
        
        if (WarningPanel != null)
            WarningPanel.SetActive(false);
    }
    
    void OnCommand(string command)
    {
        if (string.IsNullOrEmpty(command)) return;
        
        WriteLine("");
        WriteLine($">>> {command}");
        
        ProcessCommand(command.ToLower().Trim());
        
        if (InputField != null)
        {
            InputField.text = "";
            InputField.Select();
        }
        
        history.Add(command);
        historyIndex = history.Count;
    }
    
    void ProcessCommand(string cmd)
    {
        string[] parts = cmd.Split(' ');
        
        switch (parts[0])
        {
            case "help":
                WriteLine("");
                WriteLine("  help     - Bu menü");
                WriteLine("  status   - Sistem durumu");
                WriteLine("  clear    - Temizle");
                WriteLine("  attack fdi temp  - FDI saldırısı");
                WriteLine("  attack dos medium - DoS saldırısı");
                WriteLine("  stop     - Saldırıyı durdur");
                WriteLine("");
                break;
                
            case "status":
                WriteLine("");
                WriteLine("┌────────────────────────────────────────┐");
                WriteLine("│ SİSTEM DURUMU                         │");
                WriteLine("├────────────────────────────────────────┤");
                if (mqttReceiver != null)
                {
                    WriteLine($"│ MQTT   : {(mqttReceiver.IsConnected() ? "BAĞLI ✓" : "BAĞLI DEĞİL ✗")}");
                    WriteLine($"│ Sıcaklık: {mqttReceiver.GetLastTemperature():F0} °C");
                    WriteLine($"│ Gerilim : {mqttReceiver.GetLastVoltage():F0} kV");
                    WriteLine($"│ Yük     : %{mqttReceiver.GetLastLoad():F0}");
                }
                else
                {
                    WriteLine("│ MQTT   : BULUNAMADI !");
                }
                WriteLine($"│ Saldırı : {(isAttackRunning ? "AKTİF" : "YOK")}");
                WriteLine("└────────────────────────────────────────┘");
                WriteLine("");
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
    
    void StartFDIAttack()
    {
        if (isAttackRunning)
        {
            WriteLine("> Zaten aktif saldırı var! 'stop' ile durdurun.");
            return;
        }
        
        WriteLine("");
        WriteLine("🔴 FDI SALDIRISI BAŞLATILDI!");
        WriteLine("   Hedef: Sıcaklık Sensörü");
        WriteLine("   Etki: Veri manipülasyonu");
        WriteLine("");
        
        isAttackRunning = true;
        
        if (mqttReceiver != null)
            mqttReceiver.SetAttackMode(true, "temperature", 95f);
        
        ShowWarning("⚠️ FDI SALDIRISI! Sıcaklık verileri manipüle ediliyor!");
        StartCoroutine(AutoStop(10));
    }
    
    void StartDoSAttack()
    {
        if (isAttackRunning)
        {
            WriteLine("> Zaten aktif saldırı var! 'stop' ile durdurun.");
            return;
        }
        
        WriteLine("");
        WriteLine("🔴 DoS SALDIRISI BAŞLATILDI!");
        WriteLine("   Şiddet: MEDIUM");
        WriteLine("   Etki: Veri akışı kesiliyor");
        WriteLine("");
        
        isAttackRunning = true;
        
        if (mqttReceiver != null)
            mqttReceiver.SetAttackMode(true, "dos", 0);
        
        ShowWarning("⚠️ DoS SALDIRISI! Veri akışı kesildi!");
        StartCoroutine(AutoStop(10));
    }
    
    void StopAttack()
    {
        if (!isAttackRunning)
        {
            WriteLine("> Aktif saldırı yok.");
            return;
        }
        
        WriteLine("");
        WriteLine("🛑 SALDIRI DURDURULDU");
        WriteLine("");
        
        if (mqttReceiver != null)
            mqttReceiver.SetAttackMode(false, "", 0);
        
        if (WarningPanel != null)
            WarningPanel.SetActive(false);
        
        isAttackRunning = false;
        UpdateStatusBar();
        WriteLine("> Sistem normale döndü.");
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
        if (StatusBar != null && mqttReceiver != null)
        {
            float temp = mqttReceiver.GetLastTemperature();
            float volt = mqttReceiver.GetLastVoltage();
            float load = mqttReceiver.GetLastLoad();
            
            string text = $"🌡️ {temp:F0}°C  ⚡ {volt:F0}kV  📊 %{load:F0}";
            if (isAttackRunning) text = "⚠️ SALDIRI AKTİF ⚠️  |  " + text;
            
            StatusBar.text = text;
        }
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
        if (OutputText != null)
        {
            OutputText.text += text + "\n";
            
            // SADECE otomatik scroll aktifse en alta kaydır
            if (autoScroll)
            {
                ScrollToBottom();
            }
            // Değilse, kullanıcının scroll pozisyonunu koru
            else if (scrollRect != null)
            {
                // Scroll pozisyonunu geri yükle
                StartCoroutine(RestoreScrollPosition());
            }
        }
        else
        {
            Debug.Log(text);
        }
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
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 0f;
            autoScroll = true;
            lastScrollPosition = 0f;
        }
    }
    
    void ClearTerminal()
    {
        if (OutputText != null)
            OutputText.text = "";
        WriteLine("> Terminal temizlendi.");
        
        // Temizleme sonrası en alta scroll yap
        autoScroll = true;
        ScrollToBottom();
    }
    
    void Update()
    {
        if (InputField != null && InputField.isFocused)
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
}