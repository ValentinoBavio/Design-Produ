using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChipFloatGlow : MonoBehaviour
{
    [Header("FLOAT")]
    public bool useLocal = true;
    public float floatAmplitude = 0.25f;
    public float floatFrequency = 1.15f;

    [Header("SPIN")]
    public Vector3 rotationSpeed = new Vector3(0f, 110f, 0f);

    [Header("PSX VIBE (jitter)")]
    public bool psxJitter = true;
    public float jitterGrid = 0.01f;

    [Header("OPCIONAL: pulse de escala")]
    public bool pulseScale = false;
    public float scalePulseAmount = 0.04f;
    public float scalePulseSpeed = 2.0f;

    [Header("GLOW (SIN cambiar el color del chip)")]
    [Tooltip("Crea un halo (Sprite) alrededor. NO toca el material del chip.")]
    public bool useHaloSprite = true;

    [Tooltip("Color del resplandor (teal recomendado para tu chip).")]
    public Color glowColor = new Color(0f, 0.78f, 0.41f, 1f);

    [Range(0f, 1f)]
    public float haloAlpha = 0.75f;

    [Tooltip("Tamaño del halo (en unidades).")]
    public float haloSize = 1.1f;

    [Tooltip("El halo mira a la cámara (billboard).")]
    public bool haloBillboardToCamera = true;

    [Tooltip("Alejar/pegar el halo al chip en el eje forward local.")]
    public float haloForwardOffset = 0.02f;

    [Header("Pulse del halo")]
    public bool pulseGlow = true;
    [Range(0f, 1f)] public float glowPulseAmount = 0.35f;
    public float glowPulseSpeed = 2.0f;

    [Header("OPCIONAL: LIGHT REAL (no cambia el albedo del chip)")]
    public bool addPointLight = false;
    public Vector3 lightLocalOffset = Vector3.zero;
    public float lightRange = 2.5f;
    public float lightIntensity = 8f;

    // runtime
    Vector3 _startPos;
    Vector3 _startScale;
    float _phase;

    // Halo
    Transform _haloT;
    SpriteRenderer _haloSR;
    Sprite _haloSprite;

    // Light
    Light _light;

    void Awake()
    {
        CacheStartPose();

        if (useHaloSprite)
            SetupHalo();

        if (addPointLight)
            SetupLight();

        ApplyGlow(1f);
    }

    void OnEnable()
    {
        CacheStartPose();
        ApplyGlow(1f);
    }

    void CacheStartPose()
    {
        _startPos = useLocal ? transform.localPosition : transform.position;
        _startScale = transform.localScale;
        _phase = Random.value * 10f;
    }

    void LateUpdate()
    {
        float t = Time.time + _phase;

        // FLOAT
        float y = Mathf.Sin(t * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        Vector3 p = _startPos + Vector3.up * y;

        if (psxJitter && jitterGrid > 0f)
            p = Quantize(p, jitterGrid);

        if (useLocal) transform.localPosition = p;
        else transform.position = p;

        // SPIN
        if (rotationSpeed.sqrMagnitude > 0.0001f)
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);

        // SCALE PULSE (opcional)
        if (pulseScale)
        {
            float s = 1f + Mathf.Sin(t * scalePulseSpeed * Mathf.PI * 2f) * scalePulseAmount;
            transform.localScale = _startScale * s;
        }

        // GLOW factor
        float g = 1f;
        if (pulseGlow)
        {
            float wave01 = (Mathf.Sin(t * glowPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            g = 1f - glowPulseAmount + (glowPulseAmount * 2f) * wave01; // oscila alrededor de 1
        }

        // Billboard halo
        if (_haloT && haloBillboardToCamera && Camera.main)
        {
            // mira hacia cámara sin volverse loco con rotación
            Vector3 camFwd = Camera.main.transform.forward;
            _haloT.forward = camFwd;
        }

        ApplyGlow(g);
    }

    void ApplyGlow(float factor)
    {
        // Halo (no toca material del chip)
        if (_haloSR)
        {
            float a = Mathf.Clamp01(haloAlpha * factor);
            _haloSR.color = new Color(glowColor.r, glowColor.g, glowColor.b, a);

            // escala del halo
            float size = haloSize;
            _haloT.localScale = Vector3.one * size;

            // offset forward
            _haloT.localPosition = Vector3.forward * haloForwardOffset;
        }

        // Light (opcional)
        if (_light)
        {
            _light.color = glowColor;
            _light.range = lightRange;
            _light.intensity = lightIntensity * factor;
        }
    }

    void SetupHalo()
    {
        // Crear GO halo
        var go = new GameObject("GlowHalo");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.forward * haloForwardOffset;

        _haloT = go.transform;
        _haloSR = go.AddComponent<SpriteRenderer>();
        _haloSR.sortingOrder = 999; // por si querés que se vea encima en 2D/overlay (ajustable)

        // Sprite radial procedural
        _haloSprite = CreateRadialSprite(128, 128);
        _haloSR.sprite = _haloSprite;

        // Por defecto, SpriteRenderer usa alpha blending => “resplandor suave”.
        // Si querés más “neón”, subí haloAlpha y/o activá Bloom en URP.
    }

    void SetupLight()
    {
        var go = new GameObject("GlowLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = lightLocalOffset;

        _light = go.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = glowColor;
        _light.range = lightRange;
        _light.intensity = lightIntensity;
        _light.shadows = LightShadows.None;
    }

    static Sprite CreateRadialSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float maxR = Mathf.Min(cx, cy);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / maxR; // 0..1
            // Curva suave: centro fuerte, borde 0
            float a = Mathf.Clamp01(1f - r);
            a = a * a;             // más fuerte al centro
            a = Mathf.SmoothStep(0f, 1f, a);

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply(false, true);

        var rect = new Rect(0, 0, w, h);
        var pivot = new Vector2(0.5f, 0.5f);
        // pixelsPerUnit alto para que sea fácil ajustar por "haloSize"
        return Sprite.Create(tex, rect, pivot, 100f);
    }

    static Vector3 Quantize(Vector3 v, float grid)
    {
        return new Vector3(
            Mathf.Round(v.x / grid) * grid,
            Mathf.Round(v.y / grid) * grid,
            Mathf.Round(v.z / grid) * grid
        );
    }
}