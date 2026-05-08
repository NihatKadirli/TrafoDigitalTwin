using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Terminal UI'ını runtime'da oluşturur. Modern dark theme (1920x1080).
/// Terminal_Canvas objesine eklenir. Awake'te tüm UI elemanlarını oluşturur.
/// </summary>
public class TerminalUIBuilder : MonoBehaviour
{
    // ── Renkler ──
    static readonly Color COL_BG       = new Color(0.035f, 0.043f, 0.055f, 0.98f);
    static readonly Color COL_TITLE    = new Color(0.02f, 0.025f, 0.035f, 1f);
    static readonly Color COL_OUTPUT   = new Color(0.018f, 0.028f, 0.026f, 0.96f);
    static readonly Color COL_INPUT    = new Color(0.025f, 0.032f, 0.042f, 1f);
    static readonly Color COL_STATUS   = new Color(0.035f, 0.043f, 0.055f, 1f);
    static readonly Color COL_ACCENT   = new Color(0.05f, 0.82f, 0.62f, 1f);
    static readonly Color COL_TEXT     = new Color(0.82f, 0.88f, 0.90f, 1f);
    static readonly Color COL_DIM      = new Color(0.50f, 0.58f, 0.62f, 1f);
    static readonly Color COL_WARN_BG  = new Color(0.72f, 0.08f, 0.08f, 0.95f);
    static readonly Color COL_BTN      = new Color(0.12f, 0.15f, 0.17f, 1f);
    static readonly Color COL_BTN_SEND = new Color(0.04f, 0.62f, 0.45f, 1f);
    static readonly Color COL_SCROLL   = new Color(0.25f, 0.27f, 0.30f, 0.6f);

    void Awake()
    {
        SetupCanvas();
        var window = BuildWindow();
        var titleBar = BuildTitleBar(window);
        var contentArea = BuildContentArea(window);
        var outputScroll = BuildOutputArea(contentArea);
        var statusBar = BuildStatusBar(contentArea.GetComponent<RectTransform>());
        var inputArea = BuildInputArea(contentArea);
        var warningPanel = BuildWarningPanel(window);

        WireComponents(window, titleBar, contentArea, outputScroll, inputArea, statusBar, warningPanel);
    }

    void SetupCanvas()
    {
        var c = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        var s = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;

        if (!gameObject.GetComponent<GraphicRaycaster>())
            gameObject.AddComponent<GraphicRaycaster>();
    }

