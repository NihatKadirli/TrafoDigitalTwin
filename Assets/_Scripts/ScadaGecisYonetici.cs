using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine turunu izler. vCam_ScadaOdasi aktif olduğunda ve
/// PlayableDirector durduğunda FPS kontrolü + Terminal'i aktif eder.
/// Main Camera'yı FPS_Player'ın child'ı yaparak tam FPS kontrol sağlar.
/// </summary>
public class ScadaGecisYonetici : MonoBehaviour
{
    [Header("Referanslar")]
    public CinemachineBrain cinemachineBrain;
    public PlayableDirector playableDirector;
    public FPSKontrol fpsKontrol;
    public GameObject terminalCanvas;        // Terminal_Canvas
    public GameObject bilesenBilgileriCanvas; // Bilesen_Bilgileri

    [Header("Ayarlar")]
    [Tooltip("SCADA kamerası aktifken tur bitiş bekleme süresi (sn)")]
    public float turBitisGecikmesi = 2f;

    [Tooltip("Kameranın FPS_Player'a göre göz yüksekliği offset'i")]
    public float gozYuksekligiOffset = 0.6f;

    [Tooltip("SCADA odasinda collider yoksa oyuncunun dusmemesi icin gecis sirasinda gorunmez zemin olustur.")]
    public bool scadaGuvenliZeminOlustur = true;

    [Tooltip("Gorunmez SCADA zemin boyutu.")]
    public Vector3 scadaZeminBoyutu = new Vector3(12f, 0.12f, 10f);

    [Tooltip("Sinematik turu atlayip dogrudan SCADA odasina geciren tus.")]
    public KeyCode sinematikSkipTusu = KeyCode.Return;

    // ── Dahili ──
    private bool _gecisYapildi = false;
    private bool _scadaKamerasiAktifti = false;
    private float _scadaBaslamaSuresi = -1f;
    private CinemachineCamera _scadaVCam;

    void Start()
    {
        // Otomatik bul
        if (cinemachineBrain == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null) cinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
        }
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();
        if (fpsKontrol == null)
            fpsKontrol = FindFirstObjectByType<FPSKontrol>();
        if (terminalCanvas == null)
            terminalCanvas = GameObject.Find("Terminal_Canvas");
        if (bilesenBilgileriCanvas == null)
            bilesenBilgileriCanvas = GameObject.Find("Bilesen_Bilgileri");

        // SCADA vCam referansı
        var scadaGO = GameObject.Find("vCam_ScadaOdasi");
        if (scadaGO != null)
            _scadaVCam = scadaGO.GetComponent<CinemachineCamera>();

