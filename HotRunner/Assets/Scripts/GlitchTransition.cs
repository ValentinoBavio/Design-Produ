using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class GlitchTransition : MonoBehaviour
{
    public static GlitchTransition Instance { get; private set; }

    [Header("Overlay (SpriteRenderer)")]
    [Tooltip("SpriteRenderer que cubre pantalla. (Un GameObject hijo con SpriteRenderer).")]
    public SpriteRenderer overlay;

    [Tooltip("Si está vacío, usa Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Distancia delante de la cámara.")]
    public float distanceFromCamera = 0.20f;

    [Header("Frames (tus 14 sprites)")]
    public Sprite[] frames;
    [Range(1f, 60f)] public float fps = 24f;
    public bool randomFrames = true;
    [Range(0f, 1f)] public float overlayAlpha = 1f;

    [Header("SFX (no se corta)")]
    public AudioClip glitchSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioMixerGroup sfxMixerGroup;

    [Header("Duración")]
    [Tooltip("0 = usa glitchSfx.length. Si no hay clip, usa frames.Length/fps.")]
    public float durationOverride = 0f;

    [Tooltip("Failsafe extra por si algo raro pasa.")]
    public float failSafeExtraSeconds = 0.50f;

    [Header("Opcional")]
    public bool dontDestroyOnLoad = true;
    public bool debugLogs = false;

    [Header("Compatibilidad FOV Boost (SpeedFovLinesFX)")]
    [Tooltip("Si está ON: desactiva temporalmente SpeedFovLinesFX mientras dura el glitch, para que el overlay no se achique por FOV boost.")]
    public bool disableSpeedFovLinesWhilePlaying = true;

    AudioSource _src;
    bool _playing;
    float _expectedEndUnscaled;
    int _seq;

    Transform _originalParent;
    Vector3 _originalLocalPos;
    Quaternion _originalLocalRot;
    Vector3 _originalLocalScale;

    // cache de SpeedFovLinesFX
    readonly List<SpeedFovLinesFX> _speedFx = new List<SpeedFovLinesFX>();
    readonly List<bool> _speedFxPrevEnabled = new List<bool>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialBlend = 0f;
        _src.ignoreListenerPause = true;
        if (sfxMixerGroup != null) _src.outputAudioMixerGroup = sfxMixerGroup;

        CacheOverlayOriginalTransform();
        ForceHideOverlay();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        HardReset();
    }

    void LateUpdate()
    {
        if (_playing && Time.unscaledTime > _expectedEndUnscaled + Mathf.Max(0.05f, failSafeExtraSeconds))
        {
            if (debugLogs) Debug.LogWarning("[GlitchTransition] Failsafe: se pasó del tiempo. Apago overlay.");
            HardReset();
        }

        if (!_playing && overlay != null && overlay.gameObject.activeSelf)
            ForceHideOverlay();
    }

    // ========= API =========

    public bool IsPlaying => _playing;

    /// <summary>Glitch (anim+sfx) por tiempo fijo. No cuelga.</summary>
    public IEnumerator PlayRoutine()
    {
        if (_playing) yield break;
        _playing = true;

        float dur = GetDuration();
        _expectedEndUnscaled = Time.unscaledTime + dur;

        try
        {
            // Si venís desde pausa, que no quede timeScale=0 para el resto del juego
            if (Time.timeScale <= 0.0001f)
                Time.timeScale = 1f;

            // ✅ FIX: apagar temporalmente FOV boost / speedlines (para que overlay no se achique)
            if (disableSpeedFovLinesWhilePlaying)
                DisableSpeedFxTemporarily();

            if (overlay == null || frames == null || frames.Length == 0)
            {
                if (debugLogs) Debug.LogWarning("[GlitchTransition] Falta overlay o frames. Haré solo SFX/espera.");
                PlaySfx();
                yield return new WaitForSecondsRealtime(dur);
                yield break;
            }

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
            {
                if (debugLogs) Debug.LogWarning("[GlitchTransition] No hay cámara (Camera.main null). Solo SFX/espera.");
                PlaySfx();
                yield return new WaitForSecondsRealtime(dur);
                yield break;
            }

            AttachOverlayToCamera(cam);

            overlay.gameObject.SetActive(true);

            StepFrame();
            FitOverlayToCamera(cam);

            PlaySfx();

            float interval = 1f / Mathf.Max(1f, fps);
            float nextStep = Time.unscaledTime + interval;

            while (Time.unscaledTime < _expectedEndUnscaled)
            {
                if (Time.unscaledTime >= nextStep)
                {
                    StepFrame();
                    FitOverlayToCamera(cam);
                    nextStep += interval;
                }
                yield return null;
            }
        }
        finally
        {
            ForceHideOverlay();

            // ✅ restore de SpeedFovLinesFX
            if (disableSpeedFovLinesWhilePlaying)
                RestoreSpeedFx();

            _playing = false;
        }
    }

    public void PlayThenLoadScene(string sceneName)
    {
        if (_playing) return;
        StartCoroutine(PlayThenLoadRoutine(sceneName));
    }

    IEnumerator PlayThenLoadRoutine(string sceneName)
    {
        yield return PlayRoutine();
        SceneManager.LoadScene(sceneName);
    }

    public void HardReset()
    {
        StopAllCoroutines();
        if (_src != null) _src.Stop();
        _playing = false;
        ForceHideOverlay();

        if (disableSpeedFovLinesWhilePlaying)
            RestoreSpeedFx();
    }

    // ========= Internals =========

    float GetDuration()
    {
        if (durationOverride > 0.001f) return durationOverride;
        if (glitchSfx != null) return glitchSfx.length;
        if (frames != null && frames.Length > 0) return frames.Length / Mathf.Max(1f, fps);
        return 0.25f;
    }

    void PlaySfx()
    {
        // ✅ Si hay GameOver activo, no reproducir nada
        if (GameOverManager.IsGameOverActive)
            return;

        if (glitchSfx == null) return;

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (_src == null || !_src.enabled || !_src.gameObject.activeInHierarchy)
            return;

        _src.loop = false;

        if (sfxMixerGroup != null && _src.outputAudioMixerGroup != sfxMixerGroup)
            _src.outputAudioMixerGroup = sfxMixerGroup;

        _src.Stop();
        _src.clip = glitchSfx;
        _src.volume = sfxVolume;
        _src.Play();
    }

    void StepFrame()
    {
        if (overlay == null || frames == null || frames.Length == 0) return;

        Sprite s;
        if (randomFrames)
            s = frames[Random.Range(0, frames.Length)];
        else
        {
            _seq = (_seq + 1) % frames.Length;
            s = frames[_seq];
        }

        overlay.sprite = s;

        var c = overlay.color;
        c.a = overlayAlpha;
        overlay.color = c;

        overlay.sortingOrder = 32767;
    }

    void CacheOverlayOriginalTransform()
    {
        if (overlay == null) return;

        Transform t = overlay.transform;
        _originalParent = t.parent;
        _originalLocalPos = t.localPosition;
        _originalLocalRot = t.localRotation;
        _originalLocalScale = t.localScale;
    }

    void AttachOverlayToCamera(Camera cam)
    {
        if (overlay == null) return;

        if (_originalParent == null)
            CacheOverlayOriginalTransform();

        Transform t = overlay.transform;
        t.SetParent(cam.transform, worldPositionStays: false);

        float dist = Mathf.Max(cam.nearClipPlane + 0.05f, distanceFromCamera);
        t.localPosition = new Vector3(0f, 0f, dist);
        t.localRotation = Quaternion.identity;
    }

    void FitOverlayToCamera(Camera cam)
    {
        if (overlay == null || overlay.sprite == null) return;

        float dist = Mathf.Max(cam.nearClipPlane + 0.05f, distanceFromCamera);

        float viewH, viewW;
        if (cam.orthographic)
        {
            viewH = cam.orthographicSize * 2f;
            viewW = viewH * cam.aspect;
        }
        else
        {
            viewH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            viewW = viewH * cam.aspect;
        }

        Vector2 spriteSize = overlay.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

        float sx = (viewW / spriteSize.x) * 1.02f;
        float sy = (viewH / spriteSize.y) * 1.02f;

        overlay.transform.localScale = new Vector3(sx, sy, 1f);
    }

    void ForceHideOverlay()
    {
        if (overlay == null) return;

        overlay.gameObject.SetActive(false);

        Transform t = overlay.transform;

        if (_originalParent != null && t.parent != _originalParent)
            t.SetParent(_originalParent, worldPositionStays: false);

        t.localPosition = _originalLocalPos;
        t.localRotation = _originalLocalRot;
        t.localScale = _originalLocalScale;
    }

    // ======== SpeedFovLinesFX integration ========

    void DisableSpeedFxTemporarily()
    {
        _speedFx.Clear();
        _speedFxPrevEnabled.Clear();

        // encuentra aunque estén desactivados en jerarquía (true)
        var fx = FindObjectsOfType<SpeedFovLinesFX>(true);
        for (int i = 0; i < fx.Length; i++)
        {
            if (!fx[i]) continue;
            _speedFx.Add(fx[i]);
            _speedFxPrevEnabled.Add(fx[i].enabled);

            // deshabilitar para que se des-suscriba del RenderPipeline y no meta FOV boost
            fx[i].enabled = false;
        }
    }

    void RestoreSpeedFx()
    {
        if (_speedFx.Count == 0) return;

        for (int i = 0; i < _speedFx.Count; i++)
        {
            var fx = _speedFx[i];
            if (!fx) continue;

            bool wasEnabled = (i < _speedFxPrevEnabled.Count) ? _speedFxPrevEnabled[i] : true;
            fx.enabled = wasEnabled;
        }

        _speedFx.Clear();
        _speedFxPrevEnabled.Clear();
    }
}
