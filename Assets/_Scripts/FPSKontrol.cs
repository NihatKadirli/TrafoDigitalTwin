using UnityEngine;

/// <summary>
/// SCADA odasına geçildikten sonra aktif olan WASD + Mouse Look FPS kontrolcü.
/// Terminal açıkken mouse serbest bırakılır, WASD devre dışı kalır.
/// </summary>
public class FPSKontrol : MonoBehaviour
{
    [Header("Hareket")]
    public float hareketHizi = 5f;
    public float kosmaHizCarpani = 2f;

    [Header("Kamera Bakış")]
    public float fareHassasiyeti = 2f;
    public float maxDikeyAci = 80f;

    [Header("SCADA Zemin Koruma")]
    [Tooltip("SCADA odasinda oyuncunun altina zemin bulunamazsa dusmeyi engelleyen minimum yukseklik.")]
    public float minimumYukseklik = 1f;

    [Tooltip("Zemin algilama mesafesi. Bu mesafede collider varsa oyuncu zemine oturtulur.")]
    public float zeminKontrolMesafesi = 1.6f;

    [Header("Referanslar")]
    [Tooltip("Kamera Transform'u (Main Camera). Boş bırakılırsa Camera.main kullanılır.")]
    public Transform kameraTransform;

    // ── Dahili ──
    private float _xRotasyon = 0f;
    private bool _terminalAcik = false;
    private CharacterController _cc;
    private float _dikeyHiz = 0f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        // Başlangıçta pasif — ScadaGecisYonetici aktif edecek
        enabled = false;
    }

    void OnEnable()
    {
        // Kamera referansını güncelle (re-parent sonrası)
        if (kameraTransform == null && Camera.main != null)
            kameraTransform = Camera.main.transform;

        if (!_terminalAcik)
            KilidiBas();
    }

    void OnDisable()
    {
        KilidiAc();
    }

    /// <summary>
    /// ScadaGecisYonetici tarafından çağrılır.
    /// Kamera re-parent edildikten sonra başlangıç açılarını ayarlar.
    /// </summary>
    public void BaslangicRotasyonuAyarla(float dikeyAci, float yatayAci)
    {
        _dikeyHiz = 0f;
        _xRotasyon = dikeyAci;
        // Karakterin yatay rotasyonunu ayarla
        transform.rotation = Quaternion.Euler(0f, yatayAci, 0f);
        // Kameranın dikey rotasyonunu ayarla
        if (kameraTransform != null)
            kameraTransform.localRotation = Quaternion.Euler(_xRotasyon, 0f, 0f);

        Debug.Log($"[FPSKontrol] Başlangıç rotasyonu: pitch={dikeyAci:F1}, yaw={yatayAci:F1}");
    }

    void Update()
    {
        if (!_terminalAcik)
        {
            MouseBakis();
            WasdHareket();
        }
    }

    // ─── Mouse Bakış ───────────────────────────────────────────────
    void MouseBakis()
    {
        if (kameraTransform == null) return;

        float mouseX = Input.GetAxis("Mouse X") * fareHassasiyeti;
        float mouseY = Input.GetAxis("Mouse Y") * fareHassasiyeti;

        _xRotasyon -= mouseY;
        _xRotasyon = Mathf.Clamp(_xRotasyon, -maxDikeyAci, maxDikeyAci);

        // Kamera dikey döner (local X), karakter yatay döner (world Y)
        kameraTransform.localRotation = Quaternion.Euler(_xRotasyon, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    // ─── WASD Hareket ──────────────────────────────────────────────
    void WasdHareket()
    {
        float hiz = Input.GetKey(KeyCode.LeftShift) ? hareketHizi * kosmaHizCarpani : hareketHizi;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 yon = (transform.right * x + transform.forward * z).normalized;

        if (_cc != null)
        {
            if (_cc.isGrounded && _dikeyHiz < 0f)
                _dikeyHiz = -1f;

            if (!_cc.isGrounded)
                _dikeyHiz += Physics.gravity.y * Time.deltaTime;

            Vector3 hareket = (yon * hiz) + (Vector3.up * _dikeyHiz);
            hareket *= Time.deltaTime;
            _cc.Move(hareket);
            ZemindenDusmeyiOnle();
        }
        else
        {
            transform.position += yon * hiz * Time.deltaTime;
        }
    }

    void ZemindenDusmeyiOnle()
    {
        if (transform.position.y < minimumYukseklik)
        {
            Vector3 pos = transform.position;
            pos.y = minimumYukseklik;
            transform.position = pos;
            _dikeyHiz = 0f;
            return;
        }

        if (_cc == null) return;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, zeminKontrolMesafesi, ~0, QueryTriggerInteraction.Ignore))
        {
            float hedefY = hit.point.y + (_cc.height * 0.5f) - _cc.center.y + 0.02f;
            if (transform.position.y < hedefY)
            {
                Vector3 pos = transform.position;
                pos.y = hedefY;
                transform.position = pos;
                _dikeyHiz = 0f;
            }
        }
    }

    // ─── Terminal Entegrasyonu ──────────────────────────────────────
    /// <summary>TerminalPencereYonetici çağırır.</summary>
    public void SetTerminalDurumu(bool acik)
    {
        _terminalAcik = acik;
        if (acik)
            KilidiAc();
        else
            KilidiBas();
    }

    void KilidiBas()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void KilidiAc()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