    // ─── WINDOW ────────────────────────────────────────────────
RectTransform BuildWindow()
    {
        var go = CreateUI("TerminalWindow", transform);
        var rt = go.GetComponent<RectTransform>();
        AddImage(go, COL_BG);
        go.AddComponent<RectMask2D>();
        rt.anchorMin = new Vector2(0.035f, 0.055f);
        rt.anchorMax = new Vector2(0.965f, 0.945f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(COL_ACCENT.r, COL_ACCENT.g, COL_ACCENT.b, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        return rt;
    }

    // ─── TITLE BAR ─────────────────────────────────────────────
RectTransform BuildTitleBar(RectTransform parent)
    {
        var go = CreateUI("TitleBar", parent);
        var rt = go.GetComponent<RectTransform>();
        AddImage(go, COL_TITLE);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 52f);

        var dot = CreateUI("Dot", go.transform);
        AddImage(dot, COL_ACCENT);
        var dotRT = dot.GetComponent<RectTransform>();
        dotRT.anchorMin = new Vector2(0f, 0.5f);
        dotRT.anchorMax = new Vector2(0f, 0.5f);
        dotRT.pivot = new Vector2(0f, 0.5f);
        dotRT.anchoredPosition = new Vector2(18f, 0f);
        dotRT.sizeDelta = new Vector2(10f, 30f);

        var title = CreateTMP("TitleLabel", go.transform,
            "SCADA KONTROL TERMINALI - TRAFO MERKEZI", 17, COL_TEXT, FontStyles.Bold);
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(38f, 0f);
        titleRT.offsetMax = new Vector2(-230f, 0f);

        var controls = CreateUI("WindowControls", go.transform);
        var controlsRT = controls.GetComponent<RectTransform>();
        controlsRT.anchorMin = new Vector2(1f, 0f);
        controlsRT.anchorMax = new Vector2(1f, 1f);
        controlsRT.pivot = new Vector2(1f, 0.5f);
        controlsRT.anchoredPosition = new Vector2(-16f, 0f);
        controlsRT.sizeDelta = new Vector2(210f, 0f);
        var controlsLayout = controls.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.spacing = 10;
        controlsLayout.padding = new RectOffset(0, 0, 0, 0);
        controlsLayout.childControlWidth = false;
        controlsLayout.childControlHeight = false;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = false;
        controlsLayout.childAlignment = TextAnchor.MiddleRight;

        var hint = CreateTMP("HintLabel", controls.transform, "F1 PANEL | F2 MINI", 11, COL_DIM);
        hint.overflowMode = TextOverflowModes.Ellipsis;
        AddLE(hint.gameObject, 116, 30);

        CreateTitleBtn(controls.transform, "MinBtn", "-");
        CreateTitleBtn(controls.transform, "MaxBtn", "x");

        return rt;
    }

Button CreateTitleBtn(Transform parent, string n, string label)
    {
        var go = CreateUI(n, parent);
        var img = AddImage(go, COL_BTN);
        AddLE(go, 32, 30);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.32f, 0.36f, 1f);
        colors.pressedColor = new Color(0.15f, 0.16f, 0.18f, 1f);
        btn.colors = colors;
        var labelText = CreateTMP("Label", go.transform, label, 16, COL_TEXT, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(labelText.GetComponent<RectTransform>());
        return btn;
    }

    // ─── CONTENT AREA ──────────────────────────────────────────
GameObject BuildContentArea(RectTransform parent)
    {
        var go = CreateUI("ContentArea", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(0f, -52f);
        return go;
    }

    // ─── OUTPUT AREA (ScrollRect) ──────────────────────────────
ScrollRect BuildOutputArea(GameObject parent)
    {
        var outputGO = CreateUI("OutputPanel", parent.transform);
        var outputRT = outputGO.GetComponent<RectTransform>();
        outputRT.anchorMin = Vector2.zero;
        outputRT.anchorMax = Vector2.one;
        outputRT.offsetMin = new Vector2(0f, 113f);
        outputRT.offsetMax = Vector2.zero;
        AddImage(outputGO, COL_OUTPUT);
        outputGO.AddComponent<RectMask2D>();

        var txt = CreateTMP("OutputText", outputGO.transform,
            "", 17, COL_TEXT, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        txt.enableWordWrapping = true;
        txt.overflowMode = TextOverflowModes.Truncate;
        txt.richText = true;
        txt.font = TMP_Settings.defaultFontAsset;
        txt.lineSpacing = 3f;
        txt.margin = new Vector4(20f, 18f, 20f, 18f);
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.pivot = new Vector2(0.5f, 0.5f);
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        return null;
    }

    // ─── INPUT AREA ────────────────────────────────────────────
GameObject BuildInputArea(GameObject parent)
    {
        var sep = CreateUI("Separator", parent.transform);
        AddImage(sep, new Color(COL_ACCENT.r, COL_ACCENT.g, COL_ACCENT.b, 0.22f));
        var sepRT = sep.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0f, 0f);
        sepRT.anchorMax = new Vector2(1f, 0f);
        sepRT.pivot = new Vector2(0.5f, 0f);
        sepRT.anchoredPosition = new Vector2(0f, 72f);
        sepRT.sizeDelta = new Vector2(0f, 1f);

        var row = CreateUI("InputArea", parent.transform);
        AddImage(row, COL_INPUT);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 0f);
        rowRT.anchorMax = new Vector2(1f, 0f);
        rowRT.pivot = new Vector2(0.5f, 0f);
        rowRT.anchoredPosition = Vector2.zero;
        rowRT.sizeDelta = new Vector2(0f, 72f);

        var prompt = CreateTMP("Prompt", row.transform, "scada>", 20, COL_ACCENT, FontStyles.Bold);
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        var promptRT = prompt.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0f, 0f);
        promptRT.anchorMax = new Vector2(0f, 1f);
        promptRT.pivot = new Vector2(0f, 0.5f);
        promptRT.anchoredPosition = new Vector2(24f, 0f);
        promptRT.sizeDelta = new Vector2(86f, 0f);

        var ifGO = CreateUI("TerminalInput", row.transform);
        var ifRT = ifGO.GetComponent<RectTransform>();
        ifRT.anchorMin = new Vector2(0f, 0f);
        ifRT.anchorMax = new Vector2(1f, 1f);
        ifRT.offsetMin = new Vector2(120f, 0f);
        ifRT.offsetMax = new Vector2(-148f, 0f);
        var ifImg = AddImage(ifGO, new Color(0.055f, 0.065f, 0.075f, 1f));

        var inputField = ifGO.AddComponent<TMP_InputField>();

        var textArea = CreateUI("Text Area", ifGO.transform);
        var taRT = textArea.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero;
        taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(14, 6);
        taRT.offsetMax = new Vector2(-14, -6);

        var ph = CreateTMP("Placeholder", textArea.transform,
            "komut girin: help, status, scan, attack fdi temp", 18, COL_DIM);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        var phRT = ph.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;

        var itxt = CreateTMP("Text", textArea.transform, "", 18, COL_TEXT);
        itxt.alignment = TextAlignmentOptions.MidlineLeft;
        var itRT = itxt.GetComponent<RectTransform>();
        itRT.anchorMin = Vector2.zero;
        itRT.anchorMax = Vector2.one;
        itRT.offsetMin = Vector2.zero;
        itRT.offsetMax = Vector2.zero;

        inputField.textViewport = taRT;
        inputField.textComponent = itxt;
        inputField.placeholder = ph;
        inputField.fontAsset = TMP_Settings.defaultFontAsset;
        inputField.pointSize = 18;
        inputField.caretColor = COL_ACCENT;
        inputField.selectionColor = new Color(COL_ACCENT.r, COL_ACCENT.g, COL_ACCENT.b, 0.25f);
        inputField.targetGraphic = ifImg;

        var sendGO = CreateUI("SendButton", row.transform);
        var sendRT = sendGO.GetComponent<RectTransform>();
        sendRT.anchorMin = new Vector2(1f, 0f);
        sendRT.anchorMax = new Vector2(1f, 1f);
        sendRT.pivot = new Vector2(1f, 0.5f);
        sendRT.anchoredPosition = new Vector2(-20f, 0f);
        sendRT.sizeDelta = new Vector2(124f, 0f);
        var sendImg = AddImage(sendGO, COL_BTN_SEND);
        var sendBtn = sendGO.AddComponent<Button>();
        sendBtn.targetGraphic = sendImg;
        var sc = sendBtn.colors;
        sc.highlightedColor = new Color(0f, 0.75f, 0.63f, 1f);
        sc.pressedColor = new Color(0f, 0.5f, 0.42f, 1f);
        sendBtn.colors = sc;
        var sendLabel = CreateTMP("SendLabel", sendGO.transform, "CALISTIR", 15, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(sendLabel.GetComponent<RectTransform>());

        return row;
    }

    // ─── STATUS BAR ────────────────────────────────────────────
RectTransform BuildStatusBar(RectTransform parent)
    {
        var go = CreateUI("StatusBarArea", parent);
        AddImage(go, COL_STATUS);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 73f);
        rt.sizeDelta = new Vector2(0f, 40f);

        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(20, 20, 4, 4);
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        var stxt = CreateTMP("StatusBar", go.transform,
            "TEMP -- C  |  VOLT -- kV  |  LOAD -- %  |  MQTT BEKLENIYOR", 14, COL_DIM);
        var statusLE = AddLE(stxt.gameObject, -1, 32);
        statusLE.flexibleWidth = 1;

        return go.GetComponent<RectTransform>();
    }

