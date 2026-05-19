
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor aracı: Bilesen_Bilgileri altındaki tüm Text objelerini
/// doğru konuma, boyuta ve içeriğe sahip Info Panel'e dönüştürür.
/// </summary>
public class BilesenBilgiDuzenleyici : MonoBehaviour
{
    [MenuItem("Trafo/Bileşen Bilgilerini Düzenle")]
    public static void DuzenleHepsini()
    {
        var canvas = GameObject.Find("Bilesen_Bilgileri");
        if (canvas == null) { Debug.LogError("Bilesen_Bilgileri bulunamadı!"); return; }

        // Canvas Scaler ayarla
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Canvas RectTransform sıfırla (full-screen stretch)
        var rt = canvas.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
        }

        // Her bileşen için bilgi ve renk
        var bilgiler = new System.Collections.Generic.Dictionary<string, (string baslik, string icerik, Color renk)>
        {
            ["Txt_DisAlan"] = (
                "DIŞ ALAN",
                "Trafo merkezinin açık hava bölümüdür.\n\n" +
                "• Yüksek gerilim ekipmanları barındırır\n" +
                "• 154 kV veya 380 kV hatlarla beslenir\n" +
                "• Topraklama sistemi ile korunur\n" +
                "• İzolasyon mesafeleri standartlara uygun",
                new Color(0.2f, 0.6f, 1f)
            ),
            ["Txt_GirisKapi"] = (
                "GİRİŞ KAPISI",
                "Trafo merkezine yetkili erişimi kontrol eder.\n\n" +
                "• Elektromanyetik kilit sistemi\n" +
                "• Kamera ve güvenlik sistemi entegrasyonu\n" +
                "• İzinsiz girişe karşı alarm mekanizması\n" +
                "• 24 saat güvenlik izleme",
                new Color(0.3f, 0.8f, 0.4f)
            ),
            ["Txt_GenlesmeTanki"] = (
                "GENLEŞME TANKI",
                "Trafo yağının ısıl genleşmesini karşılar.\n\n" +
                "• Trafo gövdesinin üstüne monte edilir\n" +
                "• Yağ seviyesi göstergesi içerir\n" +
                "• Nefes alma (silika-jel) filtresi\n" +
                "• Yağ-hava temasını minimize eder\n" +
                "• Basınç taşma ventili",
                new Color(1f, 0.6f, 0.1f)
            ),
            ["Txt_Busingler"] = (
                "BUSİNGLER (GEÇİŞ İZOLATÖRÜ)",
                "Trafo bobinleri ile dış şebeke arasındaki bağlantıyı sağlar.\n\n" +
                "• Yüksek voltaj altında izolasyon sağlar\n" +
                "• Porselene veya kompozit malzemeden üretilir\n" +
                "• Kirlilik sınıfına göre seçilir (I-IV)\n" +
                "• Nem ve kirden etkilenmeye karşı tasarımlanmış\n" +
                "• Kondansatör tipi izolasyon",
                new Color(1f, 0.3f, 0.3f)
            ),
            ["Txt_Anahtarlama"] = (
                "ANAHTARLAMA SAHASI",
                "Güç sisteminin anahtarlama ve koruma merkezi.\n\n" +
                "• Kesiciler ve ayırıcıları barındırır\n" +
                "• Bara sistemleri ile bağlantılıdır\n" +
                "• Çift bara veya tek bara düzeni\n" +
                "• Arıza akımlarını keserek sistemi korur\n" +
                "• Uzaktan ve manuel kontrol imkânı",
                new Color(0.8f, 0.3f, 1f)
            ),
            ["Txt_yuksekGerilimKesici"] = (
                "YÜKSEK GERİLİM KESİCİ",
                "Şebeke arızalarında devre açan anahtarlama cihazı.\n\n" +
                "• SF6 gazlı veya vakumlu tip\n" +
                "• Kısa devre akımını ms'de keser\n" +
                "• Otomatik yeniden kapama özelliği\n" +
                "• Röle sistemi ile koordineli çalışır\n" +
                "• Kısa devre kırıcı kapasitesi: 40-63 kA",
                new Color(1f, 0.2f, 0.5f)
            ),
            ["Txt_ayirici"] = (
                "AYIRICI (DİSCONNECT SWITCH)",
                "Devreyi gerilimden ayırmak için kullanılan mekanik anahtar.\n\n" +
                "• Yük altında açılmaz (kesiciden sonra)\n" +
                "• Görünür açık boşluk sağlar\n" +
                "• Topraklama bıçağı ile donatılmış\n" +
                "• Motorlu veya el ile kumanda\n" +
                "• Bakım güvenliğini sağlar",
                new Color(0.9f, 0.7f, 0.1f)
            ),
            ["Txt_TasiciBaraSistemi"] = (
                "TAŞIYICI BARA SİSTEMİ",
                "Trafo merkezindeki güç iletim omurgası.\n\n" +
                "• Bakır veya alüminyum bara\n" +
                "• 1250-4000 A sürekli akım kapasitesi\n" +
                "• İzolasyonlu veya açık bara tipi\n" +
                "• Topraklama koruması\n" +
                "• Termal genleşme kompansatörleri",
                new Color(0.2f, 0.9f, 0.7f)
            ),
            ["Txt_olcuTransformator"] = (
                "ÖLÇÜ TRANSFORMATÖRÜ",
                "Yüksek gerilim/akımı ölçüm sistemine uygun değerlere dönüştürür.\n\n" +
                "• Akım Transformatörü (CT): 5A veya 1A sekonder\n" +
                "• Gerilim Transformatörü (VT): 100V sekonder\n" +
                "• 0.2S-0.5 sınıf hassasiyeti\n" +
                "• Röle ve sayaç besleme\n" +
                "• IEC 61869 standardına uygun",
                new Color(0.4f, 0.8f, 1f)
            ),
            ["Txt_parafudr"] = (
                "PARAFUDR (GERİLİM SINIRLAYICI)",
                "Yıldırım ve anahtarlama aşırı gerilimlerinden sistemi korur.\n\n" +
                "• Metal oksit varistör (MOV) teknolojisi\n" +
                "• Gaz deşarj tüpü ile birlikte kullanılır\n" +
                "• Koruma seviyesi: Up < 2.5 × Un\n" +
                "• Her fazda ayrı montaj\n" +
                "• IEC 60099-4 standardına uygun",
                new Color(1f, 0.5f, 0.1f)
            ),
            ["Txt_iclhtiyacTransformatoru"] = (
                "İÇ İHTİYAÇ TRANSFORMATÖRÜ",
                "Trafo merkezinin kendi enerji ihtiyacını karşılar.\n\n" +
                "• Güç: 100-630 kVA\n" +
                "• Sekonder: 400 V / 3 faz\n" +
                "• Aydınlatma ve kontrol paneli besleme\n" +
                "• SCADA ve koruma cihazları için UPS\n" +
                "• Bakım ve işletme ekipmanları besleme",
                new Color(0.6f, 1f, 0.3f)
            ),
            ["Txt_sontReaktor"] = (
                "ŞÖNT REAKTÖR",
                "Uzun hatların kapasitif reaktif gücünü kompanse eder.\n\n" +
                "• Hat sonunda veya baraya bağlı\n" +
                "• Boşta çalışma kayıplarını azaltır\n" +
                "• Gerilim yükselmesini önler (Ferranti etkisi)\n" +
                "• Sabit veya değişken reaktans\n" +
                "• 10-100 MVAR kapasitesi",
                new Color(0.7f, 0.3f, 0.9f)
            ),
            ["Txt_ScadaOdasi"] = (
                "SCADA ODASI",
                "Tüm trafo merkezini izleyen ve kontrol eden merkez.\n\n" +
                "• Gerçek zamanlı veri izleme (RTU/IED)\n" +
                "• Uzaktan kesici açma/kapama\n" +
                "• Alarm yönetimi ve olay kaydı\n" +
                "• IEC 61850 protokol desteği\n" +
                "• Tarihsel veri depolama ve analiz\n" +
                "• Siber güvenlik katmanı",
                new Color(0.1f, 0.7f, 1f)
            ),
        };