        // Başlangıçta terminal gizli
        if (terminalCanvas != null)
            terminalCanvas.SetActive(false);
    }

    void Update()
    {
        if (_gecisYapildi) return;

        if (Input.GetKeyDown(sinematikSkipTusu) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SinematigiAtlaVeScadayaGec();
            return;
        }

        // SCADA kamerası aktif mi?
        bool scadaSimdiAktif = false;
        if (cinemachineBrain != null && _scadaVCam != null)
        {
            var aktif = cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;
            scadaSimdiAktif = (aktif == _scadaVCam);
        }

        // İlk kez SCADA'ya geçildi
        if (scadaSimdiAktif && !_scadaKamerasiAktifti)
        {
            _scadaKamerasiAktifti = true;
            _scadaBaslamaSuresi = Time.time;
            Debug.Log("[ScadaGecisYonetici] SCADA kamerası aktif, tur bitiş bekleniyor...");
        }

        // SCADA aktif + (Director durdu VEYA bekleme süresi geçti)
        if (_scadaKamerasiAktifti && _scadaBaslamaSuresi > 0)
        {
            bool directorBitti = playableDirector == null ||
                                 playableDirector.state != PlayState.Playing;
            bool sureGecti = (Time.time - _scadaBaslamaSuresi) >= turBitisGecikmesi;

            if (directorBitti && sureGecti)
            {
                GecisYap();
            }
        }
    }

    public void SinematigiAtlaVeScadayaGec()
    {
        if (_gecisYapildi) return;

        if (playableDirector != null && playableDirector.state == PlayState.Playing)
            playableDirector.Stop();

        _scadaKamerasiAktifti = true;
        _scadaBaslamaSuresi = Time.time;
        Debug.Log("[ScadaGecisYonetici] Sinematik atlandi, SCADA odasina geciliyor.");
        GecisYap();
    }

    void GecisYap()
    {
        if (_gecisYapildi) return;

        _gecisYapildi = true;
        Debug.Log("[ScadaGecisYonetici] SCADA geçişi başlatılıyor...");

        // 1. Bileşen bilgi panelini gizle
        if (bilesenBilgileriCanvas != null)
            bilesenBilgileriCanvas.SetActive(false);

        // 2. Main Camera'yı FPS_Player'ın child'ı yap
        Camera mainCam = Camera.main;
        if (mainCam != null && fpsKontrol != null && _scadaVCam != null)
        {
            Vector3 scadaPos = _scadaVCam.transform.position;
            Vector3 scadaEuler = _scadaVCam.transform.eulerAngles;
            Vector3 oyuncuPos = new Vector3(scadaPos.x, scadaPos.y - gozYuksekligiOffset, scadaPos.z);

            if (scadaGuvenliZeminOlustur)
                ScadaZemininiHazirla(oyuncuPos);

            // FPS_Player'ı SCADA kamera pozisyonuna yerleştir (göz hizası çıkarılmış)
            var cc = fpsKontrol.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            fpsKontrol.transform.position = oyuncuPos;
            if (cc != null) cc.enabled = true;

            // CinemachineBrain devre dışı bırak
            if (cinemachineBrain != null)
                cinemachineBrain.enabled = false;

            // Kamerayı FPS_Player'ın child'ı yap
            mainCam.transform.SetParent(fpsKontrol.transform);
            mainCam.transform.localPosition = new Vector3(0f, gozYuksekligiOffset, 0f);
            mainCam.transform.localRotation = Quaternion.identity;

            // Kamera referansını güncelle
            fpsKontrol.kameraTransform = mainCam.transform;

            // FPS kontrolü aktif et
            fpsKontrol.enabled = true;

            // Başlangıç rotasyonunu SCADA kamerasından al
            float pitch = scadaEuler.x;
            if (pitch > 180f) pitch -= 360f; // 350° → -10° dönüşümü
            float yaw = scadaEuler.y;
            fpsKontrol.BaslangicRotasyonuAyarla(pitch, yaw);

            Debug.Log($"[ScadaGecisYonetici] Kamera re-parent edildi. Pos={scadaPos}, Pitch={pitch:F1}, Yaw={yaw:F1}");
        }
        else
        {
            // Fallback: eski yöntem
            if (cinemachineBrain != null)
                cinemachineBrain.enabled = false;
            if (fpsKontrol != null)
                fpsKontrol.enabled = true;
        }

        // 3. Terminali aç
        if (terminalCanvas != null)
        {
            terminalCanvas.SetActive(true);
            var pencere = terminalCanvas.GetComponent<TerminalPencereYonetici>();
            if (pencere != null)
                pencere.fpsKontrol = fpsKontrol;
            if (fpsKontrol != null && fpsKontrol.enabled)
                fpsKontrol.SetTerminalDurumu(true);
            Debug.Log("[ScadaGecisYonetici] Terminal açıldı.");
        }
    }

    void ScadaZemininiHazirla(Vector3 oyuncuPos)
    {
        const string zeminAdi = "SCADA_GuvenliZemin";
        var zemin = GameObject.Find(zeminAdi);
        if (zemin == null)
        {
            zemin = new GameObject(zeminAdi);
            zemin.AddComponent<BoxCollider>();
        }

        zemin.transform.position = new Vector3(oyuncuPos.x, oyuncuPos.y - 1.02f, oyuncuPos.z);
        zemin.transform.rotation = Quaternion.identity;
        zemin.transform.localScale = Vector3.one;

        var box = zemin.GetComponent<BoxCollider>();
        box.size = scadaZeminBoyutu;
        box.center = Vector3.zero;
        box.isTrigger = false;
    }

    /// <summary>Editor / test için geçişi zorla tetikle.</summary>
    [ContextMenu("Geçişi Zorla Tetikle")]
    public void GecisiZorlaTestEt()
    {
        _gecisYapildi = false;
        _scadaKamerasiAktifti = true;
        _scadaBaslamaSuresi = 0f;
        GecisYap();
    }
}
