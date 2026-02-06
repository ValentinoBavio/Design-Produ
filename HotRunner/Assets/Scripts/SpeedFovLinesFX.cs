using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10000)]
public class SpeedFovLinesFX : MonoBehaviour
{
    [Header("Refs")]
    public Camera targetCamera;
    public PlayerMovementAdvanced_ playerMove;
    public GrappleGun grappleRef;

    [Tooltip("Opcional: si queda null, intenta usar playerMove.wallRunRef.")]
    public WallRun wallRunRef;

    public ParticleSystem speedLines;

    [Header("Follow")]
    public bool parentToCameraOnPlay = true;
    public Vector3 localOffset = new Vector3(0f, 0f, 0.15f);
    public bool keepOriginalLocalRotation = true;
    public bool keepOriginalLocalScale = true;

    [Header("FOV")]
    [Tooltip("FOV boost para Slide/Grapple.")]
    public float boostFov = 150f;

    [Header("WallRun FOV")]
    public bool enableOnWallRun = true;
    public bool useWallRunFovOverride = true;
    public float wallRunBoostFov = 135f;

    public enum FovPriority
    {
        SlideGrappleWins,
        WallRunWins
    }
    [Tooltip("Si coinciden condiciones (ej wallrun + grapple), elegí quién define el FOV.")]
    public FovPriority fovPriority = FovPriority.SlideGrappleWins;

    [Tooltip("Suavizado del cambio de FOV (seg).")]
    public float fovSmoothTime = 0.12f;

    [Tooltip("Suavizado de la intensidad ON/OFF (seg).")]
    public float intensitySmoothTime = 0.10f;

    [Header("Cuándo se activa")]
    public bool enableOnSlide = true;
    public bool enableOnGrapplePull = true;

    [Header("Histeresis de velocidad (evita ON/OFF cerca del umbral)")]
    public bool requireMinSpeed = true;
    public float minSpeedOn = 10f;
    public float minSpeedOff = 7f;

    [Header("Anti parpadeo")]
    public float minOnTime = 0.25f;
    public float offGrace = 0.20f;

    [Header("Particles (opcional)")]
    public bool scaleEmissionBySpeed = true;
    public float emissionMinRate = 10f;
    public float emissionMaxRate = 80f;
    public float emissionSpeedMin = 8f;
    public float emissionSpeedMax = 30f;

    [Header("Intro Overlay (opcional)")]
    public bool ignoreDuringIntro = true;

    Rigidbody _rb;

    bool _fxOn;
    float _offGraceUntil;
    float _mustStayOnUntil;

    float _intensity;
    float _intVel;

    float _renderFov;
    float _fovVel;

    float _baselineFov;

    // Para restaurar después de render
    float _fovBeforeRender;
    bool _overrideActive;

    // pack original
    Quaternion _origLocalRot;
    Vector3 _origLocalScale;
    bool _origSaved;

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void Awake()
    {
        if (!playerMove) playerMove = GetComponentInParent<PlayerMovementAdvanced_>();
        if (!grappleRef) grappleRef = GetComponentInParent<GrappleGun>();

        if (!wallRunRef && playerMove) wallRunRef = playerMove.wallRunRef;
        if (!wallRunRef) wallRunRef = GetComponentInParent<WallRun>();

        if (!targetCamera)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (!cam && Camera.main) cam = Camera.main;
            targetCamera = cam;
        }

        if (playerMove) _rb = playerMove.GetComponent<Rigidbody>();
        if (!_rb) _rb = GetComponentInParent<Rigidbody>();

        if (speedLines)
        {
            SaveOriginalLocal();
            if (speedLines.isPlaying)
                speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _intensity = 0f;
        _renderFov = targetCamera ? targetCamera.fieldOfView : 90f;
    }

    void SaveOriginalLocal()
    {
        if (_origSaved || !speedLines) return;
        _origLocalRot = speedLines.transform.localRotation;
        _origLocalScale = speedLines.transform.localScale;
        _origSaved = true;
    }

