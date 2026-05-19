using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

/// <summary>
/// Aktif Cinemachine sanal kamerasını takip eder ve
/// karşılık gelen bileşen bilgi metnini gösterir.
/// </summary>
public class BilesenBilgiYonetici : MonoBehaviour
{
    [Header("Ana Kamera (CinemachineBrain)")]
    [Tooltip("Sahnedeki Main Camera objesi (CinemachineBrain bileşeni olan)")]
    public CinemachineBrain cinemachineBrain;

    [Header("Bileşen Bilgi Paneli")]
    [Tooltip("'Bilesen_Bilgileri' Canvas objesi")]
    public GameObject bilesenBilgileriCanvas;

    // --- VCam → Text eşleştirme tablosu ---
    // Inspector'dan sürükle-bırak ile de doldurabilirsiniz.
    // Script, "vCam_XXX" ve "Txt_XXX" isim eşleşmesiyle otomatik kurar.
    [System.Serializable]
    public class VCamTextEslestirme
    {
        [Tooltip("Sanal kamera (CinemachineCamera bileşeni olan GameObject)")]
        public GameObject vCam;
        [Tooltip("Bu kamera aktifken gösterilecek Text GameObject'i")]
        public GameObject bilgiText;
    }

    [Header("VCam - Text Eşleştirmeleri")]
    [Tooltip("Boş bırakırsanız script otomatik eşleştirir (vCam_XXX → Txt_XXX)")]
    public List<VCamTextEslestirme> eslestirmeler = new List<VCamTextEslestirme>();

    // İç kullanım için Dictionary
    private Dictionary<CinemachineCamera, GameObject> _vcamTextMap
        = new Dictionary<CinemachineCamera, GameObject>();

    private CinemachineCamera _oncekiAktifVCam = null;

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        // Brain yoksa otomatik bul
        if (cinemachineBrain == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
                cinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
        }

        if (cinemachineBrain == null)
        {
            Debug.LogError("[BilesenBilgiYonetici] CinemachineBrain bulunamadı!");
            return;
        }

        // Canvas yoksa otomatik bul
        if (bilesenBilgileriCanvas == null)
            bilesenBilgileriCanvas = GameObject.Find("Bilesen_Bilgileri");

        // Eşleştirme tablosunu kur
        KurVCamTextHaritasi();

        // Başlangıçta hepsini gizle
        HepsiniGizle();

        // Başlangıçtaki aktif kamerayı göster
        GuncelleBilgiPaneli();
    }

    // ─────────────────────────────────────────────────────────
    void Update()
    {
        GuncelleBilgiPaneli();
    }

    // ─────────────────────────────────────────────────────────
    /// <summary>
    /// Inspector'da el ile doldurulmuş listeyi kullan;
    /// liste boşsa isim eşleştirmesiyle otomatik kur.
    /// </summary>
    void KurVCamTextHaritasi()
    {
        _vcamTextMap.Clear();

        if (eslestirmeler != null && eslestirmeler.Count > 0)
        {
            // Inspector'dan elle doldurulmuş
            foreach (var e in eslestirmeler)
            {
                if (e.vCam == null || e.bilgiText == null) continue;

                var vcam = e.vCam.GetComponent<CinemachineCamera>();
                if (vcam != null)
                    _vcamTextMap[vcam] = e.bilgiText;
            }
            Debug.Log($"[BilesenBilgiYonetici] {_vcamTextMap.Count} eşleştirme yüklendi (manual).");
        }
        else
        {
            // Otomatik: sahnedeki tüm vCam_XXX ve Txt_XXX objelerini eşleştir
            var tumVCamlar = FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var vcam in tumVCamlar)
            {
                // vCam_DisAlan → DisAlan → Txt_DisAlan
                string goName = vcam.gameObject.name; // örn: vCam_DisAlan
                if (!goName.StartsWith("vCam_")) continue;

                string suffix = goName.Substring(5); // "DisAlan"
                string txtName = "Txt_" + suffix;    // "Txt_DisAlan"

                // Canvas içinde ara
                GameObject txtObj = null;
                if (bilesenBilgileriCanvas != null)
                {
                    var t = bilesenBilgileriCanvas.transform.Find(txtName);
                    if (t != null) txtObj = t.gameObject;
                }

                // Genel sahnede de ara (bulunamazsa)
                if (txtObj == null)
                    txtObj = GameObject.Find(txtName);

                if (txtObj != null)
                {
                    _vcamTextMap[vcam] = txtObj;
                    Debug.Log($"[BilesenBilgiYonetici] Eşleşti: {goName} → {txtName}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[BilesenBilgiYonetici] '{txtName}' bulunamadı! ({goName} için metin yok)");
                }
            }

            // Özel durum: vCam_icIhtiyacTransformatoru → Txt_iclhtiyacTransformatoru
            // (isim tutarsızlığı varsa aşağıya ekle)
            TryAddOverride("vCam_icIhtiyacTransformatoru", "Txt_iclhtiyacTransformatoru");

            Debug.Log($"[BilesenBilgiYonetici] {_vcamTextMap.Count} eşleştirme tamamlandı (auto).");
        }
    }

    /// <summary>
    /// İsim tutarsızlığı olan özel vakalar için override ekler.
    /// </summary>
    void TryAddOverride(string vcamName, string txtName)
    {
        var vcamGO = GameObject.Find(vcamName);
        if (vcamGO == null) return;

        var vcam = vcamGO.GetComponent<CinemachineCamera>();
        if (vcam == null) return;
        if (_vcamTextMap.ContainsKey(vcam)) return; // zaten ekli

        GameObject txtObj = null;
        if (bilesenBilgileriCanvas != null)
        {
            var t = bilesenBilgileriCanvas.transform.Find(txtName);
            if (t != null) txtObj = t.gameObject;
        }
        if (txtObj == null) txtObj = GameObject.Find(txtName);

        if (txtObj != null)
        {
            _vcamTextMap[vcam] = txtObj;
            Debug.Log($"[BilesenBilgiYonetici] Override eklendi: {vcamName} → {txtName}");
        }
    }

    // ─────────────────────────────────────────────────────────
    void GuncelleBilgiPaneli()
    {
        if (cinemachineBrain == null) return;

        // Şu an aktif olan sanal kamera
        var aktifVCam = cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;

        // Değişim olmadıysa güncelleme yapma
        if (aktifVCam == _oncekiAktifVCam) return;

        _oncekiAktifVCam = aktifVCam;

        HepsiniGizle();

        if (aktifVCam != null && _vcamTextMap.TryGetValue(aktifVCam, out GameObject metin))
        {
            metin.SetActive(true);
            Debug.Log($"[BilesenBilgiYonetici] Gösteriliyor: {metin.name} ({aktifVCam.name})");
        }
        else if (aktifVCam != null)
        {
            Debug.LogWarning(
                $"[BilesenBilgiYonetici] Aktif vcam için metin bulunamadı: {aktifVCam.name}");
        }
    }

    // ─────────────────────────────────────────────────────────
    void HepsiniGizle()
    {
        foreach (var kvp in _vcamTextMap)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }
    }
}