    // ─── WARNING PANEL ─────────────────────────────────────────
GameObject BuildWarningPanel(RectTransform parent)
    {
        var go = CreateUI("WarningPanel", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -68);
        rt.sizeDelta = new Vector2(-64, 54);

        AddImage(go, COL_WARN_BG);
        go.AddComponent<Outline>().effectColor = new Color(1, 0.3f, 0.3f, 0.5f);

        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(18, 18, 7, 7);
        hl.childAlignment = TextAnchor.MiddleCenter;

        CreateTMP("WarningText", go.transform, "", 17, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center);

        go.SetActive(false);
        return go;
    }

    // ─── WIRE COMPONENTS ───────────────────────────────────────
void WireComponents(RectTransform window, RectTransform titleBar,
        GameObject contentArea, ScrollRect scrollRect,
        GameObject inputArea, RectTransform statusBar, GameObject warningPanel)
    {
        var tc = gameObject.GetComponent<TerminalController>() ?? gameObject.AddComponent<TerminalController>();
        tc.OutputText = window.GetComponentInChildren<ScrollRect>()
            ?.content?.GetComponentInChildren<TMP_Text>();
        tc.InputField = inputArea.GetComponentInChildren<TMP_InputField>(true);
        tc.StatusBar = statusBar.GetComponentInChildren<TMP_Text>();
        tc.WarningPanel = warningPanel;
        tc.SendButton = inputArea.GetComponentInChildren<Button>(true);

        var tpy = gameObject.GetComponent<TerminalPencereYonetici>() ?? gameObject.AddComponent<TerminalPencereYonetici>();
        tpy.terminalPencere = window;
        tpy.icerikAlani = contentArea;
        tpy.durumCubugu = statusBar.gameObject;
        tpy.uyariPaneli = warningPanel;

        var controls = titleBar.Find("WindowControls");
        var minBtn = controls != null ? controls.Find("MinBtn")?.GetComponent<Button>() : titleBar.Find("MinBtn")?.GetComponent<Button>();
        var maxBtn = controls != null ? controls.Find("MaxBtn")?.GetComponent<Button>() : titleBar.Find("MaxBtn")?.GetComponent<Button>();
        if (minBtn != null)
            minBtn.onClick.AddListener(() => tpy.KucultButonTiklandi());
        if (maxBtn != null)
            maxBtn.onClick.AddListener(() => tpy.ToggleButonTiklandi());
    }

    // ─── HELPERS ───────────────────────────────────────────────
    GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI layer
        return go;
    }

    Image AddImage(GameObject go, Color c)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    LayoutElement AddLE(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (w > 0) le.preferredWidth = w;
        if (h > 0) le.preferredHeight = h;
        return le;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    TMP_Text CreateTMP(string name, Transform parent, string text, float size,
        Color color, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = CreateUI(name, parent);
        go.AddComponent<CanvasRenderer>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }
}
