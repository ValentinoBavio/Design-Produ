using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraKick : MonoBehaviour
{
    [Header("Refs")]
    public Camera targetCamera;              // si está vacío, usa GetComponent<Camera>()

    [Header("Base")]
    [Tooltip("FOV base al que se vuelve cuando no hay kick activo.")]
    public float baseFOV = 60f;

    [Tooltip("Aplicar en LateUpdate (después de CameraFollow).")]
    public bool applyInLateUpdate = true;

    [Header("Positional Offset")]
    [Tooltip("Si está OFF, el kick NUNCA mueve la posición de la cámara (solo roll + FOV).")]
    public bool allowPositionOffset = false;
    [Tooltip("Límite |Y| de offset si está activo (evita meter la cámara bajo el piso).")]
    public float maxOffsetY = 0.06f;
    [Tooltip("Límite |Z| de offset si está activo (evita atravesar paredes).")]
    public float maxOffsetZ = 0.15f;

    [Header("Shake")]
    [Tooltip("Habilita/deshabilita por completo el shake.")]
    public bool enableShake = true;
    [Tooltip("Si está ON, el shake sólo corre mientras el kick está activo.")]
    public bool shakeOnlyDuringKick = true;
    [Range(0f, 2f)] public float shakeAmplitude = 0.25f;
    [Range(0.5f, 8f)] public float shakeFrequency = 3.0f;
    [Range(0f, 2f)] public float shakeFromKickScale = 0.6f;

    [Header("Lock")]
    public bool lockWhileKicking = false;

    // Estado
    Vector3 _kickLocalOffset;   // objetivo (local)
    float   _kickRollDeg;
    float   _kickFovPunch;
    float   _riseTime, _relaxTime, _timer;
    bool    _active;

    // Cache del frame (antes del kick)
    Vector3    _prePos;
    Quaternion _preRot;

    // ruido (semillas)
    float sxSeed, sySeed, szSeed, srSeed;

    void Reset()
    {
        var cam = GetComponent<Camera>();
        if (!targetCamera && cam) targetCamera = cam;
        if (targetCamera) baseFOV = targetCamera.fieldOfView;
    }

    void Awake()
    {
        if (!targetCamera)
        {
            targetCamera = GetComponent<Camera>();
            if (!targetCamera && Camera.main) targetCamera = Camera.main;
        }
        if (targetCamera) baseFOV = targetCamera.fieldOfView;

        sxSeed = Random.value * 1000f;
        sySeed = Random.value * 1000f;
        szSeed = Random.value * 1000f;
        srSeed = Random.value * 1000f;
    }

    void OnEnable()
    {
        _active = false;
        _timer = 0f;
        if (targetCamera) targetCamera.fieldOfView = baseFOV;
    }

    void OnDisable()
    {
        if (targetCamera) targetCamera.fieldOfView = baseFOV;
    }

    void Update()
    {
        if (!applyInLateUpdate) Tick(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (applyInLateUpdate) Tick(Time.deltaTime);
    }

    // -------- API --------
    public void DoKick(Vector3 localOffset, float rollDeg, float fovPunch, float riseTime, float relaxTime)
    {
        if (lockWhileKicking && _active) return;

        _kickLocalOffset = localOffset;   // se aplicará solo si allowPositionOffset = true
        _kickRollDeg     = rollDeg;
        _kickFovPunch    = fovPunch;
        _riseTime  = Mathf.Max(0.0001f, riseTime);
        _relaxTime = Mathf.Max(0.0001f, relaxTime);
        _timer = 0f;
        _active = true;
    }

    public void KickAndShake(Vector3 localOffset, float roll, float fov, float rise, float relax)
    {
        DoKick(localOffset, roll, fov, rise, relax);
    }

    // -------- Núcleo --------
    void Tick(float dt)
    {
        // pose previa (la dejó CameraFollow)
        _prePos = transform.localPosition;
        _preRot = transform.localRotation;

        // Curva de subida/bajada del kick (k 0..1..0)
        float k = 0f;
        if (_active)
        {
            _timer += dt;
            float total = _riseTime + _relaxTime;

            if (_timer <= _riseTime)
                k = EaseOutCubic(Mathf.Clamp01(_timer / _riseTime));
            else if (_timer <= total)
                k = 1f - EaseOutCubic(Mathf.Clamp01((_timer - _riseTime) / _relaxTime));
            else
            {
                _active = false;
                k = 0f;
            }
        }

        // base: roll y fov por kick
        float roll = _kickRollDeg * k;
        float fov  = _kickFovPunch * k;

        // ¿aplicamos shake?
        bool doShakeNow = enableShake && (_active || !shakeOnlyDuringKick);

        if (doShakeNow)
        {
            float amp = shakeAmplitude + shakeFromKickScale * k;  // más fuerte al inicio
            float t = Time.unscaledTime * shakeFrequency;
            roll += (Mathf.PerlinNoise(srSeed, t + 53.1f) - 0.5f) * 2f * (1.2f * amp);
        }

        // aplicar rot (sumo sólo roll al pre-rot)
        transform.localRotation = _preRot * Quaternion.AngleAxis(roll, Vector3.forward);

        // offset pos opcional (desactivado por defecto)
        if (allowPositionOffset)
        {
            Vector3 off = _kickLocalOffset * k;

            // clamp de seguridad
            off.y = Mathf.Clamp(off.y, -maxOffsetY, maxOffsetY);
            off.z = Mathf.Clamp(off.z, -maxOffsetZ, maxOffsetZ);

            // Mini shake pos sólo si corresponde
            if (doShakeNow)
            {
                float amp = shakeAmplitude + shakeFromKickScale * k;
                float t = Time.unscaledTime * shakeFrequency;
                float px = (Mathf.PerlinNoise(sxSeed, t) - 0.5f) * 2f * 0.01f * amp;
                float py = (Mathf.PerlinNoise(sySeed, t + 21.7f) - 0.5f) * 2f * 0.01f * amp;
                float pz = (Mathf.PerlinNoise(szSeed, t + 87.3f) - 0.5f) * 2f * 0.01f * amp;
                off += new Vector3(px, py, pz);
            }

            transform.localPosition = _prePos + off;
        }
        else
        {
            // no mover posición
            transform.localPosition = _prePos;
        }

        // FOV punch
        if (targetCamera) targetCamera.fieldOfView = baseFOV + fov;

        // Fin del kick: asegurar FOV base
        if (!_active && targetCamera) targetCamera.fieldOfView = baseFOV;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}