using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChipUIBridge : MonoBehaviour
{
    public enum VisibilidadMode
    {
        SiempreVisible,
        SiempreOculto,
        Manual,
        ApuntandoALayer
    }

    [Header("Setup")]
    public Canvas canvasDestino;
    public RectTransform rawImageParent;
    public Vector2 uiSize = new Vector2(96, 96);
    public int rtSize = 256;

    [Tooltip("Usa un layer distinto por icono (ej: UI3D_Chip / UI3D_Grapple) para que no se mezclen.")]
    public string layerUI3D = "UI3D";

    [Header("Camara UI3D")]
    [Range(-10, 60)] public float tiltGrados = 20f;
    public float fov = 40f;
    public Color bgColor = new Color(0, 0, 0, 0);
    public float cameraDistanceMult = 2.5f;

    [Header("Idle")]
    public bool usarFlotacion = true;
    public float bobAmplitude = 0.07f;
    public float bobFreq = 1.6f;
    public bool usarRotacion = true;
    public float velRotY = 60f;

    [Header("Pulse (cuando lo agarras)")]
    public float pulseEscala = 1.25f;
    public float pulseRecuperacion = 10f;

    [Header("Raw")]
    public string rawName = "Raw_Chip";
    public bool destruirRawAlDestruir = true;

    // ===== VISIBILIDAD =====
    [Header("Visibilidad")]
    public VisibilidadMode modoVisibilidad = VisibilidadMode.SiempreVisible;
    public bool startVisible = true;

    [Header("Modo: Apuntando a layer (Grapple)")]
    public LayerMask aimLayerMask;         // pon SOLO GrappleRope
    public float aimMaxDistance = 12f;
    public float aimSphereRadius = 0.08f;  // 0 = Raycast
    public Camera aimCamera;               // si queda null usa Camera.main
    public bool debugRay = false;

    // ===== ARO =====
    [Header("Aro de luz girando (opcional, para Grapple)")]
    public bool usarAro = false;

    [ColorUsage(true, true)]
    public Color aroColor = new Color(1f, 0.55f, 0.15f, 1f); // HDR permitido

    [Range(0.1f, 5f)] public float aroColorIntensity = 1.8f;

    [Range(0f, 1f)] public float aroAlpha = 0.85f;
    [Range(0.05f, 1f)] public float aroTrail = 0.75f;
    public float aroSizeMul = 1.6f;
    public float aroRotSpeed = 220f; // deg/s

    [Header("Aro - brillo extra (fake glow)")]
    public bool aroGlowExtra = true;
    [Range(0f, 1f)] public float aroGlowAlpha = 0.35f;
    [Range(0.05f, 1f)] public float aroGlowTrail = 0.9f;
    public float aroGlowSizeMul = 2.2f;

    [Tooltip("Sube el brillo del sprite del aro (la 'cola' y la cabeza).")]
    [Range(0.5f, 3f)] public float aroSpriteGlowBoost = 1.7f;

    [Tooltip("Si está activo, intenta usar un material aditivo para que el aro 'brille' más (sin bloom).")]
    public bool aroUsarMaterialAdditive = true;

    [Tooltip("Si lo asignás, se usa este material (ideal: uno aditivo para UI).")]
    public Material aroMaterialOverride;

    // ===== TEXTOS TMP =====
    [Header("Tecla (TMP) - Texto 1 (opcional)")]
    public bool mostrarTecla = false;
    public string tecla = "E";
    public int teclaFontSize = 26;
    public Vector2 teclaOffset = new Vector2(-10f, 10f);
    public Color teclaColor = Color.white;
    public TMP_FontAsset teclaFont;
    public float teclaOutlineWidth = 0.2f;
    public Color teclaOutlineColor = new Color(0f, 0f, 0f, 0.85f);
    public TextAlignmentOptions teclaAlignment = TextAlignmentOptions.TopRight;

    [Header("Texto 2 (TMP) - opcional (ej: 'Grapple')")]
    public bool mostrarTexto2 = false;
    [TextArea] public string texto2 = "Grapple";
    public int texto2FontSize = 18;
    public Vector2 texto2Offset = new Vector2(-10f, -12f);
    public Color texto2Color = Color.white;
    public TMP_FontAsset texto2Font;
    public float texto2OutlineWidth = 0.15f;
    public Color texto2OutlineColor = new Color(0f, 0f, 0f, 0.85f);
    public TextAlignmentOptions texto2Alignment = TextAlignmentOptions.TopRight;

    Camera camUI;
    RenderTexture rt;
    RawImage raw;
    CanvasGroup rawCg;

    GameObject ringGO;
    RectTransform ringRT;
    Image ringImg;
    CanvasGroup ringCg;
    Sprite ringSprite;

    GameObject ringGlowGO;
    RectTransform ringGlowRT;
    Image ringGlowImg;
    CanvasGroup ringGlowCg;
    Sprite ringGlowSprite;

    Transform extrasParent;
    TextMeshProUGUI teclaTMP;
    TextMeshProUGUI texto2TMP;

    Material _aroMatRuntime;

    Vector3 escalaInicial;
    Vector3 posInicialLocal;
    float multPulse = 1f;
    float t0;

    bool _visible = true;

    void Start()
    {
        if (!canvasDestino)
        {
            Debug.LogWarning("ChipUIBridge: asigna canvasDestino.");
            enabled = false;
            return;
        }

        int layer = LayerMask.NameToLayer(layerUI3D);
        if (layer < 0)
        {
            Debug.LogWarning("ChipUIBridge: no existe la capa '" + layerUI3D + "'. Creala en Tags & Layers.");
            enabled = false;
            return;
        }

        // OJO: este objeto debe ser un DUPLICADO solo para UI (no el del mundo)
        SetLayerRecursively(gameObject, layer);

        rt = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        rt.Create();

        GameObject goCam = new GameObject("Cam_UI3D_" + rawName);
        camUI = goCam.AddComponent<Camera>();
        camUI.clearFlags = CameraClearFlags.SolidColor;
        camUI.backgroundColor = bgColor;
        camUI.cullingMask = 1 << layer;
        camUI.orthographic = false;
        camUI.fieldOfView = fov;
        camUI.nearClipPlane = 0.03f;
        camUI.farClipPlane = 200f;
        camUI.targetTexture = rt;

        Bounds b = GetBoundsWorld(transform);
        Vector3 center = b.center;
        float radius = Mathf.Max(0.01f, b.extents.magnitude);

        Vector3 dir = Quaternion.Euler(tiltGrados, 0f, 0f) * Vector3.back;
        camUI.transform.position = center + dir.normalized * (radius * cameraDistanceMult);
        camUI.transform.LookAt(center, Vector3.up);

        // Raw principal
        GameObject goRaw = new GameObject(rawName, typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
        goRaw.transform.SetParent(rawImageParent ? rawImageParent : canvasDestino.transform, false);

        RectTransform rtUI = goRaw.GetComponent<RectTransform>();
        rtUI.sizeDelta = uiSize;

        raw = goRaw.GetComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;

        rawCg = goRaw.GetComponent<CanvasGroup>();
        rawCg.blocksRaycasts = false;
        rawCg.interactable = false;

        // Parent para TMPs (para que hereden alpha del CanvasGroup del Raw)
        if (mostrarTecla || mostrarTexto2)
        {
            var goExtras = new GameObject("ExtrasTMP", typeof(RectTransform));
            goExtras.transform.SetParent(goRaw.transform, false);
            extrasParent = goExtras.transform;

            RectTransform r = goExtras.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // Aro (se crea como sibling, detras del raw)
        if (usarAro)
            CreateRing(goRaw.transform.parent, rtUI);

        // Textos TMP
        if (mostrarTecla)
            teclaTMP = CreateTMP(extrasParent, "TMP_Tecla", tecla, teclaFontSize, teclaColor, teclaFont, teclaAlignment, teclaOffset, teclaOutlineWidth, teclaOutlineColor);

        if (mostrarTexto2)
            texto2TMP = CreateTMP(extrasParent, "TMP_Texto2", texto2, texto2FontSize, texto2Color, texto2Font, texto2Alignment, texto2Offset, texto2OutlineWidth, texto2OutlineColor);

        escalaInicial = transform.localScale;
        posInicialLocal = transform.localPosition;
        t0 = Random.value * 10f;

        SetVisible(startVisible);
    }

    void Update()
    {
        // Visibilidad
        if (modoVisibilidad == VisibilidadMode.SiempreVisible) SetVisible(true);
        else if (modoVisibilidad == VisibilidadMode.SiempreOculto) SetVisible(false);
        else if (modoVisibilidad == VisibilidadMode.ApuntandoALayer) UpdateAimVisibility();

        if (!_visible) return;

        // Rotacion del modelo
        if (usarRotacion)
            transform.Rotate(0f, velRotY * Time.deltaTime, 0f, Space.Self);

        // Bob
        float bob = 0f;
        if (usarFlotacion)
            bob = Mathf.Sin((t0 + Time.time) * Mathf.PI * 2f * bobFreq) * bobAmplitude;

        // Pulse
        float kRec = 1f - Mathf.Exp(-pulseRecuperacion * Time.deltaTime);
        multPulse = Mathf.Lerp(multPulse, 1f, kRec);

        transform.localScale = escalaInicial * multPulse;

        Vector3 basePos = posInicialLocal + new Vector3(0f, bob, 0f);
        if (transform.localPosition != basePos)
            transform.localPosition = basePos;

        // Rotacion del aro en UI
        if (ringRT) ringRT.Rotate(0f, 0f, -aroRotSpeed * Time.deltaTime);
        if (ringGlowRT) ringGlowRT.Rotate(0f, 0f, -aroRotSpeed * Time.deltaTime);
    }

    void UpdateAimVisibility()
    {
        Camera cam = aimCamera ? aimCamera : Camera.main;
        if (!cam)
        {
            SetVisible(false);
            return;
        }

        Vector3 o = cam.transform.position;
        Vector3 d = cam.transform.forward;

        bool hitOk;
        RaycastHit hit;

        if (aimSphereRadius > 0f)
            hitOk = Physics.SphereCast(o, aimSphereRadius, d, out hit, aimMaxDistance, aimLayerMask, QueryTriggerInteraction.Ignore);
        else
            hitOk = Physics.Raycast(o, d, out hit, aimMaxDistance, aimLayerMask, QueryTriggerInteraction.Ignore);

        if (debugRay)
            Debug.DrawRay(o, d * aimMaxDistance, hitOk ? Color.green : Color.red);

        SetVisible(hitOk);
    }

    // ===== API =====
    public void SetVisible(bool on)
    {
        _visible = on;

        if (rawCg) rawCg.alpha = on ? 1f : 0f;
        if (ringCg) ringCg.alpha = on ? 1f : 0f;
        if (ringGlowCg) ringGlowCg.alpha = on ? 1f : 0f;

        // opcional: apagar la cam para ahorrar
        if (camUI) camUI.enabled = on;
    }

    public void Pulse()
    {
        multPulse = Mathf.Max(multPulse, Mathf.Max(1.01f, pulseEscala));
    }

    // Por si querés cambiar textos en runtime
    public void SetTecla(string nuevaTecla)
    {
        tecla = nuevaTecla;
        if (teclaTMP) teclaTMP.text = nuevaTecla;
    }

    public void SetTexto2(string nuevoTexto)
    {
        texto2 = nuevoTexto;
        if (texto2TMP) texto2TMP.text = nuevoTexto;
    }

    // ===== UI extras =====
    TextMeshProUGUI CreateTMP(Transform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font,
        TextAlignmentOptions alignment, Vector2 offset, float outlineW, Color outlineC)
    {
        if (!parent) return null;

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = offset;
        r.sizeDelta = new Vector2(220, 70);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        if (font) tmp.font = font;

        tmp.outlineWidth = Mathf.Clamp01(outlineW);
        tmp.outlineColor = outlineC;

        return tmp;
    }

    void CreateRing(Transform parent, RectTransform rtRaw)
    {
        // ---- Base ring ----
        ringGO = new GameObject(rawName + "_Ring", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ringGO.transform.SetParent(parent, false);

        ringRT = ringGO.GetComponent<RectTransform>();
        ringRT.sizeDelta = rtRaw.sizeDelta * aroSizeMul;
        ringRT.anchorMin = rtRaw.anchorMin;
        ringRT.anchorMax = rtRaw.anchorMax;
        ringRT.pivot = rtRaw.pivot;
        ringRT.anchoredPosition = rtRaw.anchoredPosition;

        ringImg = ringGO.GetComponent<Image>();
        ringImg.raycastTarget = false;

        Color c = aroColor * aroColorIntensity;
        c.a = 1f;
        ringImg.color = c;

        ringCg = ringGO.GetComponent<CanvasGroup>();
        ringCg.blocksRaycasts = false;
        ringCg.interactable = false;

        ringSprite = CreateArcRingSprite(256, aroAlpha, aroTrail, aroSpriteGlowBoost);
        ringImg.sprite = ringSprite;

        ApplyRingMaterial(ringImg);

        // dejarlo detras del raw
        ringGO.transform.SetSiblingIndex(rtRaw.transform.GetSiblingIndex());

        // ---- Extra glow ring (suave) ----
        if (aroGlowExtra)
        {
            ringGlowGO = new GameObject(rawName + "_RingGlow", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            ringGlowGO.transform.SetParent(parent, false);

            ringGlowRT = ringGlowGO.GetComponent<RectTransform>();
            ringGlowRT.sizeDelta = rtRaw.sizeDelta * aroGlowSizeMul;
            ringGlowRT.anchorMin = rtRaw.anchorMin;
            ringGlowRT.anchorMax = rtRaw.anchorMax;
            ringGlowRT.pivot = rtRaw.pivot;
            ringGlowRT.anchoredPosition = rtRaw.anchoredPosition;

            ringGlowImg = ringGlowGO.GetComponent<Image>();
            ringGlowImg.raycastTarget = false;

            // un poco menos intenso que el aro base, pero “abre” el brillo
            Color cg = aroColor * Mathf.Max(1f, aroColorIntensity * 0.85f);
            cg.a = 1f;
            ringGlowImg.color = cg;

            ringGlowCg = ringGlowGO.GetComponent<CanvasGroup>();
            ringGlowCg.blocksRaycasts = false;
            ringGlowCg.interactable = false;

            ringGlowSprite = CreateArcRingSprite(256, aroGlowAlpha, aroGlowTrail, Mathf.Max(1.2f, aroSpriteGlowBoost * 1.15f));
            ringGlowImg.sprite = ringGlowSprite;

            ApplyRingMaterial(ringGlowImg);

            // detrás del aro base
            ringGlowGO.transform.SetSiblingIndex(ringGO.transform.GetSiblingIndex());
        }
    }

    void ApplyRingMaterial(Image img)
    {
        if (!img) return;

        // Si el usuario asignó material, lo respetamos.
        if (aroMaterialOverride)
        {
            img.material = aroMaterialOverride;
            return;
        }

        // Intento de material aditivo runtime (si existe el shader).
        if (!aroUsarMaterialAdditive) return;

        Shader s = Shader.Find("UI/Particles/Additive");
        if (!s) s = Shader.Find("Particles/Additive"); // fallback
        if (!s) return;

        if (!_aroMatRuntime)
            _aroMatRuntime = new Material(s);

        img.material = _aroMatRuntime;
    }

    static Sprite CreateArcRingSprite(int size, float alphaMul, float trail01, float glowBoost)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float maxR = Mathf.Min(cx, cy);

        float ringR = 0.46f;
        float thick = 0.035f * Mathf.Lerp(0.95f, 1.25f, Mathf.InverseLerp(0.5f, 3f, glowBoost));
        float headBoost = 1.35f * glowBoost;

        float trailRad = Mathf.Lerp(0.7f, 5.2f, trail01);
        float baseGlow = 0.08f * glowBoost;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / maxR;
                float dy = (y - cy) / maxR;

                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float aRing = 1f - Mathf.Abs(r - ringR) / thick;
                aRing = Mathf.Clamp01(aRing);
                aRing = aRing * aRing;

                if (aRing <= 0f)
                {
                    tex.SetPixel(x, y, new Color(1, 1, 1, 0));
                    continue;
                }

                float ang = Mathf.Atan2(dy, dx);
                if (ang < 0f) ang += Mathf.PI * 2f;

                // "cabeza" del arco en ang = 0
                float delta = ang;
                float tail = Mathf.Exp(-delta / Mathf.Max(0.0001f, trailRad));
                float head = Mathf.Exp(-delta / 0.18f) * headBoost;

                float glow = baseGlow + tail + head;
                glow = Mathf.Clamp01(glow);

                float a = aRing * glow * alphaMul;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        if (camUI) Destroy(camUI.gameObject);

        if (rt)
        {
            rt.Release();
            Destroy(rt);
        }

        if (ringSprite) Destroy(ringSprite);
        if (ringGlowSprite) Destroy(ringGlowSprite);

        if (destruirRawAlDestruir && raw)
            Destroy(raw.gameObject);

        if (ringGO) Destroy(ringGO);
        if (ringGlowGO) Destroy(ringGlowGO);

        if (_aroMatRuntime) Destroy(_aroMatRuntime);
    }

    static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            if (child) SetLayerRecursively(child.gameObject, newLayer);
    }

    static Bounds GetBoundsWorld(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(t.position, Vector3.one * 0.1f);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }
}