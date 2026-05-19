using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerminalUIBuilder : MonoBehaviour
{
    static readonly Color ColBackground = new Color(0.025f, 0.031f, 0.040f, 0.98f);
    static readonly Color ColPanel = new Color(0.045f, 0.056f, 0.070f, 0.98f);
    static readonly Color ColPanelDark = new Color(0.026f, 0.034f, 0.044f, 1f);
    static readonly Color ColTitle = new Color(0.015f, 0.020f, 0.028f, 1f);
    static readonly Color ColLine = new Color(0.18f, 0.64f, 0.82f, 0.65f);
    static readonly Color ColGoose = new Color(1f, 0.76f, 0.24f, 0.75f);
    static readonly Color ColText = new Color(0.82f, 0.88f, 0.90f, 1f);
    static readonly Color ColDim = new Color(0.48f, 0.56f, 0.61f, 1f);
    static readonly Color ColAccent = new Color(0.05f, 0.82f, 0.62f, 1f);
    static readonly Color ColInput = new Color(0.030f, 0.039f, 0.050f, 1f);
    static readonly Color ColWarning = new Color(0.72f, 0.08f, 0.08f, 0.96f);
    static readonly Color ColAlarmPanel = new Color(0.070f, 0.034f, 0.038f, 0.98f);

    readonly List<SCADAHMIController.ComponentStatusBinding> componentBindings =
        new List<SCADAHMIController.ComponentStatusBinding>();

    RectTransform terminalWindow;
    GameObject bodyArea;
    GameObject warningPanel;
    Button minButton;
    Button toggleButton;

    TMP_Text systemModeText;
    TMP_Text protocolText;
    TMP_Text clockText;
    TMP_Text statusBarText;
    TMP_Text outputText;
    TMP_InputField inputField;
    Button sendButton;
    ScrollRect outputScrollRect;

    TMP_Text iedCurrentText;
    TMP_Text iedThresholdText;
    TMP_Text iedTripStatusText;
    TMP_Text iedStNumText;
    TMP_Text iedSqNumText;
    TMP_Text iedLastGooseText;
    TMP_Text iedAttackText;
    Image iedIndicator;

    TMP_Text breakerStateText;
    TMP_Text breakerDetailText;
    TMP_Text breakerCommandSourceText;
    TMP_Text breakerLastCommandTimeText;
    Image breakerStateIndicator;
    Image breakerMimicIndicator;

    TMP_Text alarmListText;
    TMP_Text alarmCountText;
    Image alarmPanelImage;

    void Awake()
    {
        SetupCanvas();
        BuildScadaHmi();
        WireComponents();
    }

    void SetupCanvas()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void BuildScadaHmi()
    {
        GameObject windowGO = CreatePanel("TerminalWindow", transform, ColBackground);
        terminalWindow = windowGO.GetComponent<RectTransform>();
        SetAnchors(terminalWindow, new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.945f), Vector2.zero, Vector2.zero);
        terminalWindow.gameObject.AddComponent<RectMask2D>();
        AddOutline(terminalWindow.gameObject, new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.32f), new Vector2(2f, -2f));

        RectTransform titleBar = BuildTitleBar(terminalWindow);

        bodyArea = CreateUI("SCADABody", terminalWindow);
        RectTransform bodyRT = bodyArea.GetComponent<RectTransform>();
        SetAnchors(bodyRT, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -54f));

        BuildHeader(bodyRT);
        BuildMainArea(bodyRT);
        BuildConsole(bodyRT);
        warningPanel = BuildWarningPanel(terminalWindow);

        Transform controls = titleBar.Find("WindowControls");
        if (controls != null)
        {
            minButton = controls.Find("MinBtn")?.GetComponent<Button>();
            toggleButton = controls.Find("ToggleBtn")?.GetComponent<Button>();
        }
    }

    RectTransform BuildTitleBar(RectTransform parent)
    {
        GameObject bar = CreateUI("TitleBar", parent);
        RectTransform rt = bar.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 54f);
        AddImage(bar, ColTitle);

        TMP_Text title = CreateTMP("TitleLabel", bar.transform,
            "SCADA/HMI - TRAFO MERKEZI DIJITAL IKIZI", 18, ColText, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetAnchors(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(42f, 0f), new Vector2(-330f, 0f));

        GameObject accent = CreateUI("Accent", bar.transform);
        AddImage(accent, ColAccent);
        RectTransform accentRT = accent.GetComponent<RectTransform>();
        SetAnchors(accentRT, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        accentRT.pivot = new Vector2(0f, 0.5f);
        accentRT.anchoredPosition = new Vector2(18f, 0f);
        accentRT.sizeDelta = new Vector2(10f, 32f);

        GameObject controls = CreateUI("WindowControls", bar.transform);
        RectTransform controlsRT = controls.GetComponent<RectTransform>();
        SetAnchors(controlsRT, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        controlsRT.pivot = new Vector2(1f, 0.5f);
        controlsRT.anchoredPosition = new Vector2(-16f, 0f);
        controlsRT.sizeDelta = new Vector2(304f, 0f);

        HorizontalLayoutGroup layout = controls.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text hint = CreateTMP("HintLabel", controls.transform, "F1 PANEL | F2 MINI", 11, ColDim);
        hint.alignment = TextAlignmentOptions.MidlineRight;
        AddLayout(hint.gameObject, 150f, 30f);

        CreateTitleButton(controls.transform, "MinBtn", "-");
        CreateTitleButton(controls.transform, "ToggleBtn", "x");

        return rt;
    }

    void BuildHeader(RectTransform parent)
    {
        GameObject header = CreatePanel("SCADAHeader", parent, ColPanelDark);
        RectTransform rt = header.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(0f, 0.925f), new Vector2(1f, 1f), new Vector2(16f, 8f), new Vector2(-16f, -10f));

        HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 7, 7);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        systemModeText = CreateTMP("SystemMode", header.transform, "NORMAL OPERATION", 18, SCADAHMIController.NormalColor, FontStyles.Bold);
        AddLayout(systemModeText.gameObject, 240f, 34f);

        protocolText = CreateTMP("ProtocolText", header.transform, "IEC 61850 MMS monitored | GOOSE protection event simulation", 13, ColDim);
        protocolText.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement protocolLayout = AddLayout(protocolText.gameObject, -1f, 34f);
        protocolLayout.flexibleWidth = 1f;

        clockText = CreateTMP("ClockText", header.transform, "--:--:--", 18, ColAccent, FontStyles.Bold);
        clockText.alignment = TextAlignmentOptions.MidlineRight;
        AddLayout(clockText.gameObject, 120f, 34f);
    }

    void BuildMainArea(RectTransform parent)
    {
        GameObject main = CreateUI("MainArea", parent);
        RectTransform mainRT = main.GetComponent<RectTransform>();
        SetAnchors(mainRT, new Vector2(0f, 0.315f), new Vector2(1f, 0.925f), new Vector2(16f, 8f), new Vector2(-16f, -8f));

        GameObject mimic = CreatePanel("SubstationMimic", main.transform, ColPanel);
        RectTransform mimicRT = mimic.GetComponent<RectTransform>();
        SetAnchors(mimicRT, Vector2.zero, new Vector2(0.655f, 1f), Vector2.zero, new Vector2(-8f, 0f));
        BuildMimicDiagram(mimicRT);

        GameObject side = CreateUI("RightPanels", main.transform);
        RectTransform sideRT = side.GetComponent<RectTransform>();
        SetAnchors(sideRT, new Vector2(0.655f, 0f), Vector2.one, new Vector2(8f, 0f), Vector2.zero);
        BuildRightPanels(sideRT);
    }

    void BuildMimicDiagram(RectTransform parent)
    {
        TMP_Text title = CreateTMP("MimicTitle", parent, "SUBSTATION SINGLE LINE / IEC 61850 STATION BUS", 15, ColText, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(title.rectTransform, new Vector2(0f, 0.925f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, 0f));

        AddLine(parent, "Line_Transformer_CT", new Vector2(0.275f, 0.68f), new Vector2(0.340f, 0.68f), 3f, ColLine);
        AddLine(parent, "Line_CT_Busbar", new Vector2(0.485f, 0.635f), new Vector2(0.485f, 0.500f), 3f, ColLine);
        AddLine(parent, "Line_Busbar_Breaker", new Vector2(0.610f, 0.535f), new Vector2(0.670f, 0.535f), 3f, ColLine);
        AddLine(parent, "Line_CT_IED", new Vector2(0.595f, 0.735f), new Vector2(0.675f, 0.315f), 2f, ColGoose);
        AddLine(parent, "Line_IED_Breaker", new Vector2(0.790f, 0.405f), new Vector2(0.790f, 0.475f), 2f, ColGoose);
        AddLine(parent, "Line_IED_Switch", new Vector2(0.675f, 0.275f), new Vector2(0.595f, 0.275f), 2f, ColGoose);
        AddLine(parent, "Line_Switch_Server", new Vector2(0.340f, 0.255f), new Vector2(0.275f, 0.255f), 2f, ColLine);

        CreateComponentNode(parent, "Transformer", "Transformer", "T1 154/34.5 kV", new Vector2(0.035f, 0.565f), new Vector2(0.275f, 0.825f));
        CreateComponentNode(parent, "CT / Current Sensor", "CT / Current Sensor", "MMXU.A.phsA", new Vector2(0.340f, 0.690f), new Vector2(0.610f, 0.855f));
        CreateComponentNode(parent, "Busbar", "Busbar", "34.5 kV BUS-A", new Vector2(0.340f, 0.455f), new Vector2(0.610f, 0.620f));

        SCADAHMIController.ComponentStatusBinding breakerBinding =
            CreateComponentNode(parent, "Circuit Breaker", "Circuit Breaker", "XCBR1.Pos.stVal", new Vector2(0.670f, 0.455f), new Vector2(0.930f, 0.645f));
        breakerMimicIndicator = breakerBinding.statusLight;

        SCADAHMIController.ComponentStatusBinding iedBinding =
            CreateComponentNode(parent, "IED Protection Relay", "IED Protection Relay", "PTOC/PTRC GOOSE", new Vector2(0.670f, 0.175f), new Vector2(0.945f, 0.390f));
        iedIndicator = iedBinding.statusLight;

        CreateComponentNode(parent, "Network Switch", "Network Switch", "Station Bus VLAN-10", new Vector2(0.325f, 0.145f), new Vector2(0.610f, 0.325f));
        CreateComponentNode(parent, "SCADA Server", "SCADA Server", "HMI/MMS Client", new Vector2(0.035f, 0.145f), new Vector2(0.275f, 0.325f));
    }

    SCADAHMIController.ComponentStatusBinding CreateComponentNode(
        RectTransform parent,
        string componentName,
        string title,
        string detail,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject node = CreatePanel(componentName.Replace("/", "_"), parent, new Color(0.013f, 0.018f, 0.024f, 0.98f));
        RectTransform rt = node.GetComponent<RectTransform>();
        SetAnchors(rt, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddRectBorder(node, new Color(ColLine.r, ColLine.g, ColLine.b, 0.90f), 1.25f);

        VerticalLayoutGroup layout = node.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 2;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject row = CreateUI("Header", node.transform);
        AddLayout(row, -1f, 20f).flexibleWidth = 1f;
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        Image indicator = CreateIndicator(row.transform, "Indicator", SCADAHMIController.NormalColor, 9f);

        TMP_Text titleText = CreateTMP("Name", row.transform, title, 11, ColText, FontStyles.Bold);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 9f;
        titleText.fontSizeMax = 11f;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        AddLayout(titleText.gameObject, -1f, 18f).flexibleWidth = 1f;

        TMP_Text detailText = CreateTMP("Detail", node.transform, detail, 9, ColDim);
        detailText.alignment = TextAlignmentOptions.MidlineLeft;
        detailText.overflowMode = TextOverflowModes.Ellipsis;
        AddLayout(detailText.gameObject, -1f, 15f).flexibleWidth = 1f;

        TMP_Text status = CreateTMP("Status", node.transform, "NORMAL", 10, SCADAHMIController.NormalColor, FontStyles.Bold);
        status.alignment = TextAlignmentOptions.MidlineRight;
        status.enableAutoSizing = true;
        status.fontSizeMin = 9f;
        status.fontSizeMax = 10f;
        status.overflowMode = TextOverflowModes.Ellipsis;
        AddLayout(status.gameObject, -1f, 15f).flexibleWidth = 1f;

        SCADAHMIController.ComponentStatusBinding binding = new SCADAHMIController.ComponentStatusBinding
        {
            componentName = componentName,
            statusText = status,
            detailText = detailText,
            statusLight = indicator
        };
        componentBindings.Add(binding);
        return binding;
    }

    void BuildRightPanels(RectTransform parent)
    {
        BuildIEDPanel(parent);
        BuildBreakerPanel(parent);
        BuildAlarmPanel(parent);
    }

    void BuildIEDPanel(RectTransform parent)
    {
        GameObject panel = CreateTitledLayoutPanel(parent, "IEDPanel", "IED PROTECTION RELAY / GOOSE", new Vector2(0f, 0.585f), Vector2.one);
        iedCurrentText = AddDataRow(panel.transform, "Current Ampere", "420 A");
        iedThresholdText = AddDataRow(panel.transform, "Trip Threshold", "900 A");
        iedTripStatusText = AddDataRow(panel.transform, "Trip Status", "NO TRIP");
        iedStNumText = AddDataRow(panel.transform, "GOOSE stNum", "1");
        iedSqNumText = AddDataRow(panel.transform, "GOOSE sqNum", "0");
        iedAttackText = AddDataRow(panel.transform, "Security", "No attack");
    }

    void BuildBreakerPanel(RectTransform parent)
    {
        GameObject panel = CreateTitledLayoutPanel(parent, "BreakerPanel", "CIRCUIT BREAKER XCBR1", new Vector2(0f, 0.305f), new Vector2(1f, 0.565f));

        GameObject row = CreateUI("BreakerStateRow", panel.transform);
        AddLayout(row, -1f, 34f).flexibleWidth = 1f;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        breakerStateIndicator = CreateIndicator(row.transform, "BreakerStateLamp", SCADAHMIController.NormalColor, 20f);
        breakerStateText = CreateTMP("BreakerStateText", row.transform, "CLOSED", 22, SCADAHMIController.NormalColor, FontStyles.Bold);
        breakerStateText.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(breakerStateText.gameObject, -1f, 32f).flexibleWidth = 1f;

        breakerCommandSourceText = AddDataRow(panel.transform, "Command Source", "operator");
        breakerLastCommandTimeText = AddDataRow(panel.transform, "Last Command Time", "--");
        breakerDetailText = AddBlockRow(panel.transform, "Operation", "Initial closed state", 30f);
    }

    void BuildAlarmPanel(RectTransform parent)
    {
        GameObject panel = CreateTitledLayoutPanel(parent, "AlarmPanel", "ALARM PANEL", Vector2.zero, new Vector2(1f, 0.285f));
        alarmPanelImage = panel.GetComponent<Image>();
        if (alarmPanelImage != null)
            alarmPanelImage.color = ColAlarmPanel;

        GameObject countRow = CreateUI("AlarmCountRow", panel.transform);
        AddLayout(countRow, -1f, 20f).flexibleWidth = 1f;
        HorizontalLayoutGroup countLayout = countRow.AddComponent<HorizontalLayoutGroup>();
        countLayout.childAlignment = TextAnchor.MiddleLeft;
        countLayout.childControlWidth = true;
        countLayout.childControlHeight = true;
        countLayout.childForceExpandWidth = false;
        countLayout.childForceExpandHeight = false;

        TMP_Text label = CreateTMP("AlarmCountLabel", countRow.transform, "Active alarms", 11, ColDim);
        AddLayout(label.gameObject, -1f, 18f).flexibleWidth = 1f;

        alarmCountText = CreateTMP("AlarmCount", countRow.transform, "0", 15, ColAccent, FontStyles.Bold);
        alarmCountText.alignment = TextAlignmentOptions.MidlineRight;
        AddLayout(alarmCountText.gameObject, 42f, 18f);

        alarmListText = CreateTMP("AlarmList", panel.transform, "<color=#728087>No active alarms</color>", 11, ColText);
        alarmListText.richText = true;
        alarmListText.enableWordWrapping = true;
        alarmListText.overflowMode = TextOverflowModes.Truncate;
        LayoutElement alarmLayout = AddLayout(alarmListText.gameObject, -1f, 62f);
        alarmLayout.flexibleWidth = 1f;
        alarmLayout.flexibleHeight = 1f;
    }

    GameObject CreateTitledLayoutPanel(RectTransform parent, string name, string title, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = CreatePanel(name, parent, ColPanel);
        RectTransform rt = panel.GetComponent<RectTransform>();
        SetAnchors(rt, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddOutline(panel, new Color(ColLine.r, ColLine.g, ColLine.b, 0.20f), new Vector2(1f, -1f));

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 8, 8);
        layout.spacing = 4;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text header = CreateTMP("PanelTitle", panel.transform, title, 13, ColAccent, FontStyles.Bold);
        header.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(header.gameObject, -1f, 20f).flexibleWidth = 1f;
        return panel;
    }

    TMP_Text AddDataRow(Transform parent, string labelText, string valueText)
    {
        GameObject row = CreateUI(labelText.Replace(" ", "") + "Row", parent);
        AddLayout(row, -1f, 22f).flexibleWidth = 1f;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text label = CreateTMP("Label", row.transform, labelText, 10, ColDim);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(label.gameObject, 145f, 20f);

        TMP_Text value = CreateTMP("Value", row.transform, valueText, 11, ColText, FontStyles.Bold);
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.enableAutoSizing = true;
        value.fontSizeMin = 9f;
        value.fontSizeMax = 11f;
        value.overflowMode = TextOverflowModes.Ellipsis;
        AddLayout(value.gameObject, -1f, 20f).flexibleWidth = 1f;
        return value;
    }

    TMP_Text AddBlockRow(Transform parent, string labelText, string valueText, float height)
    {
        GameObject block = CreateUI(labelText.Replace(" ", "") + "Block", parent);
        AddImage(block, new Color(0.020f, 0.028f, 0.036f, 0.95f));
        AddLayout(block, -1f, height).flexibleWidth = 1f;

        VerticalLayoutGroup layout = block.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(9, 9, 4, 4);
        layout.spacing = 1;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text label = CreateTMP("Label", block.transform, labelText, 9, ColDim);
        AddLayout(label.gameObject, -1f, 12f).flexibleWidth = 1f;

        TMP_Text value = CreateTMP("Value", block.transform, valueText, 10, ColText, FontStyles.Bold);
        value.enableWordWrapping = true;
        value.overflowMode = TextOverflowModes.Ellipsis;
        AddLayout(value.gameObject, -1f, height - 18f).flexibleWidth = 1f;
        return value;
    }

    void BuildConsole(RectTransform parent)
    {
        GameObject console = CreatePanel("ConsolePanel", parent, ColPanelDark);
        RectTransform consoleRT = console.GetComponent<RectTransform>();
        SetAnchors(consoleRT, Vector2.zero, new Vector2(1f, 0.305f), new Vector2(16f, 14f), new Vector2(-16f, -6f));
        AddOutline(console, new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.20f), new Vector2(1f, -1f));

        GameObject titleRow = CreateUI("ConsoleTitleRow", console.transform);
        RectTransform titleRT = titleRow.GetComponent<RectTransform>();
        SetAnchors(titleRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, 0f), new Vector2(-14f, 0f));
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(0f, 34f);

        TMP_Text title = CreateTMP("ConsoleTitle", titleRow.transform, "SCADA Terminal / Command Console", 14, ColAccent, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(title.rectTransform, Vector2.zero, new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);

        statusBarText = CreateTMP("StatusBar", titleRow.transform, "IEC 61850 station bus ready", 12, ColDim);
        statusBarText.alignment = TextAlignmentOptions.MidlineRight;
        SetAnchors(statusBarText.rectTransform, new Vector2(0.42f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

        BuildOutputScroll(console.transform);
        BuildInputRow(console.transform);
    }

    void BuildOutputScroll(Transform parent)
    {
        GameObject scrollGO = CreateUI("OutputPanel", parent);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        SetAnchors(scrollRT, Vector2.zero, Vector2.one, new Vector2(12f, 58f), new Vector2(-12f, -38f));
        AddImage(scrollGO, new Color(0.014f, 0.021f, 0.026f, 0.98f));

        outputScrollRect = scrollGO.AddComponent<ScrollRect>();
        outputScrollRect.horizontal = false;
        outputScrollRect.vertical = true;
        outputScrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateUI("Viewport", scrollGO.transform);
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        SetAnchors(viewportRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.01f));
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        viewportImage.raycastTarget = false;

        GameObject content = CreateUI("Content", viewport.transform);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 220f);

        outputText = CreateTMP("OutputText", content.transform, "", 13, ColText);
        outputText.richText = true;
        outputText.enableWordWrapping = true;
        outputText.overflowMode = TextOverflowModes.Overflow;
        outputText.alignment = TextAlignmentOptions.TopLeft;
        outputText.lineSpacing = 2f;
        outputText.margin = new Vector4(12f, 10f, 12f, 10f);
        SetAnchors(outputText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        outputScrollRect.viewport = viewportRT;
        outputScrollRect.content = contentRT;
    }

    void BuildInputRow(Transform parent)
    {
        GameObject row = CreateUI("InputArea", parent);
        RectTransform rowRT = row.GetComponent<RectTransform>();
        SetAnchors(rowRT, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 10f), new Vector2(-12f, 0f));
        rowRT.pivot = new Vector2(0.5f, 0f);
        rowRT.sizeDelta = new Vector2(0f, 42f);

        TMP_Text prompt = CreateTMP("Prompt", row.transform, "scada>", 15, ColAccent, FontStyles.Bold);
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(prompt.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        prompt.rectTransform.pivot = new Vector2(0f, 0.5f);
        prompt.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        prompt.rectTransform.sizeDelta = new Vector2(84f, 0f);

        GameObject inputGO = CreateUI("TerminalInput", row.transform);
        RectTransform inputRT = inputGO.GetComponent<RectTransform>();
        SetAnchors(inputRT, Vector2.zero, Vector2.one, new Vector2(88f, 0f), new Vector2(-132f, 0f));
        Image inputImage = AddImage(inputGO, ColInput);

        inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.targetGraphic = inputImage;
        inputField.caretColor = ColAccent;
        inputField.selectionColor = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.25f);
        inputField.pointSize = 14f;

        GameObject textArea = CreateUI("Text Area", inputGO.transform);
        RectTransform textAreaRT = textArea.GetComponent<RectTransform>();
        SetAnchors(textAreaRT, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));
        textArea.AddComponent<RectMask2D>();

        TMP_Text placeholder = CreateTMP("Placeholder", textArea.transform, "status, fault on, trip, reset, attack goose_data", 13, ColDim);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text inputText = CreateTMP("Text", textArea.transform, "", 13, ColText);
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(inputText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.fontAsset = TMP_Settings.defaultFontAsset;

        GameObject buttonGO = CreateUI("SendButton", row.transform);
        RectTransform buttonRT = buttonGO.GetComponent<RectTransform>();
        SetAnchors(buttonRT, new Vector2(1f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        buttonRT.pivot = new Vector2(1f, 0.5f);
        buttonRT.anchoredPosition = Vector2.zero;
        buttonRT.sizeDelta = new Vector2(118f, 0f);
        Image buttonImage = AddImage(buttonGO, new Color(0.04f, 0.62f, 0.45f, 1f));

        sendButton = buttonGO.AddComponent<Button>();
        sendButton.targetGraphic = buttonImage;
        ColorBlock colors = sendButton.colors;
        colors.highlightedColor = new Color(0.02f, 0.75f, 0.58f, 1f);
        colors.pressedColor = new Color(0.02f, 0.45f, 0.36f, 1f);
        sendButton.colors = colors;

        TMP_Text label = CreateTMP("SendLabel", buttonGO.transform, "RUN", 13, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    GameObject BuildWarningPanel(RectTransform parent)
    {
        GameObject panel = CreatePanel("WarningPanel", parent, ColWarning);
        RectTransform rt = panel.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, 0f), new Vector2(-44f, 0f));
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(0f, 44f);

        TMP_Text text = CreateTMP("WarningText", panel.transform, "", 15, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

        panel.SetActive(false);
        return panel;
    }

    void WireComponents()
    {
        IEDController ied = gameObject.GetComponent<IEDController>() ?? gameObject.AddComponent<IEDController>();
        CircuitBreakerController breaker = gameObject.GetComponent<CircuitBreakerController>() ?? gameObject.AddComponent<CircuitBreakerController>();
        BreakerController breakerScenario = gameObject.GetComponent<BreakerController>() ?? gameObject.AddComponent<BreakerController>();
        BreakerMQTTReceiver breakerMqtt = gameObject.GetComponent<BreakerMQTTReceiver>() ?? gameObject.AddComponent<BreakerMQTTReceiver>();
        AlarmPanelController alarms = gameObject.GetComponent<AlarmPanelController>() ?? gameObject.AddComponent<AlarmPanelController>();
        SCADAHMIController hmi = gameObject.GetComponent<SCADAHMIController>() ?? gameObject.AddComponent<SCADAHMIController>();
        SCADATerminalController scadaTerminal = gameObject.GetComponent<SCADATerminalController>() ?? gameObject.AddComponent<SCADATerminalController>();
        TerminalController legacyTerminal = gameObject.GetComponent<TerminalController>() ?? gameObject.AddComponent<TerminalController>();

        hmi.iedController = ied;
        hmi.circuitBreaker = breaker;
        hmi.alarmPanel = alarms;
        hmi.terminalController = scadaTerminal;
        hmi.breakerControlScenario = breakerScenario;
        hmi.componentBindings = componentBindings;
        hmi.systemModeText = systemModeText;
        hmi.protocolText = protocolText;
        hmi.clockText = clockText;
        hmi.iedCurrentText = iedCurrentText;
        hmi.iedThresholdText = iedThresholdText;
        hmi.iedTripStatusText = iedTripStatusText;
        hmi.iedStNumText = iedStNumText;
        hmi.iedSqNumText = iedSqNumText;
        hmi.iedLastGooseText = iedLastGooseText;
        hmi.iedAttackText = iedAttackText;
        hmi.breakerStateText = breakerStateText;
        hmi.breakerDetailText = breakerDetailText;
        hmi.breakerStateIndicator = breakerStateIndicator;
        hmi.breakerCommandSourceText = breakerCommandSourceText;
        hmi.breakerLastCommandTimeText = breakerLastCommandTimeText;

        ied.currentText = iedCurrentText;
        ied.thresholdText = iedThresholdText;
        ied.tripStatusText = iedTripStatusText;
        ied.stNumText = iedStNumText;
        ied.sqNumText = iedSqNumText;
        ied.lastGooseText = iedLastGooseText;
        ied.attackStatusText = iedAttackText;
        ied.iedIndicator = iedIndicator;

        breaker.stateText = breakerStateText;
        breaker.detailText = breakerDetailText;
        breaker.stateIndicator = breakerStateIndicator;
        breaker.mimicIndicator = breakerMimicIndicator;

        breakerScenario.circuitBreaker = breaker;
        breakerScenario.hmiController = hmi;
        breakerScenario.alarmPanelController = alarms;
        breakerScenario.terminalController = scadaTerminal;
        breakerScenario.breakerStatusText = breakerStateText;
        breakerScenario.commandSourceText = breakerCommandSourceText;
        breakerScenario.lastCommandTimeText = breakerLastCommandTimeText;
        breakerScenario.alarmPanelImage = alarmPanelImage;
        breakerScenario.alarmPanelObject = alarmPanelImage != null ? alarmPanelImage.gameObject : null;
        breakerScenario.energyLineImages = new[] { breakerMimicIndicator };

        breakerMqtt.breakerController = breakerScenario;

        alarms.alarmListText = alarmListText;
        alarms.alarmCountText = alarmCountText;
        alarms.maxAlarmRows = 3;

        scadaTerminal.hmiController = hmi;
        scadaTerminal.legacyTerminal = legacyTerminal;
        scadaTerminal.outputText = outputText;
        scadaTerminal.inputField = inputField;
        scadaTerminal.sendButton = sendButton;

        legacyTerminal.OutputText = outputText;
        legacyTerminal.InputField = inputField;
        legacyTerminal.StatusBar = statusBarText;
        legacyTerminal.WarningPanel = warningPanel;
        legacyTerminal.SendButton = sendButton;
        legacyTerminal.scadaTerminalController = scadaTerminal;

        TerminalPencereYonetici windowManager = gameObject.GetComponent<TerminalPencereYonetici>() ?? gameObject.AddComponent<TerminalPencereYonetici>();
        windowManager.terminalPencere = terminalWindow;
        windowManager.icerikAlani = bodyArea;
        windowManager.durumCubugu = statusBarText != null ? statusBarText.gameObject : null;
        windowManager.uyariPaneli = warningPanel;

        if (minButton != null)
            minButton.onClick.AddListener(windowManager.KucultButonTiklandi);
        if (toggleButton != null)
            toggleButton.onClick.AddListener(windowManager.ToggleButonTiklandi);

        hmi.RefreshAll();
    }

    Button CreateTitleButton(Transform parent, string name, string label)
    {
        GameObject go = CreateUI(name, parent);
        Image image = AddImage(go, new Color(0.10f, 0.13f, 0.16f, 1f));
        AddLayout(go, 34f, 30f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.20f, 0.24f, 0.28f, 1f);
        colors.pressedColor = new Color(0.08f, 0.10f, 0.12f, 1f);
        button.colors = colors;

        TMP_Text text = CreateTMP("Label", go.transform, label, 16, ColText, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    void AddLine(RectTransform parent, string name, Vector2 start, Vector2 end, float thickness, Color color)
    {
        GameObject line = CreateUI(name, parent);
        AddImage(line, color);
        RectTransform rt = line.GetComponent<RectTransform>();

        Vector2 delta = end - start;
        Vector2 mid = (start + end) * 0.5f;
        rt.anchorMin = mid;
        rt.anchorMax = mid;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(delta.magnitude * 1000f, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    Image CreateIndicator(Transform parent, string name, Color color, float size)
    {
        GameObject indicatorGO = CreateUI(name, parent);
        Image indicator = AddImage(indicatorGO, color);
        AddLayout(indicatorGO, size, size);
        RectTransform rt = indicatorGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        return indicator;
    }

    void AddRectBorder(GameObject target, Color color, float thickness)
    {
        RectTransform parent = target.GetComponent<RectTransform>();
        AddBorderSegment(parent, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, thickness, true, color);
        AddBorderSegment(parent, "BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), thickness, true, color);
        AddBorderSegment(parent, "BorderLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), thickness, false, color);
        AddBorderSegment(parent, "BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero, thickness, false, color);
    }

    void AddBorderSegment(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float thickness,
        bool horizontal,
        Color color)
    {
        GameObject segment = CreateUI(name, parent);
        Image image = AddImage(segment, color);
        image.raycastTarget = false;

        LayoutElement layout = segment.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        RectTransform rt = segment.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        if (horizontal)
            rt.sizeDelta = new Vector2(0f, thickness);
        else
            rt.sizeDelta = new Vector2(thickness, 0f);
    }

    GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = CreateUI(name, parent);
        AddImage(go, color);
        return go;
    }

    GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }

    Image AddImage(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    Outline AddOutline(GameObject go, Color color, Vector2 distance)
    {
        Outline outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        return outline;
    }

    LayoutElement AddLayout(GameObject go, float preferredWidth, float preferredHeight)
    {
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }
        if (preferredHeight > 0f)
        {
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 size = rt.sizeDelta;
            if (preferredWidth > 0f)
                size.x = preferredWidth;
            if (preferredHeight > 0f)
                size.y = preferredHeight;
            rt.sizeDelta = size;
        }

        return element;
    }

    TMP_Text CreateTMP(
        string name,
        Transform parent,
        string text,
        float size,
        Color color,
        FontStyles style = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject go = CreateUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
