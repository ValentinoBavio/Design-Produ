using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class JunkStackAutoSetup : MonoBehaviour
{
    [Header("Auto-find")]
    public Camera worldCam;                 // si queda null, usa Camera.main

    // ===================== PIXELATION =====================
    [Header("Pixelation (opcional)")]
    public bool enablePixelation = true;
    [Tooltip("Altura en píxeles para renderizar el mundo (ancho se calcula por aspect).")]
    public int pixelHeight = 180;
    public bool keepAspectFromScreen = true;

    // ===================== OVERLAY TOGGLES =====================
    [Header("Overlay Toggles (apagado real)")]
    public bool enableGrain = true;
    public bool enableScanlines = true;
    public bool enableDirt = true;
    public bool enableDither = true;

    // ===================== OVERLAY INTENSITIES =====================
    [Header("Overlay Intensity")]
    [Range(0f, 1f)] public float grainAlpha = 0.10f;
    [Range(0f, 1f)] public float scanlinesAlpha = 0.06f;
    [Range(0f, 1f)] public float dirtAlpha = 0.07f;
    [Range(0f, 1f)] public float ditherAlpha = 0.04f;

    // ===================== OVERLAY ANIMATION =====================
    [Header("Animation Toggles")]
    public bool animateGrain = true;
    public bool animateScanlines = true;
    public bool animateDirt = false;
    public bool animateDither = false;

    [Header("Motion (si te molesta diagonal, poné Grain Scroll en 0,0)")]
    public Vector2 grainScroll = Vector2.zero;                 // <- por defecto NO se mueve (evita diagonal)
    public Vector2 scanlinesScroll = new Vector2(0f, -0.10f);   // opcional: leve caída vertical
    public Vector2 dirtScroll = Vector2.zero;
    public Vector2 ditherScroll = Vector2.zero;

    [Header("Pulse (multiplicativo: si alpha=0 desaparece)")]
    [Range(0f, 1f)] public float grainPulse = 0.25f;
    [Range(0f, 1f)] public float scanlinesPulse = 0.15f;
    [Range(0f, 1f)] public float dirtPulse = 0.10f;
    [Range(0f, 1f)] public float ditherPulse = 0.10f;

    public float grainPulseSpeed = 0.9f;
    public float scanlinesPulseSpeed = 0.6f;
    public float dirtPulseSpeed = 0.35f;
    public float ditherPulseSpeed = 0.5f;

    // ===================== SCANLINES / DIRT GENERATION =====================
    [Header("Scanlines Generation")]
    public int scanlineThickness = 1;
    public int scanlineGap = 2;
    [Range(0f, 1f)] public float scanlineDarkness = 0.45f;

    [Header("Dirt Generation")]
    [Range(0f, 1f)] public float speckDensity = 0.05f;
    public int scratchCount = 22;

    [Header("Seeds")]
    public int seedGrain = 12345;
    public int seedDirt = 23456;

    [Header("Canvas Orders")]
    public int worldCanvasOrder = -5000;    // mundo pixelado debajo del HUD normal
    public int overlayCanvasOrder = 5000;   // overlays arriba de TODO

    [Header("Lifecycle")]
    public bool dontDestroyOnLoad = false;

    // Internals
    GameObject _worldRoot;
    GameObject _overlayRoot;

    RawImage _worldOutput;

    RawImage _grainImg;
    RawImage _scanImg;
    RawImage _dirtImg;
    RawImage _ditherImg;

    RectTransform _grainRT, _scanRT, _dirtRT, _ditherRT;
    Vector2 _grainBasePos, _scanBasePos, _dirtBasePos, _ditherBasePos;

    RenderTexture _rt;
    int _rtW, _rtH;

    Texture2D _texGrain, _texScan, _texDirt, _texDither;

    void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        Build();
    }

    void OnEnable()
    {
        // Asegura que quede aplicado
        ApplyTogglesAndAlphas(force: true);
    }

    void OnDisable()
    {
        CleanupPixelation();
    }

    void OnValidate()
    {
        // Para que cambios del inspector se sientan
        if (Application.isPlaying)
        {
            ApplyTogglesAndAlphas(force: true);
        }
    }

    [ContextMenu("Rebuild Junk Stack")]
    public void Build()
    {
        if (!worldCam) worldCam = Camera.main;

        DestroyIfExistsChild("JUNKSTACK__WORLD");
        DestroyIfExistsChild("JUNKSTACK__OVERLAYS");

        // ===== 1) WORLD PIXELATION CANVAS =====
        if (enablePixelation && worldCam != null)
        {
            _worldRoot = CreateCanvasRoot("JUNKSTACK__WORLD", worldCanvasOrder);
            _worldOutput = CreateFullscreenRawImage(_worldRoot.transform, "WorldOutput");
            _worldOutput.color = Color.white;
            SetupPixelation();
        }
        else
        {
            CleanupPixelation();
        }

        // ===== 2) OVERLAY CANVAS =====
        _overlayRoot = CreateCanvasRoot("JUNKSTACK__OVERLAYS", overlayCanvasOrder);

        _grainImg  = CreateFullscreenRawImage(_overlayRoot.transform, "Overlay_Grain");
        _scanImg   = CreateFullscreenRawImage(_overlayRoot.transform, "Overlay_Scanlines");
        _dirtImg   = CreateFullscreenRawImage(_overlayRoot.transform, "Overlay_Dirt");
        _ditherImg = CreateFullscreenRawImage(_overlayRoot.transform, "Overlay_Dither");

        _grainRT = _grainImg.rectTransform;
        _scanRT = _scanImg.rectTransform;
        _dirtRT = _dirtImg.rectTransform;
        _ditherRT = _ditherImg.rectTransform;

        _grainBasePos = _grainRT.anchoredPosition;
        _scanBasePos = _scanRT.anchoredPosition;
        _dirtBasePos = _dirtRT.anchoredPosition;
        _ditherBasePos = _ditherRT.anchoredPosition;

        // Generar texturas proceduralmente
        _texGrain  = GenerateGrainRGB(256, 256, seedGrain);
        _texScan   = GenerateScanlines(2, 256, scanlineThickness, scanlineGap, scanlineDarkness);
        _texDirt   = GenerateDirt(512, 512, seedDirt, speckDensity, scratchCount);
        _texDither = GenerateBayer4x4();

        _grainImg.texture  = _texGrain;
        _scanImg.texture   = _texScan;
        _dirtImg.texture   = _texDirt;
        _ditherImg.texture = _texDither;

        // Wrap/Filter
        SetTexDefaults(_texGrain,  TextureWrapMode.Repeat, FilterMode.Bilinear);
        SetTexDefaults(_texScan,   TextureWrapMode.Repeat, FilterMode.Point);
        SetTexDefaults(_texDirt,   TextureWrapMode.Repeat, FilterMode.Bilinear);
        SetTexDefaults(_texDither, TextureWrapMode.Repeat, FilterMode.Point);

        // Raycast off
        _grainImg.raycastTarget = false;
        _scanImg.raycastTarget = false;
        _dirtImg.raycastTarget = false;
        _ditherImg.raycastTarget = false;

        ApplyTogglesAndAlphas(force: true);
    }

    void LateUpdate()
    {
        // Rechequea pixelation si cambia la pantalla
        if (enablePixelation && worldCam != null && _worldOutput != null)
            EnsureRT();

        AnimateOverlays();
        ApplyTogglesAndAlphas(force: false);
    }

    // ===================== FIX: APLICA ON/OFF + ALPHAS (ALPHA REAL) =====================
    void ApplyTogglesAndAlphas(bool force)
    {
        if (_grainImg)  _grainImg.gameObject.SetActive(enableGrain);
        if (_scanImg)   _scanImg.gameObject.SetActive(enableScanlines);
        if (_dirtImg)   _dirtImg.gameObject.SetActive(enableDirt);
        if (_ditherImg) _ditherImg.gameObject.SetActive(enableDither);

        // Si una capa está apagada, no hace falta tocar alpha
        if (_grainImg && enableGrain)  SetAlpha(_grainImg, grainAlpha);
        if (_scanImg && enableScanlines) SetAlpha(_scanImg, scanlinesAlpha);
        if (_dirtImg && enableDirt) SetAlpha(_dirtImg, dirtAlpha);
        if (_ditherImg && enableDither) SetAlpha(_ditherImg, ditherAlpha);
    }

    static void SetAlpha(RawImage img, float a)
    {
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    // ===================== OVERLAY ANIMATION (sin “alpha sumado”) =====================
    void AnimateOverlays()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        if (_grainImg && enableGrain)
        {
            AnimateLayer(_grainImg, animateGrain, grainScroll, new Vector2(2f, 2f),
                grainAlpha, grainPulse, grainPulseSpeed, t, dt);
        }

        if (_scanImg && enableScanlines)
        {
            AnimateLayer(_scanImg, animateScanlines, scanlinesScroll, new Vector2(1f, 6f),
                scanlinesAlpha, scanlinesPulse, scanlinesPulseSpeed, t, dt);
        }

        if (_dirtImg && enableDirt)
        {
            AnimateLayer(_dirtImg, animateDirt, dirtScroll, new Vector2(1f, 1f),
                dirtAlpha, dirtPulse, dirtPulseSpeed, t, dt);
        }

        if (_ditherImg && enableDither)
        {
            // el dither queda buenísimo fijo; si lo movés puede sentirse raro
            AnimateLayer(_ditherImg, animateDither, ditherScroll, new Vector2(120f, 68f),
                ditherAlpha, ditherPulse, ditherPulseSpeed, t, dt);
        }
    }

    static void AnimateLayer(RawImage img, bool animate, Vector2 scrollPerSec, Vector2 tiling,
        float baseAlpha, float pulseAmount01, float pulseSpeed,
        float t, float dt)
    {
        // UV tiling siempre (para Repeat)
        Rect uv = img.uvRect;
        uv.width = Mathf.Max(0.0001f, tiling.x);
        uv.height = Mathf.Max(0.0001f, tiling.y);

        if (animate)
        {
            uv.x += scrollPerSec.x * dt;
            uv.y += scrollPerSec.y * dt;
        }
        img.uvRect = uv;

        // PULSE MULTIPLICATIVO:
        // alphaFinal = baseAlpha * (1 + sin(...) * pulseAmount)
        // => si baseAlpha = 0, alphaFinal SIEMPRE 0 (desaparece de verdad)
        float sin = (pulseAmount01 > 0f && pulseSpeed > 0f)
            ? Mathf.Sin(t * pulseSpeed * 6.283185f)
            : 0f;

        float a = Mathf.Clamp01(baseAlpha * (1f + sin * pulseAmount01));
        var col = img.color;
        col.a = a;
        img.color = col;
    }

    // ===================== CANVAS / UI BUILD =====================
    void DestroyIfExistsChild(string name)
    {
        var t = transform.Find(name);
        if (t) Destroy(t.gameObject);
    }

    GameObject CreateCanvasRoot(string name, int sortingOrder)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var ray = root.AddComponent<GraphicRaycaster>();
        ray.enabled = false;

        return root;
    }

    RawImage CreateFullscreenRawImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var raw = go.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        return raw;
    }

    // ===================== PIXELATION =====================
    void SetupPixelation()
    {
        if (!worldCam || !_worldOutput) return;
        EnsureRT();
        worldCam.targetTexture = _rt;
        _worldOutput.texture = _rt;
    }

    void EnsureRT()
    {
        int h = Mathf.Max(16, pixelHeight);
        int w = Mathf.Max(16, h);

        if (keepAspectFromScreen && Screen.height > 0)
        {
            float aspect = (float)Screen.width / Screen.height;
            w = Mathf.Max(16, Mathf.RoundToInt(h * aspect));
        }

        if (_rt && (_rtW != w || _rtH != h))
            CleanupPixelation();

        if (!_rt)
        {
            _rtW = w; _rtH = h;
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default);
            _rt.filterMode = FilterMode.Point; // clave pixel look
            _rt.wrapMode = TextureWrapMode.Clamp;
            _rt.useMipMap = false;
            _rt.autoGenerateMips = false;
            _rt.Create();
        }
    }

    void CleanupPixelation()
    {
        if (worldCam) worldCam.targetTexture = null;

        if (_rt)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }

    // ===================== PROCEDURAL TEXTURES =====================
    static void SetTexDefaults(Texture2D tex, TextureWrapMode wrap, FilterMode filter)
    {
        if (!tex) return;
        tex.wrapMode = wrap;
        tex.filterMode = filter;
    }

    static Texture2D GenerateGrainRGB(int w, int h, int seed)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        UnityEngine.Random.InitState(seed);
        var px = new Color32[w * h];

        for (int i = 0; i < px.Length; i++)
        {
            byte r = (byte)UnityEngine.Random.Range(0, 256);
            byte g = (byte)UnityEngine.Random.Range(0, 256);
            byte b = (byte)UnityEngine.Random.Range(0, 256);
            byte a = 255; // el alpha final lo controla RawImage.color.a
            px[i] = new Color32(r, g, b, a);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D GenerateScanlines(int w, int h, int thickness, int gap, float darkness)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        thickness = Mathf.Max(1, thickness);
        gap = Mathf.Max(0, gap);
        darkness = Mathf.Clamp01(darkness);

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            bool isLine = (y % (thickness + gap)) < thickness;
            byte a = isLine ? (byte)Mathf.RoundToInt(255f * darkness) : (byte)0;

            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(0, 0, 0, a);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D GenerateDirt(int w, int h, int seed, float speckDensity, int scratchCount)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        UnityEngine.Random.InitState(seed);
        speckDensity = Mathf.Clamp01(speckDensity);
        scratchCount = Mathf.Max(0, scratchCount);

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        int specks = Mathf.RoundToInt(w * h * speckDensity);
        for (int i = 0; i < specks; i++)
        {
            int x = UnityEngine.Random.Range(0, w);
            int y = UnityEngine.Random.Range(0, h);
            byte a = (byte)UnityEngine.Random.Range(20, 150);
            px[y * w + x] = new Color32(0, 0, 0, a);
        }

        for (int s = 0; s < scratchCount; s++)
        {
            int x0 = UnityEngine.Random.Range(0, w);
            int y0 = UnityEngine.Random.Range(0, h);
            int x1 = Mathf.Clamp(x0 + UnityEngine.Random.Range(-w / 2, w / 2), 0, w - 1);
            int y1 = Mathf.Clamp(y0 + UnityEngine.Random.Range(-h / 2, h / 2), 0, h - 1);
            byte a = (byte)UnityEngine.Random.Range(60, 160);
            DrawLine(px, w, h, x0, y0, x1, y1, new Color32(0, 0, 0, a));
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D GenerateBayer4x4()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        int[,] b = {
            { 0,  8,  2, 10},
            {12,  4, 14,  6},
            { 3, 11,  1,  9},
            {15,  7, 13,  5}
        };

        var px = new Color32[16];
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            float v = b[y, x] / 15f;
            byte a = (byte)Mathf.RoundToInt(255f * v * 0.40f);
            px[y * 4 + x] = new Color32(0, 0, 0, a);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static void DrawLine(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 c)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            int idx = y0 * w + x0;
            if (idx >= 0 && idx < px.Length) px[idx] = c;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
