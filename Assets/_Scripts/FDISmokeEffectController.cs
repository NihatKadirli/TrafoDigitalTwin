using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FDISmokeEffectController : MonoBehaviour
{
    [Header("Smoke Placement")]
    public string smokeAnchorObjectName = "TransformerSmoke";
    public Vector3 fallbackSmokePosition = new Vector3(5.3f, 9.4f, 29.6f);
    public Vector3 smokeAnchorOffset = new Vector3(0f, 1.75f, 0f);
    public bool forceDemoDefaultsOnAwake = true;

    [Header("Smoke Look")]
    public Color smokeCoreColor = new Color(0.08f, 0.08f, 0.08f, 0.72f);
    public Color smokeMidColor = new Color(0.24f, 0.24f, 0.24f, 0.58f);
    public Color smokeLightColor = new Color(0.46f, 0.46f, 0.46f, 0.42f);
    public float smokeScale = 2.45f;
    public float puffRiseSpeed = 0.55f;
    public float puffDriftRadius = 0.45f;

    [Header("Camera Focus")]
    public Camera focusCamera;
    public Vector3 focusCameraOffset = new Vector3(0.35f, 0.9f, -3.15f);
    public Vector3 focusLookOffset = new Vector3(0f, 0.55f, 0f);
    public float focusFieldOfView = 30f;
    public float focusHoldSeconds = 5f;
    public bool disableCinemachineBrainDuringFocus = true;
    public bool disableFpsControllerDuringFocus = true;

    [Header("Attack Alarm")]
    public string alarmLightObjectName = "Alarm-Light";
    public Light alarmLight;
    public Color alarmLightColor = new Color(1f, 0.05f, 0.02f, 1f);
    public float alarmLightMinIntensity = 0.2f;
    public float alarmLightMaxIntensity = 8f;
    public float alarmBlinkFrequency = 3.5f;
    public bool createAlarmLightIfMissing = true;
    public AudioSource alarmAudioSource;
    public float alarmVolume = 0.08f;
    public float alarmToneFrequency = 880f;
    public float alarmPulseFrequency = 2f;

    readonly List<Transform> smokePuffs = new List<Transform>();
    readonly List<Vector3> basePuffPositions = new List<Vector3>();
    readonly List<float> puffSeeds = new List<float>();
    readonly List<float> puffScales = new List<float>();

    GameObject smokeRoot;
    Material coreMaterial;
    Material midMaterial;
    Material lightMaterial;
    bool smokeActive;
    bool focusActive;
    Camera activeCamera;
    Camera temporaryFocusCamera;
    Vector3 focusCameraPosition;
    Quaternion focusCameraRotation;
    Transform focusedCameraTransform;
    Transform originalCameraParent;
    Vector3 originalCameraLocalPosition;
    Quaternion originalCameraLocalRotation;
    Vector3 originalCameraWorldPosition;
    Quaternion originalCameraWorldRotation;
    float originalCameraFieldOfView;
    Behaviour disabledCinemachineBrain;
    bool cinemachineBrainWasEnabled;
    FPSKontrol disabledFpsKontrol;
    bool fpsKontrolWasEnabled;
    float focusEndRealtime;
    AudioClip generatedAlarmClip;
    bool alarmActive;
    bool alarmLightObjectOriginalActive;
    bool alarmLightOriginalEnabled;
    float alarmLightOriginalIntensity;
    Color alarmLightOriginalColor;
    bool alarmLightStateCaptured;

    public static FDISmokeEffectController GetOrCreate()
    {
        FDISmokeEffectController existing = FindFirstObjectByType<FDISmokeEffectController>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject controllerObject = new GameObject("FDI_SmokeEffectController");
        return controllerObject.AddComponent<FDISmokeEffectController>();
    }

    void Awake()
    {
        if (forceDemoDefaultsOnAwake)
            ApplyDemoDefaults();

        EnsureSmokeCreated();
        EnsureAlarmReferences();
        SetSmokeVisible(false);
        SetAttackAlarm(false);
    }

    void ApplyDemoDefaults()
    {
        smokeAnchorOffset = new Vector3(0f, 1.75f, 0f);
        smokeCoreColor = new Color(0.06f, 0.06f, 0.06f, 0.82f);
        smokeMidColor = new Color(0.18f, 0.18f, 0.18f, 0.66f);
        smokeLightColor = new Color(0.42f, 0.42f, 0.42f, 0.48f);
        smokeScale = 2.45f;
        puffRiseSpeed = 0.48f;
        puffDriftRadius = 0.34f;
        focusCameraOffset = new Vector3(0.35f, 0.9f, -3.15f);
        focusLookOffset = new Vector3(0f, 0.55f, 0f);
        focusFieldOfView = 30f;
        focusHoldSeconds = 5f;
        alarmLightColor = new Color(1f, 0.05f, 0.02f, 1f);
        alarmLightMinIntensity = 0.2f;
        alarmLightMaxIntensity = 8f;
        alarmBlinkFrequency = 3.5f;
        alarmVolume = 0.08f;
        alarmToneFrequency = 880f;
        alarmPulseFrequency = 2f;
    }

    void OnEnable()
    {
        Camera.onPreCull += ApplyFocusBeforeRender;
    }

    void OnDisable()
    {
        Camera.onPreCull -= ApplyFocusBeforeRender;
    }

    void Update()
    {
        if (focusActive)
        {
            ApplyFocusCameraTransform();
            if (Time.realtimeSinceStartup >= focusEndRealtime)
                FinishCameraFocus();
        }

        if (smokeActive && smokeRoot != null)
            AnimateSmoke(Time.time);

        if (alarmActive)
            UpdateAttackAlarm();
    }

    void LateUpdate()
    {
        if (!focusActive || activeCamera == null)
            return;

        ApplyFocusCameraTransform();
    }

    void ApplyFocusBeforeRender(Camera cameraToRender)
    {
        if (!focusActive || activeCamera == null || cameraToRender != activeCamera)
            return;

        ApplyFocusCameraTransform();
    }

    void ApplyFocusCameraTransform()
    {
        activeCamera.transform.SetPositionAndRotation(focusCameraPosition, focusCameraRotation);
        activeCamera.fieldOfView = focusFieldOfView;
    }

    public void StartSmokeAttack()
    {
        if (forceDemoDefaultsOnAwake)
            ApplyDemoDefaults();

        EnsureSmokeCreated();
        ApplySmokeMaterialColors();
        MoveSmokeToAnchor();
        SetSmokeVisible(true);
        SetAttackAlarm(true);
        StartCameraFocus();
    }

    public void StopSmokeAttack()
    {
        SetSmokeVisible(false);
        SetAttackAlarm(false);
    }

    void EnsureAlarmReferences()
    {
        if (alarmLight == null && !string.IsNullOrWhiteSpace(alarmLightObjectName))
        {
            GameObject alarmLightObject = GameObject.Find(alarmLightObjectName);
            if (alarmLightObject != null)
            {
                alarmLight = alarmLightObject.GetComponent<Light>();
                if (alarmLight == null && createAlarmLightIfMissing)
                    alarmLight = alarmLightObject.AddComponent<Light>();
            }

            if (alarmLight == null)
                alarmLight = FindInactiveLightByName(alarmLightObjectName);
        }

        if (alarmLight != null && !alarmLightStateCaptured)
        {
            alarmLightObjectOriginalActive = alarmLight.gameObject.activeSelf;
            alarmLightOriginalEnabled = alarmLight.enabled;
            alarmLightOriginalIntensity = alarmLight.intensity;
            alarmLightOriginalColor = alarmLight.color;
            alarmLightStateCaptured = true;
        }

        if (alarmAudioSource == null)
        {
            alarmAudioSource = GetComponent<AudioSource>();
            if (alarmAudioSource == null)
                alarmAudioSource = gameObject.AddComponent<AudioSource>();
        }

        alarmAudioSource.playOnAwake = false;
        alarmAudioSource.loop = true;
        alarmAudioSource.spatialBlend = 0f;
        alarmAudioSource.volume = alarmVolume;

        if (generatedAlarmClip == null)
            generatedAlarmClip = CreateAlarmClip();
        if (alarmAudioSource.clip == null)
            alarmAudioSource.clip = generatedAlarmClip;
    }

    AudioClip CreateAlarmClip()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 1f;
        int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float pulse = Mathf.Sin(time * alarmPulseFrequency * Mathf.PI * 2f) > 0f ? 1f : 0f;
            float envelope = pulse * 0.35f;
            samples[i] = Mathf.Sin(time * alarmToneFrequency * Mathf.PI * 2f) * envelope;
        }

        AudioClip clip = AudioClip.Create("FDI_Small_Alarm_Beep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void SetAttackAlarm(bool active)
    {
        EnsureAlarmReferences();
        alarmActive = active;

        if (alarmLight == null)
        {
            Debug.LogWarning($"[FDISmokeEffectController] Alarm light '{alarmLightObjectName}' could not be found.");
        }
        else if (active)
        {
            alarmLight.gameObject.SetActive(true);
            alarmLight.enabled = true;
            alarmLight.color = alarmLightColor;
            alarmLight.intensity = alarmLightMaxIntensity;
        }
        else if (!active)
        {
            alarmLight.enabled = alarmLightOriginalEnabled;
            alarmLight.intensity = alarmLightOriginalIntensity;
            alarmLight.color = alarmLightOriginalColor;
            alarmLight.gameObject.SetActive(alarmLightObjectOriginalActive);
        }

        if (alarmAudioSource == null)
            return;

        alarmAudioSource.volume = alarmVolume;
        if (active)
        {
            if (!alarmAudioSource.isPlaying)
                alarmAudioSource.Play();
        }
        else
        {
            alarmAudioSource.Stop();
        }
    }

    Light FindInactiveLightByName(string lightObjectName)
    {
        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight != null && sceneLight.gameObject.name == lightObjectName)
                return sceneLight;
        }

        return null;
    }

    void UpdateAttackAlarm()
    {
        if (alarmLight == null)
            return;

        float blink = (Mathf.Sin(Time.time * alarmBlinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        alarmLight.enabled = true;
        alarmLight.color = alarmLightColor;
        alarmLight.intensity = Mathf.Lerp(alarmLightMinIntensity, alarmLightMaxIntensity, blink);
    }

    void EnsureSmokeCreated()
    {
        if (smokeRoot != null)
            return;

        smokeRoot = new GameObject("FDI_GeneratedRealisticSmoke");
        smokeRoot.transform.SetParent(transform, false);

        coreMaterial = CreateSmokeMaterial("FDI Smoke Core", smokeCoreColor);
        midMaterial = CreateSmokeMaterial("FDI Smoke Mid", smokeMidColor);
        lightMaterial = CreateSmokeMaterial("FDI Smoke Light", smokeLightColor);

        CreatePuff("Smoke_Core_01", new Vector3(0f, 0f, 0f), 1.05f, coreMaterial, 0.0f);
        CreatePuff("Smoke_Core_02", new Vector3(-0.35f, 0.35f, 0.18f), 0.95f, coreMaterial, 1.7f);
        CreatePuff("Smoke_Mid_01", new Vector3(0.45f, 0.45f, -0.2f), 1.1f, midMaterial, 3.1f);
        CreatePuff("Smoke_Mid_02", new Vector3(-0.15f, 0.95f, -0.35f), 1.2f, midMaterial, 4.8f);
        CreatePuff("Smoke_Light_01", new Vector3(0.32f, 1.28f, 0.28f), 1.3f, lightMaterial, 6.2f);
        CreatePuff("Smoke_Light_02", new Vector3(-0.55f, 1.62f, 0.15f), 1.15f, lightMaterial, 7.6f);
        CreatePuff("Smoke_Light_03", new Vector3(0.62f, 1.88f, -0.22f), 1.35f, lightMaterial, 9.0f);

        AddParticleLayer("FDI_RisingSmokeParticles", smokeMidColor, 1.2f, 2.8f, 35f, 3.6f);
        AddParticleLayer("FDI_DarkSmokeParticles", smokeCoreColor, 1.05f, 1.6f, 34f, 2.8f);

        MoveSmokeToAnchor();
    }

    Material CreateSmokeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }

    void ApplySmokeMaterialColors()
    {
        ApplySmokeMaterialColor(coreMaterial, smokeCoreColor);
        ApplySmokeMaterialColor(midMaterial, smokeMidColor);
        ApplySmokeMaterialColor(lightMaterial, smokeLightColor);
    }

    void ApplySmokeMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }


    void CreatePuff(string puffName, Vector3 localPosition, float scale, Material material, float seed)
    {
        GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.name = puffName;
        puff.transform.SetParent(smokeRoot.transform, false);
        puff.transform.localPosition = localPosition;
        puff.transform.localScale = Vector3.one * smokeScale * scale;

        Collider collider = puff.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = puff.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        smokePuffs.Add(puff.transform);
        basePuffPositions.Add(localPosition);
        puffSeeds.Add(seed);
        puffScales.Add(scale);
    }

    void AddParticleLayer(string layerName, Color color, float startSize, float startSpeed, float emissionRate, float lifetime)
    {
        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(smokeRoot.transform, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localRotation = Quaternion.LookRotation(Vector3.up);

        ParticleSystem particleSystem = layer.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = color;
        main.maxParticles = 1500;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.45f;

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.25f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.45f, 1.0f),
            new Keyframe(1f, 1.55f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = midMaterial;
            renderer.maxParticleSize = 5.5f;
            renderer.sortingFudge = 3f;
        }
    }

    void MoveSmokeToAnchor()
    {
        smokeRoot.transform.position = GetAnchorPosition();
        smokeRoot.transform.rotation = Quaternion.identity;
    }

    Vector3 GetAnchorPosition()
    {
        GameObject anchor = GameObject.Find(smokeAnchorObjectName);
        if (anchor != null)
            return anchor.transform.position + smokeAnchorOffset;

        return fallbackSmokePosition;
    }

    void SetSmokeVisible(bool visible)
    {
        smokeActive = visible;
        if (smokeRoot != null)
            smokeRoot.SetActive(visible);

        ParticleSystem[] particles = smokeRoot != null
            ? smokeRoot.GetComponentsInChildren<ParticleSystem>(true)
            : new ParticleSystem[0];

        foreach (ParticleSystem particle in particles)
        {
            if (visible)
            {
                particle.gameObject.SetActive(true);
                particle.Clear(true);
                particle.Play(true);
                particle.Emit(110);
            }
            else
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void AnimateSmoke(float time)
    {
        for (int i = 0; i < smokePuffs.Count; i++)
        {
            Transform puff = smokePuffs[i];
            if (puff == null)
                continue;

            float seed = puffSeeds[i];
            Vector3 basePosition = basePuffPositions[i];
            float driftX = Mathf.Sin(time * 0.55f + seed) * puffDriftRadius;
            float driftZ = Mathf.Cos(time * 0.42f + seed * 0.7f) * puffDriftRadius;
            float rise = Mathf.Repeat((time * puffRiseSpeed) + seed, 2.4f) * 0.35f;

            puff.localPosition = basePosition + new Vector3(driftX, rise, driftZ);
            float pulse = 1f + Mathf.Sin(time * 0.85f + seed) * 0.08f;
            puff.localScale = Vector3.one * smokeScale * puffScales[i] * pulse;
        }
    }

    void StartCameraFocus()
    {
        Camera targetCamera = focusCamera != null ? focusCamera : Camera.main;
        if (targetCamera == null || smokeRoot == null)
        {
            Debug.LogWarning("[FDISmokeEffectController] Smoke focus skipped; camera or generated smoke is missing.");
            return;
        }

        if (focusActive)
            FinishCameraFocus(false);

        BeginCameraFocus(targetCamera);
    }

    void BeginCameraFocus(Camera targetCamera)
    {
        Camera renderCamera = EnsureTemporaryFocusCamera(targetCamera);
        focusedCameraTransform = renderCamera.transform;
        originalCameraParent = focusedCameraTransform.parent;
        originalCameraLocalPosition = focusedCameraTransform.localPosition;
        originalCameraLocalRotation = focusedCameraTransform.localRotation;
        originalCameraWorldPosition = focusedCameraTransform.position;
        originalCameraWorldRotation = focusedCameraTransform.rotation;
        originalCameraFieldOfView = renderCamera.fieldOfView;

        disabledCinemachineBrain = targetCamera.GetComponent("CinemachineBrain") as Behaviour;
        cinemachineBrainWasEnabled = disabledCinemachineBrain != null && disabledCinemachineBrain.enabled;

        disabledFpsKontrol = FindFirstObjectByType<FPSKontrol>(FindObjectsInactive.Include);
        fpsKontrolWasEnabled = disabledFpsKontrol != null && disabledFpsKontrol.enabled;

        if (disableFpsControllerDuringFocus && disabledFpsKontrol != null)
            disabledFpsKontrol.enabled = false;

        if (disableCinemachineBrainDuringFocus && disabledCinemachineBrain != null)
            disabledCinemachineBrain.enabled = false;

        focusedCameraTransform.SetParent(null, true);

        Vector3 lookPoint = smokeRoot.transform.position + focusLookOffset;
        focusCameraPosition = smokeRoot.transform.position + focusCameraOffset;
        focusCameraRotation = Quaternion.LookRotation(lookPoint - focusCameraPosition, Vector3.up);
        activeCamera = renderCamera;
        activeCamera.enabled = true;
        focusActive = true;
        focusEndRealtime = Time.realtimeSinceStartup + focusHoldSeconds;
        ApplyFocusCameraTransform();

        Debug.Log($"[FDISmokeEffectController] Smoke focus started for {focusHoldSeconds:F1} seconds. now={Time.realtimeSinceStartup:F2}, end={focusEndRealtime:F2}");
    }

    Camera EnsureTemporaryFocusCamera(Camera sourceCamera)
    {
        if (temporaryFocusCamera == null)
        {
            GameObject cameraObject = new GameObject("FDI_FocusCamera");
            cameraObject.transform.SetParent(transform, false);
            temporaryFocusCamera = cameraObject.AddComponent<Camera>();
        }

        temporaryFocusCamera.CopyFrom(sourceCamera);
        temporaryFocusCamera.depth = sourceCamera.depth + 50f;
        temporaryFocusCamera.enabled = false;
        return temporaryFocusCamera;
    }

    void FinishCameraFocus(bool logRestore = true)
    {
        focusActive = false;

        if (temporaryFocusCamera != null)
            temporaryFocusCamera.enabled = false;

        if (activeCamera != null)
            activeCamera.fieldOfView = originalCameraFieldOfView;

        if (focusedCameraTransform != null)
        {
            focusedCameraTransform.SetParent(originalCameraParent, true);

            if (originalCameraParent != null)
            {
                focusedCameraTransform.localPosition = originalCameraLocalPosition;
                focusedCameraTransform.localRotation = originalCameraLocalRotation;
            }
            else
            {
                focusedCameraTransform.position = originalCameraWorldPosition;
                focusedCameraTransform.rotation = originalCameraWorldRotation;
            }
        }

        if (disabledCinemachineBrain != null)
            disabledCinemachineBrain.enabled = cinemachineBrainWasEnabled;

        if (disabledFpsKontrol != null)
        {
            disabledFpsKontrol.enabled = fpsKontrolWasEnabled;
            if (fpsKontrolWasEnabled)
                disabledFpsKontrol.SetTerminalDurumu(false);
        }

        activeCamera = null;
        focusedCameraTransform = null;
        disabledCinemachineBrain = null;
        disabledFpsKontrol = null;

        if (logRestore)
            Debug.Log($"[FDISmokeEffectController] Smoke focus finished. Camera restored. now={Time.realtimeSinceStartup:F2}");
    }
}