        int islendi = 0;
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            var child = canvas.transform.GetChild(i);
            string goName = child.name;

            if (!bilgiler.ContainsKey(goName)) continue;

            var (baslik, icerik, renk) = bilgiler[goName];

            // Mevcut Text bileşeni kaldır, objeyi yeniden düzenle
            DuzenleTextObjesi(child.gameObject, baslik, icerik, renk);
            islendi++;
        }

        // Sahneyi kaydet
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[BilesenBilgiDuzenleyici] {islendi} text objesi düzenlendi.");
        EditorUtility.DisplayDialog("Tamamlandı",
            $"{islendi} bileşen bilgisi başarıyla düzenlendi!\nSahneyi kaydedin.", "Tamam");
    }

    static void DuzenleTextObjesi(GameObject go, string baslik, string icerik, Color renkVurgu)
    {
        // ── 1. RectTransform: Ekranın sol-alt köşesine, %35 genişlik, %50 yükseklik ──
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        // Sol alt köşe anker
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);

        // 1920x1080'de: x=40, y=40, genişlik=680, yükseklik=520
        rt.anchoredPosition = new Vector2(40f, 40f);
        rt.sizeDelta = new Vector2(680f, 520f);

        // ── 2. Eski bileşenleri ÖNCE temizle ──
        var eskiText = go.GetComponent<Text>();
        if (eskiText != null) Object.DestroyImmediate(eskiText);
        var eskiCR = go.GetComponent<CanvasRenderer>();
        // CanvasRenderer silme - sadece Image varsa gerek yok

        // ── 3. Arka plan (Image) ekle/güncelle ──
        var bg = go.GetComponent<Image>();
        if (bg == null) bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.12f, 0.88f);
        bg.raycastTarget = false;

        // Sol kenar renkli çizgi şeridi (child Image)
        GameObject serit = null;
        for (int k = 0; k < go.transform.childCount; k++)
        {
            if (go.transform.GetChild(k).name == "__Serit__")
            {
                serit = go.transform.GetChild(k).gameObject;
                break;
            }
        }
        if (serit == null)
        {
            serit = new GameObject("__Serit__");
            serit.transform.SetParent(go.transform, false);
        }
        var seritImg = serit.GetComponent<Image>();
        if (seritImg == null) seritImg = serit.AddComponent<Image>();
        seritImg.color = renkVurgu;
        seritImg.raycastTarget = false;
        var seritRt = serit.GetComponent<RectTransform>();
        seritRt.anchorMin = new Vector2(0f, 0f);
        seritRt.anchorMax = new Vector2(0f, 1f);
        seritRt.offsetMin = new Vector2(0f, 0f);
        seritRt.offsetMax = new Vector2(6f, 0f);
        seritRt.pivot = new Vector2(0f, 0.5f);

        // Başlık text
        GameObject baslikGO = null;
        for (int k = 0; k < go.transform.childCount; k++)
        {
            if (go.transform.GetChild(k).name == "__Baslik__")
            {
                baslikGO = go.transform.GetChild(k).gameObject;
                break;
            }
        }
        if (baslikGO == null)
        {
            baslikGO = new GameObject("__Baslik__");
            baslikGO.transform.SetParent(go.transform, false);
        }
        var baslikText = baslikGO.GetComponent<Text>();
        if (baslikText == null) baslikText = baslikGO.AddComponent<Text>();
        baslikText.text = baslik;
        baslikText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        baslikText.fontSize = 34;
        baslikText.fontStyle = FontStyle.Bold;
        baslikText.color = renkVurgu;
        baslikText.alignment = TextAnchor.MiddleLeft;
        baslikText.raycastTarget = false;
        var baslikRt = baslikGO.GetComponent<RectTransform>();
        baslikRt.anchorMin = new Vector2(0f, 1f);
        baslikRt.anchorMax = new Vector2(1f, 1f);
        baslikRt.pivot = new Vector2(0.5f, 1f);
        baslikRt.offsetMin = new Vector2(24f, -80f);
        baslikRt.offsetMax = new Vector2(-16f, -16f);

        // İçerik text
        GameObject icerikGO = null;
        for (int k = 0; k < go.transform.childCount; k++)
        {
            if (go.transform.GetChild(k).name == "__Icerik__")
            {
                icerikGO = go.transform.GetChild(k).gameObject;
                break;
            }
        }
        if (icerikGO == null)
        {
            icerikGO = new GameObject("__Icerik__");
            icerikGO.transform.SetParent(go.transform, false);
        }
        var icerikText = icerikGO.GetComponent<Text>();
        if (icerikText == null) icerikText = icerikGO.AddComponent<Text>();
        icerikText.text = icerik;
        icerikText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        icerikText.fontSize = 22;
        icerikText.fontStyle = FontStyle.Normal;
        icerikText.color = new Color(0.88f, 0.92f, 0.98f, 1f);
        icerikText.alignment = TextAnchor.UpperLeft;
        icerikText.lineSpacing = 1.35f;
        icerikText.raycastTarget = false;
        var icerikRt = icerikGO.GetComponent<RectTransform>();
        icerikRt.anchorMin = new Vector2(0f, 0f);
        icerikRt.anchorMax = new Vector2(1f, 1f);
        icerikRt.pivot = new Vector2(0.5f, 0.5f);
        icerikRt.offsetMin = new Vector2(24f, 16f);
        icerikRt.offsetMax = new Vector2(-16f, -88f);

        // Ayırıcı çizgi
        GameObject cizgi = null;
        for (int k = 0; k < go.transform.childCount; k++)
        {
            if (go.transform.GetChild(k).name == "__Cizgi__")
            {
                cizgi = go.transform.GetChild(k).gameObject;
                break;
            }
        }
        if (cizgi == null)
        {
            cizgi = new GameObject("__Cizgi__");
            cizgi.transform.SetParent(go.transform, false);
        }
        var cizgiImg = cizgi.GetComponent<Image>();
        if (cizgiImg == null) cizgiImg = cizgi.AddComponent<Image>();
        cizgiImg.color = new Color(renkVurgu.r, renkVurgu.g, renkVurgu.b, 0.4f);
        cizgiImg.raycastTarget = false;
        var cizgiRt = cizgi.GetComponent<RectTransform>();
        cizgiRt.anchorMin = new Vector2(0f, 1f);
        cizgiRt.anchorMax = new Vector2(1f, 1f);
        cizgiRt.pivot = new Vector2(0.5f, 1f);
        cizgiRt.offsetMin = new Vector2(16f, -88f);
        cizgiRt.offsetMax = new Vector2(-16f, -85f);
    }
}
#endif
