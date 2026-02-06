using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class HoloSignTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Collider triggerCol;

    [Header("Holograma (Root opcional)")]
    [SerializeField] private GameObject holoRoot;

    [Tooltip("Arrastrá SOLO los Renderers que querés afectar (letras/panel). " +
             "Por defecto deben tener tu material HOLO original (NO el CRT agregado a mano).")]
    [SerializeField] private Renderer[] renderers;

    [Header("Material CRT (Shader Graph nuevo)")]
    [SerializeField] private Material crtMaterial;

    [Header("Shader Props CRT (Reference exacto)")]
    [SerializeField] private string propAlpha = "Alpha";
    [SerializeField] private string propWarpStrength = "WarpStrength";
    [SerializeField] private string propWarpFreq = "WarpFreq";
    [SerializeField] private string propWarpSpeed = "WarpSpeed";
    [SerializeField] private string propWarpBands = "WarpBands";

    [Header("Opcional (si existen en tu CRT)")]
    [SerializeField] private string propNoiseScale = "NoiseScale";
    [SerializeField] private string propNoiseSpeed = "NoiseSpeed";
    [SerializeField] private string propScanFreq = "ScanFreq";
    [SerializeField] private string propFlickerAmount = "FlickerAmount";

    [Header("Intro Overlay (para el primer cartel al spawn)")]
    [Tooltip("Si tu nivel tiene Level1IntroOverlay que frena el juego, esto evita que el trigger se dispare durante el intro.")]
    [SerializeField] private bool waitForIntroOverlay = true;

    [Tooltip("Delay real-time después del intro antes de chequear el trigger (1-2 frames suele bastar).")]
    [SerializeField] private float postIntroDelay = 0.05f;

    [Header("Comportamiento")]
    [SerializeField] private bool hideOnExit = true;
    [SerializeField] private bool showOnce = false;
    [SerializeField] private bool startHidden = true;

    [Header("Tiempo (anti-bug de timeScale=0)")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Aparición tipo CRT (carga)")]
    [SerializeField] private float glitchDuration = 0.75f;
    [Range(2, 30)] [SerializeField] private int steps = 10;

    [SerializeField] private float showWarpStrength = 0.06f;
    [SerializeField] private float showWarpFreq = 24f;
    [SerializeField] private float showWarpSpeed = 8f;
    [SerializeField] private float showWarpBands = 120f;

    [SerializeField] private float glitchNoiseScaleMul = 4f;
    [SerializeField] private float glitchNoiseSpeedMul = 4f;
    [SerializeField] private float glitchScanFreqMul = 4f;
    [SerializeField] private float glitchFlickerAmountMul = 4f;

    [Header("Desaparición")]
    [SerializeField] private float disappearDuration = 0.18f;
    [SerializeField] private float hideWarpStrength = 0.04f;

    [Header("Audio (opcional, respeta AudioMixer)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private AudioClip sfxEnter;
    [SerializeField] private AudioClip sfxExit;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;

    // ---- runtime ----
    private enum Phase { Hidden, Shown, Showing, Hiding }
    private Phase _phase;

    private readonly Dictionary<Renderer, Material[]> _originalMats = new();
    private MaterialPropertyBlock _mpb;
    private Coroutine _co;
    private bool _crtApplied;
    private bool _playerInside;

    // IDs
    private int _idAlpha, _idWarpStrength, _idWarpFreq, _idWarpSpeed, _idWarpBands;
    private int _idNoiseScale, _idNoiseSpeed, _idScanFreq, _idFlickerAmount;

    // base values CRT (si existen)
    private float _baseNoiseScale, _baseNoiseSpeed, _baseScanFreq, _baseFlickerAmount;

    private bool _hasAlpha, _hasWarpStrength, _hasWarpFreq, _hasWarpSpeed, _hasWarpBands;
    private bool _hasNoiseScale, _hasNoiseSpeed, _hasScanFreq, _hasFlickerAmount;

    void Reset()
    {
        triggerCol = GetComponent<Collider>();
        if (triggerCol) triggerCol.isTrigger = true;
    }

    void Awake()
    {
        if (!triggerCol) triggerCol = GetComponent<Collider>();
        if (renderers == null) renderers = new Renderer[0];

        _mpb = new MaterialPropertyBlock();

        _idAlpha = Shader.PropertyToID(propAlpha);
        _idWarpStrength = Shader.PropertyToID(propWarpStrength);
        _idWarpFreq = Shader.PropertyToID(propWarpFreq);
        _idWarpSpeed = Shader.PropertyToID(propWarpSpeed);
        _idWarpBands = Shader.PropertyToID(propWarpBands);

        _idNoiseScale = Shader.PropertyToID(propNoiseScale);
        _idNoiseSpeed = Shader.PropertyToID(propNoiseSpeed);
        _idScanFreq = Shader.PropertyToID(propScanFreq);
        _idFlickerAmount = Shader.PropertyToID(propFlickerAmount);

        SetupAudio();
        CacheOriginalMaterials();
        CacheCrtFlagsAndBase();

        if (startHidden)
        {
            if (holoRoot) holoRoot.SetActive(false);
            _phase = Phase.Hidden;
        }
        else
        {
            if (holoRoot) holoRoot.SetActive(true);
            _phase = Phase.Shown;
        }
    }

    void OnEnable()
    {
        // Si el componente se habilita justo después del intro, puede que ya estés adentro del trigger.
        if (gameObject.activeInHierarchy)
            StartCoroutine(CoPostEnableCheck());
    }

    void OnDisable()
    {
        ForceCleanupToOriginal();
        _playerInside = false;
    }

    void OnDestroy()
    {
        ForceCleanupToOriginal();
        _playerInside = false;
    }

    IEnumerator CoPostEnableCheck()
    {
        // Espera 1 frame
        yield return null;

        if (waitForIntroOverlay)
        {
            // Esperar a que termine el intro (usa tu flag global)
            while (Level1IntroOverlay.IsIntroActive)
                yield return null;

            // mini delay para que se estabilice el player/physics
            if (postIntroDelay > 0f)
                yield return new WaitForSecondsRealtime(postIntroDelay);
        }

        // Si ya estás dentro del trigger, dispará el show
        if (IsPlayerInsideNow())
        {
            _playerInside = true;
            Show();
        }
    }

    void SetupAudio()
    {
        if (!audioSource)
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (audioSource && mixerGroup) audioSource.outputAudioMixerGroup = mixerGroup;
        if (audioSource) audioSource.playOnAwake = false;
    }

    void CacheOriginalMaterials()
    {
        _originalMats.Clear();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;
            _originalMats[r] = r.sharedMaterials; // tus materiales HOLO originales
        }
    }

    void CacheCrtFlagsAndBase()
    {
        if (!crtMaterial) return;

        _hasAlpha = crtMaterial.HasProperty(_idAlpha);
        _hasWarpStrength = crtMaterial.HasProperty(_idWarpStrength);
        _hasWarpFreq = crtMaterial.HasProperty(_idWarpFreq);
        _hasWarpSpeed = crtMaterial.HasProperty(_idWarpSpeed);
        _hasWarpBands = crtMaterial.HasProperty(_idWarpBands);

        _hasNoiseScale = crtMaterial.HasProperty(_idNoiseScale);
        _hasNoiseSpeed = crtMaterial.HasProperty(_idNoiseSpeed);
        _hasScanFreq = crtMaterial.HasProperty(_idScanFreq);
        _hasFlickerAmount = crtMaterial.HasProperty(_idFlickerAmount);

        if (_hasNoiseScale) _baseNoiseScale = crtMaterial.GetFloat(_idNoiseScale);
        if (_hasNoiseSpeed) _baseNoiseSpeed = crtMaterial.GetFloat(_idNoiseSpeed);
        if (_hasScanFreq) _baseScanFreq = crtMaterial.GetFloat(_idScanFreq);
        if (_hasFlickerAmount) _baseFlickerAmount = crtMaterial.GetFloat(_idFlickerAmount);
    }

    float DT()
    {
        if (useUnscaledTime) return Time.unscaledDeltaTime;
        return Time.deltaTime;
    }

    bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag)) return true;
        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag)) return true;
            t = t.parent;
        }
        return false;
    }

    bool ShouldIgnoreBecauseIntro()
    {
        return waitForIntroOverlay && Level1IntroOverlay.IsIntroActive;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        _playerInside = true;

        // Durante intro no mostramos nada
        if (ShouldIgnoreBecauseIntro()) return;

        if (sfxEnter && audioSource) audioSource.PlayOneShot(sfxEnter, sfxVolume);
        Show();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        _playerInside = false;

        if (!hideOnExit) return;
        if (ShouldIgnoreBecauseIntro()) return;

        if (sfxExit && audioSource) audioSource.PlayOneShot(sfxExit, sfxVolume);
        Hide();
    }

    public void Show()
    {
        if (_phase == Phase.Showing || _phase == Phase.Shown) return;

        StartTransitionCleanup();
        _phase = Phase.Showing;

        if (holoRoot && !holoRoot.activeSelf)
            holoRoot.SetActive(true);

        if (sfxEnter && audioSource) audioSource.PlayOneShot(sfxEnter, sfxVolume);

        _co = StartCoroutine(CoShowCrtThenRestore());

        if (showOnce && triggerCol)
            triggerCol.enabled = false; // se deshabilita después de iniciar el show
    }

    public void Hide()
    {
        if (_phase == Phase.Hiding || _phase == Phase.Hidden) return;

        StartTransitionCleanup();
        _phase = Phase.Hiding;

        if (sfxExit && audioSource) audioSource.PlayOneShot(sfxExit, sfxVolume);

        _co = StartCoroutine(CoHideCrtThenDisable());
    }

    void StartTransitionCleanup()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        if (_crtApplied)
            ForceCleanupToOriginal();
    }

    void ForceCleanupToOriginal()
    {
        RestoreOriginalMaterials();
        ClearPropertyBlocks();
        _crtApplied = false;
    }

    IEnumerator CoShowCrtThenRestore()
    {
        if (!crtMaterial)
        {
            Debug.LogWarning("[HoloSignTrigger] Falta crtMaterial.");
            _phase = Phase.Shown;
            yield break;
        }

        CacheOriginalMaterials();
        ApplyCrtMaterial();
        _crtApplied = true;

        float t = 0f;
        float dur = Mathf.Max(0.001f, glitchDuration);

        while (t < 1f)
        {
            float dt = DT();
            if (!useUnscaledTime && dt <= 0f) dt = 0.016f; // fallback por si timeScale=0
            t += dt / dur;

            float x = Mathf.Clamp01(t);
            float stepped = (steps > 1) ? Mathf.Round(x * steps) / steps : x;

            float alpha = stepped;
            float warp = Mathf.Lerp(showWarpStrength, 0f, x);

            float ns = _baseNoiseScale * Mathf.Lerp(glitchNoiseScaleMul, 1f, x);
            float nv = _baseNoiseSpeed * Mathf.Lerp(glitchNoiseSpeedMul, 1f, x);
            float sf = _baseScanFreq * Mathf.Lerp(glitchScanFreqMul, 1f, x);
            float fa = _baseFlickerAmount * Mathf.Lerp(glitchFlickerAmountMul, 1f, x);

            SetCrtProps(alpha, warp, showWarpFreq, showWarpSpeed, showWarpBands, ns, nv, sf, fa);
            yield return null;
        }

        SetCrtProps(1f, 0f, showWarpFreq, showWarpSpeed, showWarpBands,
                    _baseNoiseScale, _baseNoiseSpeed, _baseScanFreq, _baseFlickerAmount);

        ForceCleanupToOriginal();
        _phase = Phase.Shown;
        _co = null;
    }

    IEnumerator CoHideCrtThenDisable()
    {
        if (!crtMaterial)
        {
            if (holoRoot) holoRoot.SetActive(false);
            _phase = Phase.Hidden;
            yield break;
        }

        CacheOriginalMaterials();
        ApplyCrtMaterial();
        _crtApplied = true;

        float t = 0f;
        float dur = Mathf.Max(0.001f, disappearDuration);

        while (t < 1f)
        {
            float dt = DT();
            if (!useUnscaledTime && dt <= 0f) dt = 0.016f;
            t += dt / dur;

            float x = Mathf.Clamp01(t);

            float alpha = Mathf.Lerp(1f, 0f, x);
            float warp = Mathf.Lerp(hideWarpStrength, 0f, x);

            SetCrtProps(alpha, warp, showWarpFreq, showWarpSpeed, showWarpBands,
                        _baseNoiseScale, _baseNoiseSpeed, _baseScanFreq, _baseFlickerAmount);

            yield return null;
        }

        if (holoRoot) holoRoot.SetActive(false);

        ForceCleanupToOriginal();
        _phase = Phase.Hidden;
        _co = null;
    }

    void ApplyCrtMaterial()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            int slots = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;

            var mats = new Material[slots];
            for (int m = 0; m < slots; m++) mats[m] = crtMaterial;

            r.sharedMaterials = mats;
        }
    }

    void RestoreOriginalMaterials()
    {
        foreach (var kv in _originalMats)
        {
            if (!kv.Key) continue;
            kv.Key.sharedMaterials = kv.Value;
        }
    }

    void ClearPropertyBlocks()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;
            r.SetPropertyBlock(null);
        }
    }

    void SetCrtProps(float alpha, float warpStrength, float warpFreq, float warpSpeed, float warpBands,
                     float noiseScale, float noiseSpeed, float scanFreq, float flickerAmount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            r.GetPropertyBlock(_mpb);

            if (_hasAlpha) _mpb.SetFloat(_idAlpha, alpha);

            if (_hasWarpStrength) _mpb.SetFloat(_idWarpStrength, warpStrength);
            if (_hasWarpFreq) _mpb.SetFloat(_idWarpFreq, warpFreq);
            if (_hasWarpSpeed) _mpb.SetFloat(_idWarpSpeed, warpSpeed);
            if (_hasWarpBands) _mpb.SetFloat(_idWarpBands, warpBands);

            if (_hasNoiseScale) _mpb.SetFloat(_idNoiseScale, noiseScale);
            if (_hasNoiseSpeed) _mpb.SetFloat(_idNoiseSpeed, noiseSpeed);
            if (_hasScanFreq) _mpb.SetFloat(_idScanFreq, scanFreq);
            if (_hasFlickerAmount) _mpb.SetFloat(_idFlickerAmount, flickerAmount);

            r.SetPropertyBlock(_mpb);
        }
    }

    // --- Chequeo de "ya estoy adentro" ---
    bool IsPlayerInsideNow()
    {
        if (!triggerCol) return false;

        // Detecta colisiones incluso con triggers (por si tu player tiene triggers auxiliares)
        const QueryTriggerInteraction q = QueryTriggerInteraction.Collide;

        // Caja
        if (triggerCol is BoxCollider bc)
        {
            Vector3 center = bc.transform.TransformPoint(bc.center);
            Vector3 half = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale);
            Collider[] hits = Physics.OverlapBox(center, half, bc.transform.rotation, ~0, q);
            return ContainsPlayer(hits);
        }

        // Esfera
        if (triggerCol is SphereCollider sc)
        {
            Vector3 center = sc.transform.TransformPoint(sc.center);
            float radius = sc.radius * MaxAbs(sc.transform.lossyScale);
            Collider[] hits = Physics.OverlapSphere(center, radius, ~0, q);
            return ContainsPlayer(hits);
        }

        // Fallback genérico
        Collider[] hits2 = Physics.OverlapBox(triggerCol.bounds.center, triggerCol.bounds.extents, triggerCol.transform.rotation, ~0, q);
        return ContainsPlayer(hits2);
    }

    bool ContainsPlayer(Collider[] hits)
    {
        if (hits == null) return false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i]) continue;
            if (IsPlayer(hits[i])) return true;
        }
        return false;
    }

    float MaxAbs(Vector3 v)
    {
        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);
        float az = Mathf.Abs(v.z);
        return Mathf.Max(ax, Mathf.Max(ay, az));
    }
}
