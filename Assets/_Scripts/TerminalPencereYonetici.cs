using UnityEngine;

/// <summary>
/// Terminal penceresinin açma/kapama, küçültme/büyütme işlemlerini yönetir.
/// F1 tuşu ile terminal toggle edilir.
/// FPSKontrol ile entegre çalışır (terminal açıkken mouse serbest).
/// </summary>
public class TerminalPencereYonetici : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Terminal ana pencere RectTransform'u")]
    public RectTransform terminalPencere;

    [Tooltip("Terminal icerik alani (kucultunce gizlenir)")]
    public GameObject icerikAlani;

    [Tooltip("Terminal durum cubugu")]
    public GameObject durumCubugu;

    [Tooltip("Terminal uyari paneli")]
    public GameObject uyariPaneli;

    [Tooltip("FPS Kontrol referansı")]
    public FPSKontrol fpsKontrol;

    [Header("Ayarlar")]
    [Tooltip("Terminal açma/kapama tuşu")]
    public KeyCode toggleTusu = KeyCode.F1;

    [Tooltip("Terminal küçültme tuşu")]
    public KeyCode kucultmeTusu = KeyCode.F2;

    // ── Dahili ──
    private bool _terminalAcik = true;
    private bool _kucultulmus = false;
    private Vector2 _normalAnchorMin;
    private Vector2 _normalAnchorMax;
    private Vector2 _normalPivot;
    private Vector2 _normalOffsetMin;
    private Vector2 _normalOffsetMax;
    private Vector2 _miniBoyut = new Vector2(720, 58);

    void Start()
    {
        if (fpsKontrol == null)
            fpsKontrol = FindFirstObjectByType<FPSKontrol>();

        if (terminalPencere != null)
        {
            KaydetNormalCerceve();
            TamEkranCercevesiniUygula();
        }

        if (fpsKontrol != null && fpsKontrol.enabled)
            fpsKontrol.SetTerminalDurumu(_terminalAcik);
    }

void OnEnable()
    {
        if (fpsKontrol == null)
            fpsKontrol = FindFirstObjectByType<FPSKontrol>();
        UygulaFPSKilitDurumu();
    }


    void Update()
    {
        // F1: Terminal aç/kapa
        if (Input.GetKeyDown(toggleTusu))
        {
            ToggleTerminal();
        }

        // F2: Küçült/Büyüt
        if (Input.GetKeyDown(kucultmeTusu) && _terminalAcik)
        {
            ToggleKucult();
        }
    }

    /// <summary>Terminal'i aç veya kapa.</summary>
public void ToggleTerminal()
    {
        _terminalAcik = !_terminalAcik;

        if (terminalPencere != null)
        {
            terminalPencere.gameObject.SetActive(_terminalAcik);
            if (_terminalAcik && _kucultulmus)
                MiniDurumaGec();
        }

        UygulaFPSKilitDurumu();

        Debug.Log($"[TerminalPencereYonetici] Terminal {(_terminalAcik ? "ACILDI" : "KAPANDI")}");
    }

    /// <summary>Terminal'i küçült veya büyüt.</summary>
public void ToggleKucult()
    {
        _kucultulmus = !_kucultulmus;

        if (terminalPencere == null) return;

        if (_kucultulmus)
            MiniDurumaGec();
        else
            TamEkranaDon();

        UygulaFPSKilitDurumu();

        Debug.Log($"[TerminalPencereYonetici] Terminal {(_kucultulmus ? "MINI" : "TAM EKRAN")}");
    }

void MiniDurumaGec()
    {
        SetAltPaneller(false);
        terminalPencere.anchorMin = new Vector2(1f, 0f);
        terminalPencere.anchorMax = new Vector2(1f, 0f);
        terminalPencere.pivot = new Vector2(1f, 0f);
        terminalPencere.sizeDelta = _miniBoyut;
        terminalPencere.anchoredPosition = new Vector2(-24f, 24f);
    }

    void TamEkranaDon()
    {
        TamEkranCercevesiniUygula();
        SetAltPaneller(true);
        if (uyariPaneli != null)
            uyariPaneli.SetActive(false);
    }

    void KaydetNormalCerceve()
    {
        _normalAnchorMin = new Vector2(0.035f, 0.055f);
        _normalAnchorMax = new Vector2(0.965f, 0.945f);
        _normalPivot = new Vector2(0.5f, 0.5f);
        _normalOffsetMin = Vector2.zero;
        _normalOffsetMax = Vector2.zero;
    }

    void TamEkranCercevesiniUygula()
    {
        terminalPencere.anchorMin = _normalAnchorMin;
        terminalPencere.anchorMax = _normalAnchorMax;
        terminalPencere.pivot = _normalPivot;
        terminalPencere.offsetMin = _normalOffsetMin;
        terminalPencere.offsetMax = _normalOffsetMax;
        terminalPencere.anchoredPosition = Vector2.zero;
        terminalPencere.sizeDelta = Vector2.zero;
    }

    void SetAltPaneller(bool aktif)
    {
        if (icerikAlani != null)
            icerikAlani.SetActive(aktif);
        if (durumCubugu != null)
            durumCubugu.SetActive(aktif);
        if (!aktif && uyariPaneli != null)
            uyariPaneli.SetActive(false);
    }

void UygulaFPSKilitDurumu()
    {
        if (fpsKontrol == null || !fpsKontrol.enabled) return;

        bool terminalTamEkranAcik = _terminalAcik && !_kucultulmus;
        fpsKontrol.SetTerminalDurumu(terminalTamEkranAcik);
    }



    /// <summary>Dışarıdan küçültme tetikleme (buton için).</summary>
    public void KucultButonTiklandi()
    {
        ToggleKucult();
    }

    /// <summary>Dışarıdan terminal toggle (buton için).</summary>
    public void ToggleButonTiklandi()
    {
        ToggleTerminal();
    }
}
