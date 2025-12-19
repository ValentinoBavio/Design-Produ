using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerMovementAdvanced_ : MonoBehaviour
{
    [Header("Refs")]
    public Transform head; // "Head" (padre de la Main Camera)
    [Tooltip("Opcional: referencia al WallRun para bloquear el doble salto mientras está activo.")]
    public WallRun wallRunRef;

    // ===================== GRAPPLE (stamina rules) =====================
    [Header("Grapple - Stamina Rules (FIX)")]
    [Tooltip("Opcional: referencia al GrappleGun. Si queda vacío, se auto-busca en Awake().")]
    public GrappleGun grappleRef;
    public bool grappleDrainRequiresAirborne = true;
    public bool grappleDrainOnlyIfNoJumpThisAir = true;

    [Tooltip("Si está ON: mientras el grapple está Attached y estás en el aire, NO deja hacer Jump/DoubleJump.")]
    public bool blockJumpWhileGrapplingInAir = true;

    [Tooltip("Si está ON: al enganchar el grapple, puede vaciar la stamina según las reglas de abajo.")]
    public bool drainStaminaOnGrappleAttach = true;

    [Tooltip("Regla principal: vacía stamina SOLO si enganchás el grapple en el aire y TODAVÍA NO gastaste el primer salto (jumpPhase==0).")]
    public bool drainOnlyIfAirAndNoJumpUsed = true;

    [Tooltip("Si vacía stamina, también snapea la UI a 0 instant (para que se vea al toque).")]
    public bool snapUIOnGrappleDrain = true;

    [Tooltip("Si vacía stamina en grapple, también pone jumpPhase=2 y jumpsLeft=0 (sin saltos hasta recargar por suelo o wallrun).")]
    public bool zeroJumpsOnGrappleDrain = true;

    bool _isGrapplingAttached = false;
    bool _grappleDrainResolved = false; // se resuelve cuando ya evaluamos si vaciar stamina durante grapple

    public bool IsGrapplingAttached => _isGrapplingAttached;
    // =======================================================

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl; // tap = slide, hold = crouch

    // ===================== AUDIO NUEVO =====================
    [Header("Audio - Source")]
    public AudioSource audioSource;

    [Header("Audio - Footsteps (en orden 1..6)")]
    [Tooltip("Poné tus 6 clips en orden. Se reproducen 0->5 y vuelven a 0.")]
    public AudioClip[] footstepClips = new AudioClip[6];
    [Range(0f, 1f)] public float footstepVolume = 0.08f;

    [Tooltip("Velocidad de referencia para el ritmo de pasos (si tu speed real es ~15, dejalo en 15).")]
    public float stepRefSpeed = 15f;
    [Tooltip("Intervalo de paso cuando vas a stepRefSpeed.")]
    public float stepIntervalAtRefSpeed = 0.33f;
    public float stepIntervalMin = 0.16f;
    public float stepIntervalMax = 0.60f;
    public float stepSpeedThreshold = 0.25f;
    public bool resetFootstepSequenceWhenStop = false;

    [Header("Audio - Jump / Double Jump / Landing")]
    public AudioClip jumpClip;
    public AudioClip doubleJumpClip;
    public AudioClip landClip;

    [Range(0f, 1f)] public float jumpVolume = 0.12f;
    [Range(0f, 1f)] public float doubleJumpVolume = 0.12f;

    [Tooltip("Landing: volumen mínimo SIEMPRE (aunque caigas poquito).")]
    [Range(0f, 1f)] public float landMinVolume = 0.08f;
    [Tooltip("Landing: volumen máximo si caés alto/fuerte.")]
    [Range(0f, 1f)] public float landMaxVolume = 0.25f;

    [Tooltip("Altura (en metros) desde la que empezás a subir el volumen.")]
    public float landMinHeight = 0.7f;
    [Tooltip("Altura (en metros) a la que ya estás en volumen máximo.")]
    public float landMaxHeight = 8f;

    [Tooltip("Velocidad vertical descendente (m/s) para empezar a subir intensidad.")]
    public float landMinDownVel = 2f;
    [Tooltip("Velocidad vertical descendente (m/s) para intensidad máxima.")]
    public float landMaxDownVel = 18f;

    [Tooltip("Si querés que caídas altas suenen un poquito más graves.")]
    public bool landAffectsPitch = true;
    public float landPitchMin = 0.85f; // caída fuerte
    public float landPitchMax = 1.05f; // caída suave
    // =======================================================

    [Header("Speeds")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 3.5f;

    [Header("Layers")]
    public LayerMask groundMask = ~0; // Debe incluir Slope
    public LayerMask slopeMask;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Jump QoL (arregla el salto al caer)")]
    [Tooltip("Tiempo donde todavía cuenta como 'ground' al salir del borde.")]
    public float coyoteTime = 0.14f;
    [Tooltip("Si apretás salto un poquito antes de tocar el piso, lo recuerda.")]
    public float jumpBufferTime = 0.10f;

    [Header("Stamina (Jump / Double / WallRun)")]
    public bool usarStamina = true;

    [Tooltip("Trabajamos normalizado: 1 = barra llena.")]
    public float staminaMax = 1f;

    [Tooltip("Jump consume esto (0.5 = mitad).")]
    [Range(0.05f, 1f)] public float staminaCostoJump = 0.5f;

    [Tooltip("Doble salto consume esto (0.5 = segunda mitad).")]
    [Range(0.05f, 1f)] public float staminaCostoDoubleJump = 0.5f;

    [Tooltip("Runtime.")]
    public float stamina = 1f;

    public float Stamina01 => (staminaMax <= 0f) ? 0f : Mathf.Clamp01(stamina / staminaMax);

    [Header("UI - Stamina (opcional)")]
    [Tooltip("Front (Image Filled) baja rápido.")]
    public Image staminaFrontFill;
    [Tooltip("Back (Image Filled) baja más lento con delay.")]
    public Image staminaBackFill;
    [Tooltip("Alternativa simple: una Image Filled cualquiera.")]
    public Image staminaFillSimple;
    [Tooltip("Alternativa: slider en 0..1 (max=1).")]
    public Slider staminaSlider01;

    [Header("UI - Stamina anim")]
    public float staminaFrontLerpSpeed = 22f;
    public float staminaBackDelay = 0.18f;
    public float staminaBackLerpSpeed = 7f;

    float _stFrontVis = 1f;
    float _stBackVis = 1f;
    float _stBackDelayUntil = 0f;
    float _stTarget = 1f;

    // ✅ FIX: rellenar stamina al ENTRAR en wallrun (transición), para que no quede a mitad por el jump.
    [Header("Stamina - WallRun Refill (FIX)")]
    [Tooltip("Si está ON: al empezar wallrun (detecto transición), relleno stamina a full.")]
    public bool refillStaminaOnWallRunStart = true;

    [Tooltip("Si está ON: además snapea la UI a full instant (no espera lerp).")]
    public bool snapUIOnWallRunStart = true;

    bool _wasWallRunningStamina = false;

    // ===================== TEXTO ESTADO STAMINA (TMP) =====================
    [Header("UI - Stamina Status Text (TMP)")]
    [Tooltip("Texto debajo de la barra. Muestra WALLRUN / JUMP / DOUBLE JUMP / nada.")]
    public TextMeshProUGUI staminaStatusText;

    public string staminaLabelWallRun = "WALLRUN";
    public string staminaLabelJump = "JUMP";
    public string staminaLabelDoubleJump = "DOUBLE JUMP";

    [Tooltip("Si no hay actividad, se oculta a los X segundos.")]
    public float staminaStatusHideAfter = 0.9f;

    [Tooltip("En el suelo se limpia el texto.")]
    public bool clearStaminaStatusOnGround = true;

    [Tooltip("Después de escribir JUMP/DOUBLE JUMP, bloquea que WALLRUN lo pise por X segundos.")]
    public float statusLockAfterJump = 0.35f;

    float _staminaStatusTimer = 0f;
    float _statusLockUntil = 0f;
    // =====================================================================

    // ===================== WALLRUN FOLLOW-UP JUMP =====================
    [Header("WallRun - Follow Up Jump (después del wall-jump)")]
    [Tooltip("Si está ON: al hacer wall-jump, tenés una ventana para hacer un salto extra (más alcance)")]
    public bool enableWallJumpFollowUpBoost = true;

    [Tooltip("Tiempo (seg) después del wall-jump donde el siguiente jump se convierte en el follow-up.")]
    public float wallFollowUpWindow = 0.45f;

    [Tooltip("Ángulo del follow-up (más bajo = más alcance horizontal).")]
    [Range(5f, 60f)] public float wallFollowUpAngleDeg = 22f;

    [Tooltip("Carry horizontal mínimo (en dirección de cámara).")]
    public float wallFollowUpMinHorizSpeed = 16f;

    [Tooltip("Bonus adelante si el carry proyectado es bajo.")]
    public float wallFollowUpForwardBonus = 6f;

    [Tooltip("Vertical base del follow-up.")]
    public float wallFollowUpVertForce = 6.5f;

    [Tooltip("Vertical máxima del follow-up.")]
    public float wallFollowUpVertMax = 8.5f;

    [Tooltip("Si WallRun queda IsRunning 1 frame, igual permitimos el double jump en esta ventana.")]
    public float allowDoubleEvenIfWallRunRunningWindow = 0.25f;
    // ================================================================

    [Header("Double Jump")]
    public bool enableDoubleJump = true;
    [Tooltip("Se usa SOLO 0 ó 1 (doble salto). Aunque lo subas, se clampa a 1.")]
    public int extraJumps = 1;

    [Tooltip("Fuerza vertical base del doble salto (se usa si el ángulo pide menos que esto).")]
    public float doubleJumpForce = 7f;
    [Tooltip("Carry horizontal mínimo del doble salto (en dirección de cámara).")]
    public float djMinHorizSpeed = 11f;
    [Tooltip("Bonus adelante si el carry proyectado es bajo.")]
    public float djForwardBonus = 2.5f;
    [Tooltip("Ángulo objetivo del doble salto, en grados (ej. 45 = diagonal).")]
    [Range(5f, 60f)] public float djAngleDeg = 45f;
    [Tooltip("Vertical máxima aplicada por el ángulo (para no lanzar demasiado).")]
    public float djVertMax = 9f;
    [Tooltip("Tiempo sin cap aéreo tras doble/slide jump (para no cortar el carry).")]
    public float djNoCapTime = 0.15f;

    [Header("Crouch / Slide (colisión)")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float headStandY = 0.9f;
    public float headCrouchY = 0.6f;

    [Header("Slide Core")]
    public float minSpeedToSlide = 0f;
    public float slideImpulse = 0f;    // sin impulso
    public float slideMinSpeedExit = 2.5f;
    public float slideExtraGravity = 20f;
    public float slideGroundCoyote = 0.15f;

    [Header("Slide Steering (asistencia)")]
    public float slopeAssist = 0f;

    [Header("Slide Speed Model (constante)")]
    public float slideConstantSpeed = 20.5f;
    public float slideAccelToConst = 50f;
    [Range(0f, 1f)] public float keepHeading = 0.85f;

    [Header("Slide Strafe (control lateral)")]
    [Range(0f, 2f)] public float slideStrafeInfluence = 1.0f;
    [Range(0.5f, 3f)] public float slideStrafeExponent = 1.4f;
    [Range(0f, 1f)] public float slideViewTurnAssist = 0.25f;

    [Header("Slide Lateral Limits")]
    public float slideMaxLateralSpeed = 8f;
    public float slideLateralAccel = 32f;
    [Range(0f, 1f)] public float slideLateralEdgeGuard = 0.6f;
    public float edgeProbeDistance = 0.6f;
    public float edgeProbeDown = 1.0f;

    [Header("Slide Alignment (fall line)")]
    public bool alignToFallLine = true;
    [Range(0f, 20f)] public float fallLineAlignStrength = 12f;
    [Range(0f, 1f)] public float lateralDampOnEnter = 0.6f;

    [Header("Slide Steer (giro del rumbo con A/D)")]
    public float slideSteerYawDegPerSec = 140f;
    [Range(0.5f, 3f)] public float slideSteerYawExponent = 1.4f;

    [Header("QoL inicio de slide")]
    public bool startOnLandingWhileHeld = true;
    public bool autoSlideOnSlope = false;
    public float slideStartMinForwardSpeed = 6f;
    public bool autoStartSnapToConstSpeed = true;
    public bool autoSlideIgnoreCrouchToSustain = true;

    [Header("Arranque más responsivo (boost corto)")]
    public float steerBoostDuration = 0.35f;
    public float steerBoostLateralAccelMult = 1.5f;
    public float steerBoostMaxLatMult = 1.2f;

    [Header("Slide Entry Unify")]
    public bool unifyEntrySpeed = true;
    public bool entryClampExact = true;

    [Header("Slide Jump — alcance")]
    [Range(5f, 60f)] public float slideJumpAngleDeg = 28f;
    public float slideJumpHorizFloor = 12f;
    public float slideJumpHorizBonus = 6f;
    public float slideJumpHorizMax = 22f;
    public float slideJumpVertMax = 9f;
    public bool slideJumpProjectOnSlope = true;

    [Header("Slide Jump — responsiveness")]
    public bool slideJumpOnlyOnSlope = true;
    public float slideJumpBuffer = 0.12f;
    public float slideJumpCoyote = 0.12f;
    public float slideJumpExitLock = 0.10f;

    [Header("AIR CONTROL (no frena el impulso)")]
    public float airControlAccel = 10f;
    public float airMaxHorizontalSpeed = 40f;
    public float afterSlideJumpNoControlTime = 0.18f;

    [Header("Audio Slide")]
    public ProceduralSlideNoise slideNoise;

    [Header("Debug")]
    public bool debugRays = false;

    [HideInInspector] public bool suppressAirSpeedCap = false;
    public void SetAirCapSuppressed(bool on) { suppressAirSpeedCap = on; }
    float suppressAirCapUntil = 0f;

    Rigidbody rb;
    CapsuleCollider col;

    Vector3 inputDir;
    float inputXRaw;
    bool isGrounded, wasGrounded, isCrouching, isSliding;
    float targetSpeed, timeAirWhileSliding;
    Vector3 lastGroundNormal = Vector3.up;
    bool lastGroundIsSlope = false;

    bool slideJumpQueued;
    float slideJumpQueuedUntil;
    float airControlBlockTimer;
    float slideExitLockTimer;

    Vector3 slideForwardCurrent = Vector3.forward;
    Vector3 slideStartDirPlanar = Vector3.forward;

    enum SlideStartMode { Manual, LandingHeld, AutoSlope }
    SlideStartMode currentStartMode = SlideStartMode.Manual;

    float steerBoostTimer = 0f;
    bool slideOnSlopeAtStart = false;

    int jumpsLeft;
    Vector3 lastGoodPlanarForward = Vector3.forward;

    float coyoteTimer = 0f;
    float jumpBufferTimer = 0f;
    int jumpPhase = 0; // 0 = ninguno, 1 = ya hiciste "primer salto", 2 = doble usado

    int stepIndex = 0;
    float stepTimer = 0f;
    bool wasMovingOnGround = false;

    float fallApexY = 0f;

    // Follow-up state
    float _wallFollowUpUntil = 0f;
    bool _wallFollowUpAvailable = false;
    float _ignoreWallRunBlockUntil = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        col.height = standHeight;
        col.center = new Vector3(0f, standHeight * 0.5f, 0f);

        if (head)
            head.localPosition = new Vector3(head.localPosition.x, headStandY, head.localPosition.z);

        if (slideNoise) slideNoise.sliding = false;

        if (!wallRunRef) wallRunRef = GetComponent<WallRun>();

        // Grapple ref (opcional)
        if (!grappleRef) grappleRef = GetComponent<GrappleGun>();

        jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;

        fallApexY = transform.position.y;

        staminaMax = Mathf.Max(0.01f, staminaMax);
        stamina = staminaMax;

        InitStaminaUI();
        ClearStaminaStatus(true);

        _wasWallRunningStamina = false;
    }

    void Update()
    {
        if (rb && rb.isKinematic) return;

        if (coyoteTimer > 0f) coyoteTimer -= Time.deltaTime;
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;

        Vector3 planarForward = GetCamPlanarForwardSafe();
        Vector3 planarRight = head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        inputXRaw = x;
        inputDir = (planarRight * x + planarForward * z).normalized;

        // Ground info
        isGrounded = GroundInfo(out RaycastHit hit);
        if (isGrounded)
        {
            lastGroundNormal = hit.normal;
            lastGroundIsSlope = ((slopeMask.value & (1 << hit.collider.gameObject.layer)) != 0);
            coyoteTimer = coyoteTime;
        }
        else lastGroundIsSlope = false;

        // Aire/suelo
        if (wasGrounded && !isGrounded) fallApexY = transform.position.y;
        if (!isGrounded) if (transform.position.y > fallApexY) fallApexY = transform.position.y;

        if (!wasGrounded && isGrounded)
        {
            PlayLandingByHeightOrImpact();

            jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;
            jumpPhase = 0;

            RefillStaminaFull(true);

            _wallFollowUpAvailable = false;
            _wallFollowUpUntil = 0f;
            _ignoreWallRunBlockUntil = 0f;
        }

        if (_wallFollowUpAvailable && Time.time > _wallFollowUpUntil)
            _wallFollowUpAvailable = false;

        bool wallRunningNow = (wallRunRef && wallRunRef.IsRunning);

        // ✅ FIX: al ENTRAR a wallrun, rellená stamina + (opcional) snap UI
        if (refillStaminaOnWallRunStart && wallRunningNow && !_wasWallRunningStamina)
        {
            RefillStaminaFull(snapUIOnWallRunStart);
        }
        _wasWallRunningStamina = wallRunningNow;

        // Grapple stamina gate: si engancha en suelo, se resuelve cuando despega.
        TickGrappleStaminaGate();

        if (wallRunningNow && Time.time >= _statusLockUntil)
            SetStaminaStatus(staminaLabelWallRun, true);

        bool crouchHeld = Input.GetKey(crouchKey);
        bool crouchPressed = Input.GetKeyDown(crouchKey);
        bool sprintHeld = Input.GetKey(sprintKey);

        // START SLIDE
        if (!isSliding && crouchPressed && isGrounded && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.Manual);

        if (startOnLandingWhileHeld && !wasGrounded && isGrounded && !isSliding && crouchHeld && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.LandingHeld);

        if (autoSlideOnSlope && !isSliding && isGrounded && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.AutoSlope);

        if (!isSliding) { if (crouchHeld) StartCrouch(); else StopCrouch(); }

        // ===== JUMP (buffer + coyote) =====
        if (Input.GetKeyDown(jumpKey))
            jumpBufferTimer = jumpBufferTime;

        if (jumpBufferTimer > 0f)
        {
            if (isSliding)
            {
                QueueSlideJump();
                jumpBufferTimer = 0f;
            }
            else
            {
                if (TryDoFirstJump()) jumpBufferTimer = 0f;
                else if (TryDoDoubleJump()) jumpBufferTimer = 0f;
            }
        }
        // =================================

        if (!isSliding && isGrounded)
            targetSpeed = Input.GetKey(crouchKey) ? crouchSpeed : (sprintHeld ? sprintSpeed : walkSpeed);

        if (isSliding)
            timeAirWhileSliding = isGrounded ? 0f : timeAirWhileSliding + Time.deltaTime;

        if (head)
        {
            float targetY = (isCrouching || isSliding) ? headCrouchY : headStandY;
            Vector3 hp = head.localPosition; hp.y = Mathf.Lerp(hp.y, targetY, 12f * Time.deltaTime);
            head.localPosition = hp;
        }

        HandleFootsteps();
        TickStaminaUI(Time.deltaTime);
        TickStaminaStatus(Time.deltaTime);

        wasGrounded = isGrounded;

        if (suppressAirCapUntil > 0f && Time.time >= suppressAirCapUntil) suppressAirCapUntil = 0f;
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;

        if (slideExitLockTimer > 0f) slideExitLockTimer -= Time.fixedDeltaTime;
        if (airControlBlockTimer > 0f) airControlBlockTimer -= Time.fixedDeltaTime;
        if (steerBoostTimer > 0f) steerBoostTimer -= Time.fixedDeltaTime;

        if (slideNoise)
        {
            Vector3 hv0 = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            slideNoise.speed = hv0.magnitude;
        }

        if (slideJumpQueued)
        {
            bool bufferAlive = Time.time <= slideJumpQueuedUntil;
            bool canBecauseGround = isGrounded;
            bool canBecauseCoyote = timeAirWhileSliding <= slideJumpCoyote;
            bool slopeOk = !slideJumpOnlyOnSlope || slideOnSlopeAtStart;

            if (bufferAlive && (canBecauseGround || canBecauseCoyote) && slopeOk)
            {
                slideJumpQueued = false;

                if (!CanSpendStamina(staminaCostoJump))
                    return;

                if (isSliding)
                {
                    isSliding = false;
                    StopCrouch();
                    if (slideNoise) slideNoise.sliding = false;
                }

                ConsumeStamina(staminaCostoJump);
                jumpPhase = 1;
                jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;

                Vector3 fwd = GetCamPlanarForwardSafe();
                Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                float baseForward = Mathf.Max(0f, Vector3.Dot(planarVel, fwd));

                float desiredHoriz = Mathf.Clamp(baseForward + slideJumpHorizBonus, slideJumpHorizFloor, slideJumpHorizMax);
                float angRad = Mathf.Clamp(slideJumpAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
                float desiredVert = Mathf.Min(desiredHoriz * Mathf.Tan(angRad), slideJumpVertMax);

                float vyKeep = Mathf.Max(0f, rb.velocity.y);
                rb.velocity = fwd * desiredHoriz + Vector3.up * vyKeep;
                rb.AddForce(Vector3.up * desiredVert, ForceMode.VelocityChange);

                airControlBlockTimer = afterSlideJumpNoControlTime;
                suppressAirCapUntil = Mathf.Max(suppressAirCapUntil, Time.time + djNoCapTime);

                SetStaminaStatus(staminaLabelJump, true);
                _statusLockUntil = Time.time + statusLockAfterJump;

                PlayOneShot2D(jumpClip, jumpVolume);
                return;
            }
            else if (!bufferAlive) slideJumpQueued = false;
        }

        if (isSliding)
        {
            rb.AddForce(Vector3.down * slideExtraGravity, ForceMode.Acceleration);

            Vector3 n = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal.normalized;
            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, n);
            if (downSlope.sqrMagnitude > 1e-6f) downSlope.Normalize();

            Vector3 fwdTmp = slideForwardCurrent;
            if (Mathf.Abs(inputXRaw) > 0.01f)
            {
                float sign = Mathf.Sign(inputXRaw);
                float mag = Mathf.Pow(Mathf.Abs(inputXRaw), slideSteerYawExponent);
                float yaw = slideSteerYawDegPerSec * mag * sign * Time.fixedDeltaTime;
                fwdTmp = Quaternion.AngleAxis(yaw, n) * fwdTmp;
            }
            fwdTmp = Vector3.ProjectOnPlane(fwdTmp, n).normalized;

            slideForwardCurrent = (alignToFallLine && downSlope != Vector3.zero)
                ? Vector3.Slerp(fwdTmp, downSlope, fallLineAlignStrength * Time.fixedDeltaTime).normalized
                : fwdTmp;

            Vector3 forwardOnSlope = slideForwardCurrent;
            Vector3 rightOnSlope = (downSlope != Vector3.zero)
                ? Vector3.Cross(n, slideForwardCurrent).normalized
                : (head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right);

            if (slopeAssist > 0f && downSlope != Vector3.zero)
                rb.AddForce(downSlope * slopeAssist, ForceMode.Acceleration);

            bool crouchHeld = Input.GetKey(crouchKey);
            if (isGrounded && !lastGroundIsSlope && slideExitLockTimer <= 0f)
            {
                isSliding = false;
                StopCrouch();
                if (slideNoise) slideNoise.sliding = false;
                return;
            }

            Vector3 planarVelNow = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            float raw = Mathf.Clamp(inputXRaw, -1f, 1f);
            float signInput = Mathf.Sign(raw);
            float magInput = Mathf.Pow(Mathf.Abs(raw), slideStrafeExponent);
            float inputLateral = signInput * magInput * slideStrafeInfluence;

            if (slideLateralEdgeGuard > 0f)
            {
                Vector3 sideDir = (inputLateral >= 0f ? rightOnSlope : -rightOnSlope);
                Vector3 origin = col.bounds.center; origin.y = col.bounds.min.y + 0.05f;

                Vector3 probeSide = origin + sideDir * edgeProbeDistance;
                bool haySueloCostado = Physics.Raycast(probeSide, Vector3.down, out _, edgeProbeDown, groundMask, QueryTriggerInteraction.Ignore);

                if (debugRays)
                {
                    Debug.DrawLine(origin, probeSide, Color.yellow, Time.fixedDeltaTime);
                    Debug.DrawRay(probeSide, Vector3.down * edgeProbeDown, haySueloCostado ? Color.green : Color.red);
                }

                if (!haySueloCostado) inputLateral *= slideLateralEdgeGuard;
            }

            float vFwd = Vector3.Dot(planarVelNow, forwardOnSlope);
            float vLat = Vector3.Dot(planarVelNow, rightOnSlope);

            float targetFwd = slideConstantSpeed;
            float effLatAccel = slideLateralAccel * ((steerBoostTimer > 0f) ? steerBoostLateralAccelMult : 1f);
            float effLatMax = slideMaxLateralSpeed * ((steerBoostTimer > 0f) ? steerBoostMaxLatMult : 1f);
            float targetLat = Mathf.Clamp(inputLateral * slideMaxLateralSpeed, -effLatMax, effLatMax);

            vFwd = Mathf.MoveTowards(vFwd, targetFwd, slideAccelToConst * Time.fixedDeltaTime);
            vLat = Mathf.MoveTowards(vLat, targetLat, effLatAccel * Time.fixedDeltaTime);

            Vector3 newPlanar = forwardOnSlope * vFwd + rightOnSlope * vLat;
            rb.velocity = new Vector3(newPlanar.x, rb.velocity.y, newPlanar.z);

            float planarSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
            bool needsHold = !(autoSlideIgnoreCrouchToSustain && currentStartMode == SlideStartMode.AutoSlope);
            if (slideExitLockTimer <= 0f)
            {
                if ((needsHold && !crouchHeld) || planarSpeed < slideMinSpeedExit || timeAirWhileSliding > slideGroundCoyote)
                {
                    isSliding = false;
                    StopCrouch();
                    if (slideNoise) slideNoise.sliding = false;
                }
            }
            return;
        }

        if (!isGrounded)
        {
            if (airControlBlockTimer <= 0f)
            {
                Vector3 planarForward = GetCamPlanarForwardSafe();
                Vector3 planarRight = head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right;
                Vector3 wish = (planarRight * Input.GetAxisRaw("Horizontal") + planarForward * Input.GetAxisRaw("Vertical")).normalized;
                if (wish.sqrMagnitude > 0.01f)
                    rb.AddForce(wish * airControlAccel, ForceMode.Acceleration);
            }

            bool suppress = suppressAirSpeedCap || (suppressAirCapUntil > Time.time);
            if (!suppress)
            {
                Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                if (hv.magnitude > airMaxHorizontalSpeed)
                {
                    hv = hv.normalized * airMaxHorizontalSpeed;
                    rb.velocity = new Vector3(hv.x, rb.velocity.y, hv.z);
                }
            }
            return;
        }

        Vector3 desired = inputDir * targetSpeed;
        Vector3 vel = rb.velocity;
        float accel = 20f;
        vel.x = Mathf.Lerp(vel.x, desired.x, accel * Time.fixedDeltaTime);
        vel.z = Mathf.Lerp(vel.z, desired.z, accel * Time.fixedDeltaTime);
        rb.velocity = vel;
    }

    // ===================== GRAPPLE CALLBACKS =====================
    // GrappleGun te llama cuando pasa a Attached / cuando se corta.
    public void OnGrappleAttached()
    {
        _isGrapplingAttached = true;
        _grappleDrainResolved = false;

        if (!usarStamina || !drainStaminaOnGrappleAttach)
            return;

        // Evaluación inmediata con raycast fresco (no depende del cache de isGrounded)
        bool groundedNow = QueryGroundedNow();
        TryResolveGrappleStaminaDrain(groundedNow);
    }

    public void OnGrappleDetached()
    {
        _isGrapplingAttached = false;
        _grappleDrainResolved = false;
    }

    void TickGrappleStaminaGate()
    {
        if (!_isGrapplingAttached) return;
        if (_grappleDrainResolved) return;

        if (!usarStamina || !drainStaminaOnGrappleAttach)
        {
            _grappleDrainResolved = true;
            return;
        }

        // En Update ya tenemos isGrounded actualizado para este frame
        TryResolveGrappleStaminaDrain(isGrounded);
    }

    void TryResolveGrappleStaminaDrain(bool groundedNow)
    {
        if (_grappleDrainResolved) return;

        // Si pedimos “sólo cuando está en el aire”, esperamos hasta que deje de estar grounded.
        if (grappleDrainRequiresAirborne && groundedNow)
            return;

        const float eps = 0.0001f;

        // Por defecto: sólo vaciamos si la barra está llena (o casi).
        bool staminaIsFull = stamina >= (staminaMax - eps);

        bool shouldDrain = staminaIsFull;

        // Si querés respetar la excepción: “si ya saltaste en este aire, no vacíes”.
        if (grappleDrainOnlyIfNoJumpThisAir && jumpPhase != 0)
            shouldDrain = false;

        if (shouldDrain)
        {
            stamina = 0f;

            // Snap visual instantáneo para que se note al enganchar / al despegar del suelo.
            SnapStaminaUIToTarget();
        }

        // IMPORTANTE: una vez que el jugador está en el aire (o si no requerimos aire), se resuelve.
        // Si no drenamos porque ya había salto (stamina half / jumpPhase=1), NO lo volvemos a intentar.
        _grappleDrainResolved = true;
    }

    bool QueryGroundedNow()
    {
        Vector3 origin = col.bounds.center;
        float rayDist = col.bounds.extents.y + 0.06f;
        return Physics.Raycast(origin, Vector3.down, rayDist, groundMask, QueryTriggerInteraction.Ignore);
    }
    // ============================================================

    // ===================== STAMINA + UI (SMOOTH) =====================

    bool CanSpendStamina(float cost)
    {
        if (!usarStamina) return true;
        return stamina >= (Mathf.Abs(cost) - 0.0001f);
    }

    public void RefillStaminaFull(bool snapUI)
    {
        if (!usarStamina) return;
        stamina = staminaMax;
        if (snapUI) SnapStaminaUIToTarget();
    }

    void ConsumeStamina(float amount)
    {
        if (!usarStamina) return;

        float prev = stamina;
        stamina = Mathf.Clamp(stamina - Mathf.Abs(amount), 0f, staminaMax);

        if (stamina < prev)
            NotifyStaminaDecreased();
    }

    /// <summary>Lo llama WallRun al empezar: recarga barra y resetea doble salto.</summary>
    public void OnWallRunStarted()
    {
        RefillStaminaFull(true);
        jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;
        jumpPhase = 0;
    }

    /// <summary>Lo llama WallRun por frame: drena la barra en maxWallRunTime segundos.</summary>
    public void DrainStaminaForWallRun(float dt, float maxWallRunTime)
    {
        if (!usarStamina) return;

        float drainPerSec = staminaMax / Mathf.Max(0.001f, maxWallRunTime);
        float prev = stamina;
        stamina = Mathf.Clamp(stamina - drainPerSec * dt, 0f, staminaMax);

        if (stamina < prev)
            NotifyStaminaDecreased();
    }

    /// <summary>Lo llama WallRun cuando hacés wall-jump.</summary>
    public void OnWallJumpExecuted()
    {
        if (!CanSpendStamina(staminaCostoJump)) return;

        jumpBufferTimer = 0f;

        ConsumeStamina(staminaCostoJump);

        jumpPhase = 1;
        jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;
        coyoteTimer = 0f;

        _wallFollowUpAvailable = enableWallJumpFollowUpBoost;
        _wallFollowUpUntil = Time.time + Mathf.Max(0.01f, wallFollowUpWindow);

        _ignoreWallRunBlockUntil = Time.time + Mathf.Max(0.05f, allowDoubleEvenIfWallRunRunningWindow);

        SetStaminaStatus(staminaLabelJump, true);
        _statusLockUntil = Time.time + statusLockAfterJump;
    }

    void InitStaminaUI()
    {
        _stTarget = Stamina01;
        _stFrontVis = _stTarget;
        _stBackVis = _stTarget;
        _stBackDelayUntil = 0f;
        ApplyStaminaUI();
    }

    void SnapStaminaUIToTarget()
    {
        _stTarget = Stamina01;
        _stFrontVis = _stTarget;
        _stBackVis = _stTarget;
        _stBackDelayUntil = 0f;
        ApplyStaminaUI();
    }

    void NotifyStaminaDecreased()
    {
        _stBackDelayUntil = Time.time + Mathf.Max(0f, staminaBackDelay);
    }

    float SmoothExp(float current, float target, float speed, float dt)
    {
        if (speed <= 0f) return target;
        float t = 1f - Mathf.Exp(-speed * dt);
        return Mathf.Lerp(current, target, t);
    }

    void TickStaminaUI(float dt)
    {
        if (!staminaFrontFill && !staminaBackFill && !staminaFillSimple && !staminaSlider01)
            return;

        float target = Stamina01;

        if (target < _stTarget - 0.0001f)
            NotifyStaminaDecreased();

        _stTarget = target;

        float fSpeed = Mathf.Max(0.01f, staminaFrontLerpSpeed);
        _stFrontVis = SmoothExp(_stFrontVis, target, fSpeed, dt);

        if (_stBackVis < target)
        {
            _stBackVis = target;
        }
        else
        {
            if (Time.time >= _stBackDelayUntil)
            {
                float bSpeed = Mathf.Max(0.01f, staminaBackLerpSpeed);
                _stBackVis = SmoothExp(_stBackVis, target, bSpeed, dt);
            }
        }

        ApplyStaminaUI();
    }

    void ApplyStaminaUI()
    {
        if (staminaFrontFill) staminaFrontFill.fillAmount = _stFrontVis;
        if (staminaBackFill) staminaBackFill.fillAmount = _stBackVis;

        if (staminaFillSimple) staminaFillSimple.fillAmount = Stamina01;

        if (staminaSlider01)
        {
            staminaSlider01.minValue = 0f;
            staminaSlider01.maxValue = 1f;
            staminaSlider01.value = Stamina01;
        }
    }

    // ===================== TEXTO ESTADO STAMINA =====================

    void SetStaminaStatus(string label, bool refreshTimer)
    {
        if (!staminaStatusText) return;

        staminaStatusText.text = label;

        if (refreshTimer)
            _staminaStatusTimer = Mathf.Max(0.05f, staminaStatusHideAfter);
    }

    void ClearStaminaStatus(bool force)
    {
        if (!staminaStatusText) return;

        if (force)
        {
            staminaStatusText.text = "";
            _staminaStatusTimer = 0f;
        }
    }

    void TickStaminaStatus(float dt)
    {
        if (!staminaStatusText) return;

        if (clearStaminaStatusOnGround && isGrounded)
        {
            if (Time.time >= _statusLockUntil)
            {
                staminaStatusText.text = "";
                _staminaStatusTimer = 0f;
            }
            return;
        }

        if (_staminaStatusTimer > 0f)
        {
            _staminaStatusTimer -= dt;
            if (_staminaStatusTimer <= 0f)
                staminaStatusText.text = "";
        }
    }

    // ===================== JUMP LOGIC =====================

    bool TryDoFirstJump()
    {
        // Grapple: si está Attached y estás en el aire, bloqueamos el salto (pero en suelo lo dejamos).
        if (blockJumpWhileGrapplingInAir && _isGrapplingAttached && !isGrounded)
            return false;

        if (jumpPhase != 0) return false;

        if (!(isGrounded || coyoteTimer > 0f)) return false;
        if (!CanSpendStamina(staminaCostoJump)) return false;

        ConsumeStamina(staminaCostoJump);

        Vector3 v = rb.velocity;
        float vy = (v.y < 0f) ? 0f : v.y;
        rb.velocity = new Vector3(v.x, vy, v.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        PlayOneShot2D(jumpClip, jumpVolume);

        jumpPhase = 1;
        coyoteTimer = 0f;

        jumpsLeft = enableDoubleJump ? Mathf.Clamp(extraJumps, 0, 1) : 0;

        SetStaminaStatus(staminaLabelJump, true);
        _statusLockUntil = Time.time + statusLockAfterJump;

        return true;
    }

    bool TryDoDoubleJump()
    {
        // Grapple: si está Attached y estás en el aire, bloqueamos el doble (cuando sueltes E, vuelve a poder).
        if (blockJumpWhileGrapplingInAir && _isGrapplingAttached && !isGrounded)
            return false;

        if (!enableDoubleJump) return false;
        if (jumpsLeft <= 0) return false;
        if (jumpPhase != 1) return false;

        if (wallRunRef && wallRunRef.IsRunning)
        {
            bool allowBecauseJustWallJump = Time.time <= _ignoreWallRunBlockUntil;
            if (!allowBecauseJustWallJump) return false;
        }

        if (!CanSpendStamina(staminaCostoDoubleJump)) return false;

        ConsumeStamina(staminaCostoDoubleJump);

        bool canFollowUp = enableWallJumpFollowUpBoost && _wallFollowUpAvailable && (Time.time <= _wallFollowUpUntil);
        if (canFollowUp)
        {
            DoWallRunFollowUpJump();
            _wallFollowUpAvailable = false;
        }
        else
        {
            DoDoubleJumpWithAngle();
            _wallFollowUpAvailable = false;
        }

        jumpsLeft = 0;
        jumpPhase = 2;

        SetStaminaStatus(staminaLabelDoubleJump, true);
        _statusLockUntil = Time.time + statusLockAfterJump;

        return true;
    }

    void DoWallRunFollowUpJump()
    {
        Vector3 dirCam = GetCamPlanarForwardSafe();

        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float carryForward = Mathf.Max(0f, Vector3.Dot(hv, dirCam));

        float horiz = Mathf.Max(carryForward, wallFollowUpMinHorizSpeed);
        if (carryForward < wallFollowUpMinHorizSpeed * 0.75f)
            horiz += wallFollowUpForwardBonus;

        float angRad = Mathf.Clamp(wallFollowUpAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
        float vertByAngle = Mathf.Min(horiz * Mathf.Tan(angRad), wallFollowUpVertMax);
        float vert = Mathf.Max(vertByAngle, wallFollowUpVertForce);

        float vyKeep = Mathf.Max(0f, rb.velocity.y);

        rb.velocity = dirCam * horiz + Vector3.up * vyKeep;
        rb.AddForce(Vector3.up * vert, ForceMode.VelocityChange);

        suppressAirCapUntil = Mathf.Max(suppressAirCapUntil, Time.time + djNoCapTime);

        PlayOneShot2D(doubleJumpClip, doubleJumpVolume);
    }

    // ===================== AUDIO HELPERS =====================

    void HandleFootsteps()
    {
        if (!audioSource || footstepClips == null || footstepClips.Length == 0)
            return;

        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float speed = hv.magnitude;

        bool canStep = isGrounded && !isSliding && speed >= stepSpeedThreshold;

        if (!canStep)
        {
            stepTimer = 0f;
            if (resetFootstepSequenceWhenStop) stepIndex = 0;
            wasMovingOnGround = false;
            return;
        }

        if (!wasMovingOnGround)
        {
            stepTimer = stepIntervalAtRefSpeed;
            wasMovingOnGround = true;
        }

        float interval = GetStepIntervalForSpeed(speed);
        stepTimer += Time.deltaTime;

        if (stepTimer >= interval)
        {
            stepTimer = 0f;
            PlayFootstepOrdered();
        }
    }

    float GetStepIntervalForSpeed(float speed)
    {
        float refSpd = Mathf.Max(0.01f, stepRefSpeed);
        float s = Mathf.Max(0.01f, speed);

        float interval = stepIntervalAtRefSpeed * (refSpd / s);
        return Mathf.Clamp(interval, stepIntervalMin, stepIntervalMax);
    }

    void PlayFootstepOrdered()
    {
        int n = footstepClips.Length;
        if (n == 0) return;

        for (int tries = 0; tries < n; tries++)
        {
            int idx = stepIndex % n;
            stepIndex = (stepIndex + 1) % n;

            AudioClip c = footstepClips[idx];
            if (c)
            {
                PlayOneShot2D(c, footstepVolume);
                return;
            }
        }
    }

    void PlayLandingByHeightOrImpact()
    {
        if (!audioSource || !landClip) return;

        float landingY = transform.position.y;
        float fallHeight = Mathf.Max(0f, fallApexY - landingY);

        float downVel = Mathf.Max(0f, -rb.velocity.y);

        float h01 = Mathf.InverseLerp(landMinHeight, landMaxHeight, fallHeight);
        float v01 = Mathf.InverseLerp(landMinDownVel, landMaxDownVel, downVel);

        float intensity = Mathf.Clamp01(Mathf.Max(h01, v01));
        float vol = Mathf.Lerp(landMinVolume, landMaxVolume, intensity);

        if (landAffectsPitch)
        {
            float oldPitch = audioSource.pitch;
            audioSource.pitch = Mathf.Lerp(landPitchMax, landPitchMin, intensity);
            audioSource.PlayOneShot(landClip, vol);
            audioSource.pitch = oldPitch;
        }
        else
        {
            audioSource.PlayOneShot(landClip, vol);
        }
    }

    void PlayOneShot2D(AudioClip clip, float vol)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip, vol);
    }

    // ===================== Helpers existentes =====================

    void DoDoubleJumpWithAngle()
    {
        Vector3 dirCam = GetCamPlanarForwardSafe();

        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float carryForward = Mathf.Max(0f, Vector3.Dot(hv, dirCam));

        float horiz = Mathf.Max(carryForward, djMinHorizSpeed);
        if (carryForward < djMinHorizSpeed * 0.75f) horiz += djForwardBonus;

        float angRad = Mathf.Clamp(djAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
        float vertByAngle = Mathf.Min(horiz * Mathf.Tan(angRad), djVertMax);
        float vert = Mathf.Max(vertByAngle, doubleJumpForce);

        float vyKeep = Mathf.Max(0f, rb.velocity.y);

        rb.velocity = dirCam * horiz + Vector3.up * vyKeep;
        rb.AddForce(Vector3.up * vert, ForceMode.VelocityChange);

        suppressAirCapUntil = Mathf.Max(suppressAirCapUntil, Time.time + djNoCapTime);

        PlayOneShot2D(doubleJumpClip, doubleJumpVolume);
    }

    void StartSlideNow(Vector3 planarForward, SlideStartMode mode)
    {
        StartCrouch();
        isSliding = true;
        timeAirWhileSliding = 0f;
        slideOnSlopeAtStart = true;
        currentStartMode = mode;

        Vector3 n = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal.normalized;
        Vector3 fallLine = Vector3.ProjectOnPlane(Vector3.down, n);
        if (fallLine.sqrMagnitude < 1e-6f) fallLine = planarForward;
        fallLine.Normalize();

        slideForwardCurrent = fallLine;
        slideStartDirPlanar = fallLine;

        Vector3 pv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 right = Vector3.Cross(n, fallLine).normalized;
        float vFwd = Vector3.Dot(pv, fallLine);
        float vLat = Vector3.Dot(pv, right);

        vLat *= (1f - Mathf.Clamp01(lateralDampOnEnter));

        if (unifyEntrySpeed)
        { if (entryClampExact) vFwd = slideConstantSpeed; else vFwd = Mathf.Min(vFwd, slideConstantSpeed); }
        else
        {
            vFwd = Mathf.Max(vFwd, slideStartMinForwardSpeed);
            if (autoStartSnapToConstSpeed && (mode == SlideStartMode.AutoSlope || mode == SlideStartMode.LandingHeld))
                vFwd = Mathf.Max(vFwd, slideConstantSpeed);
        }

        Vector3 newPlanar = fallLine * vFwd + right * vLat;
        rb.velocity = new Vector3(newPlanar.x, rb.velocity.y, newPlanar.z);

        steerBoostTimer = steerBoostDuration;

        if (slideNoise) slideNoise.sliding = true;
    }

    void QueueSlideJump()
    {
        slideJumpQueued = true;
        slideJumpQueuedUntil = Time.time + slideJumpBuffer;
        slideExitLockTimer = slideJumpExitLock;
    }

    Vector3 GetCamPlanarForwardSafe()
    {
        Vector3 fwd = head ? Vector3.ProjectOnPlane(head.forward, Vector3.up)
                           : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            fwd = (hv.sqrMagnitude > 0.01f) ? hv.normalized : lastGoodPlanarForward;
        }
        else
        {
            lastGoodPlanarForward = fwd.normalized;
        }
        return fwd.normalized;
    }

    bool GroundInfo(out RaycastHit hit)
    {
        Vector3 origin = col.bounds.center;
        float rayDist = col.bounds.extents.y + 0.06f;
        bool ok = Physics.Raycast(origin, Vector3.down, out hit, rayDist, groundMask, QueryTriggerInteraction.Ignore);
        if (debugRays) Debug.DrawRay(origin, Vector3.down * rayDist, ok ? Color.green : Color.red);
        return ok;
    }

    void StartCrouch() { isCrouching = true; col.height = crouchHeight; col.center = new Vector3(0f, crouchHeight * 0.5f, 0f); }
    void StopCrouch() { isCrouching = false; col.height = standHeight; col.center = new Vector3(0f, standHeight * 0.5f, 0f); }
}
