using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class ChipProximityLoopSFX : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip loopClip;

    [Tooltip("Para respetar tu AudioMixer (SFX). Opcional.")]
    public AudioMixerGroup mixerGroup;

    [Range(0f, 1f)] public float maxVolume = 0.25f;

    [Tooltip("Fade in/out para evitar clicks.")]
    public float fadeIn = 0.10f;
    public float fadeOut = 0.12f;

    [Header("Proximidad")]
    [Tooltip("Distancia a la que suena al 100%.")]
    public float minDistance = 1.5f;

    [Tooltip("Distancia a la que queda en 0 (mudo).")]
    public float maxDistance = 10f;

    [Tooltip("Curva de volumen según distancia normalizada (0 = cerca, 1 = lejos).")]
    public AnimationCurve volumeByDistance = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Target (Player)")]
    [Tooltip("Si encuentra un objeto con este tag, usa ese como target.")]
    public string playerTag = "Player";

    [Tooltip("Si está ON y no encuentra Player, usa la Camera.main.")]
    public bool fallbackToMainCamera = true;

    [Header("Opcional: Spatial (3D)")]
    [Tooltip("Si querés paneo/posicionamiento 3D además del volumen por distancia.")]
    public bool use3DSpatial = true;

    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float dopplerLevel = 0f;

    [Header("Performance")]
    [Tooltip("Actualizar cada X segundos (0 = cada frame).")]
    public float updateInterval = 0.05f;

    AudioSource _src;
    Transform _target;
    float _currentVol;
    float _nextUpdate;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        if (!_src) _src = gameObject.AddComponent<AudioSource>();

        // Config base
        _src.playOnAwake = false;
        _src.loop = true;
        _src.clip = loopClip;
        _src.volume = 0f;

        if (mixerGroup) _src.outputAudioMixerGroup = mixerGroup;

        // Spatial opcional
        _src.spatialBlend = use3DSpatial ? Mathf.Clamp01(spatialBlend) : 0f;
        _src.dopplerLevel = dopplerLevel;

        // No usamos el rolloff para volumen principal (lo hacemos nosotros)
        // pero igual dejamos min/max del AudioSource coherentes si usás spatial:
        _src.minDistance = Mathf.Max(0.01f, minDistance);
        _src.maxDistance = Mathf.Max(_src.minDistance + 0.01f, maxDistance);
        _src.rolloffMode = AudioRolloffMode.Linear;

        ResolveTarget();

        if (_src.clip) _src.Play();
    }

    void OnEnable()
    {
        if (_src && _src.clip && !_src.isPlaying) _src.Play();
        ResolveTarget();
    }

    void OnDisable()
    {
        // Fade out rápido al deshabilitar
        if (_src) _src.volume = 0f;
    }

    void ResolveTarget()
    {
        // Player por tag
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go) { _target = go.transform; return; }

        if (fallbackToMainCamera && Camera.main)
            _target = Camera.main.transform;
    }

    void Update()
    {
        if (!_src || !_src.clip) return;

        if (!_target)
        {
            ResolveTarget();
            if (!_target)
            {
                // sin target, mute
                _src.volume = 0f;
                return;
            }
        }

        if (updateInterval > 0f && Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;

        float d = Vector3.Distance(transform.position, _target.position);

        // 0..1 (0 cerca, 1 lejos)
        float t = Mathf.InverseLerp(minDistance, maxDistance, d);
        t = Mathf.Clamp01(t);

        float curve = volumeByDistance.Evaluate(t); // 1 cerca, 0 lejos (por default)
        float targetVol = Mathf.Clamp01(curve) * maxVolume;

        // Fade suave
        float fade = (targetVol > _currentVol) ? fadeIn : fadeOut;
        float k = (fade <= 0f) ? 1f : (1f - Mathf.Exp(-Time.deltaTime / fade));

        _currentVol = Mathf.Lerp(_currentVol, targetVol, k);
        _src.volume = _currentVol;

        // Si querés ahorrar CPU, podemos pausar cuando está re lejos:
        if (_currentVol <= 0.0005f)
        {
            if (_src.isPlaying) _src.Pause();
        }
        else
        {
            if (!_src.isPlaying) _src.UnPause();
        }
    }
}