    void Update()
    {
        if (!targetCamera) return;

        if (ignoreDuringIntro && Level1IntroOverlay.IsIntroActive)
        {
            ForceOff();
            return;
        }

        float speed = (_rb) ? _rb.velocity.magnitude : 0f;

        bool sliding = enableOnSlide && playerMove && playerMove.slideNoise && playerMove.slideNoise.sliding;

        bool grapplingPull = false;
        if (enableOnGrapplePull)
        {
            bool attached = false;
            if (playerMove) attached = playerMove.IsGrapplingAttached;
            if (!attached && grappleRef) attached = grappleRef.IsAttached;

            bool holding = grappleRef ? Input.GetKey(grappleRef.grappleKey) : true;
            grapplingPull = attached && holding;
        }

        bool wallRunning = enableOnWallRun && wallRunRef && wallRunRef.IsRunning;

        bool want = sliding || grapplingPull || wallRunning;

        // Histeresis por velocidad
        if (requireMinSpeed)
        {
            if (!_fxOn)
            {
                if (speed < minSpeedOn) want = false;
            }
            else
            {
                if (speed < minSpeedOff) want = false;
            }
        }

        // Anti-parpadeo (grace + mínimo encendido)
        if (want)
        {
            _offGraceUntil = Time.time + offGrace;

            if (!_fxOn)
            {
                _fxOn = true;
                _mustStayOnUntil = Time.time + minOnTime;
                PlayLines(true);
            }
        }
        else
        {
            bool canOff = Time.time >= _offGraceUntil && Time.time >= _mustStayOnUntil;
            if (_fxOn && canOff)
            {
                _fxOn = false;
                PlayLines(false);
            }
        }

        // Intensidad suave (0..1)
        float targetIntensity = _fxOn ? 1f : 0f;
        _intensity = Mathf.SmoothDamp(_intensity, targetIntensity, ref _intVel, Mathf.Max(0.01f, intensitySmoothTime));

        // Baseline = lo que “otro sistema” ponga (Cinemachine, etc.)
        _baselineFov = targetCamera.fieldOfView;

        // ✅ Elegir FOV según prioridad
        float chosenBoost = boostFov;

        if (useWallRunFovOverride && wallRunning)
        {
            bool slideOrGrapple = sliding || grapplingPull;

            if (!slideOrGrapple)
            {
                chosenBoost = wallRunBoostFov;
            }
            else
            {
                // Coinciden
                chosenBoost = (fovPriority == FovPriority.WallRunWins) ? wallRunBoostFov : boostFov;
            }
        }

        float desired = Mathf.Lerp(_baselineFov, chosenBoost, _intensity);
        _renderFov = Mathf.SmoothDamp(_renderFov, desired, ref _fovVel, Mathf.Max(0.01f, fovSmoothTime));

        // Partículas
        if (_fxOn && speedLines)
        {
            SnapSpeedLinesToCamera();

            if (scaleEmissionBySpeed)
            {
                float t = Mathf.InverseLerp(emissionSpeedMin, emissionSpeedMax, speed);
                float rate = Mathf.Lerp(emissionMinRate, emissionMaxRate, t);
                var em = speedLines.emission;
                em.rateOverTime = rate;
            }
        }
    }

    // ✅ URP/SRP correcto: aplica FOV justo antes de renderizar
    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (!targetCamera || cam != targetCamera) return;

        _fovBeforeRender = cam.fieldOfView;
        cam.fieldOfView = _renderFov;
        _overrideActive = true;
    }

    // ✅ Restaura para que no “rompa” otros sistemas que lean FOV
    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (!targetCamera || cam != targetCamera) return;
        if (!_overrideActive) return;

        cam.fieldOfView = _fovBeforeRender;
        _overrideActive = false;
    }

    void PlayLines(bool on)
    {
        if (!speedLines) return;

        if (on)
        {
            speedLines.gameObject.SetActive(true);
            SnapSpeedLinesToCamera();
            speedLines.Clear(true);
            speedLines.Play(true);
        }
        else
        {
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void SnapSpeedLinesToCamera()
    {
        if (!speedLines || !targetCamera) return;

        SaveOriginalLocal();

        Transform fxT = speedLines.transform;
        Transform camT = targetCamera.transform;

        if (parentToCameraOnPlay && fxT.parent != camT)
            fxT.SetParent(camT, false);

        fxT.localPosition = localOffset;
        fxT.localRotation = keepOriginalLocalRotation ? _origLocalRot : Quaternion.identity;
        fxT.localScale = keepOriginalLocalScale ? _origLocalScale : Vector3.one;

        var main = speedLines.main;
        if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
    }

    void ForceOff()
    {
        _fxOn = false;
        _intensity = 0f;
        _intVel = 0f;
        _fovVel = 0f;

        PlayLines(false);
    }
}
