using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementAdvanced_ : MonoBehaviour
{
    [Header("Refs")]
    public Transform head; // "Head" (padre de la Main Camera)
    [Tooltip("Opcional: referencia al WallRun para bloquear el doble salto mientras está activo.")]
    public WallRun wallRunRef;

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl; // tap = slide, hold = crouch

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip doubleJumpClip;

    [Header("Speeds")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 3.5f;

    [Header("Layers")]
    public LayerMask groundMask = ~0; // Debe incluir Slope
    public LayerMask slopeMask;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Double Jump")]
    public bool enableDoubleJump = true;
    public int  extraJumps = 1;
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
    public bool  slideJumpOnlyOnSlope = true;
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

    // Hook no-cap aéreo (lo puede usar Grapple)
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

    // Slide jump control
    bool   slideJumpQueued;
    float  slideJumpQueuedUntil;
    float  airControlBlockTimer;
    float  slideExitLockTimer;

    // Slide forward
    Vector3 slideForwardCurrent = Vector3.forward;
    Vector3 slideStartDirPlanar = Vector3.forward;

    enum SlideStartMode { Manual, LandingHeld, AutoSlope }
    SlideStartMode currentStartMode = SlideStartMode.Manual;

    float steerBoostTimer = 0f;
    bool slideOnSlopeAtStart = false;

    // Doble salto
    int  jumpsLeft;

    // Cache de forward planar robusto
    Vector3 lastGoodPlanarForward = Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        col.height = standHeight;
        col.center = new Vector3(0f, standHeight * 0.5f, 0f);

        if (head)
            head.localPosition = new Vector3(head.localPosition.x, headStandY, head.localPosition.z);

        if (slideNoise) slideNoise.sliding = false;

        jumpsLeft = extraJumps;

        if (!wallRunRef) wallRunRef = GetComponent<WallRun>(); // si existe en el mismo GO
    }

    void Update()
    {
        if (rb && rb.isKinematic) return;

        Vector3 planarForward = GetCamPlanarForwardSafe();
        Vector3 planarRight   = head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        inputXRaw = x;
        inputDir = (planarRight * x + planarForward * z).normalized;

        // Ground info
        isGrounded = GroundInfo(out RaycastHit hit);
        if (isGrounded)
        {
            lastGroundNormal  = hit.normal;
            lastGroundIsSlope = ((slopeMask.value & (1 << hit.collider.gameObject.layer)) != 0);
        }
        else lastGroundIsSlope = false;

        bool crouchHeld    = Input.GetKey(crouchKey);
        bool crouchPressed = Input.GetKeyDown(crouchKey);
        bool jumpPressed   = Input.GetKeyDown(jumpKey);
        bool sprintHeld    = Input.GetKey(sprintKey);

        // START SLIDE
        if (!isSliding && crouchPressed && isGrounded && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.Manual);

        if (startOnLandingWhileHeld && !wasGrounded && isGrounded && !isSliding && crouchHeld && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.LandingHeld);

        if (autoSlideOnSlope && !isSliding && isGrounded && lastGroundIsSlope)
            StartSlideNow(planarForward, SlideStartMode.AutoSlope);

        // Crouch normal
        if (!isSliding) { if (crouchHeld) StartCrouch(); else StopCrouch(); }

        // JUMP
        if (jumpPressed)
        {
            if (isSliding)
            {
                QueueSlideJump();
            }
            else if (isGrounded)
            {
                Vector3 v = rb.velocity;
                float vy = (v.y < 0f) ? 0f : v.y;
                rb.velocity = new Vector3(v.x, vy, v.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

                jumpsLeft = extraJumps;
            }
            else if (enableDoubleJump && jumpsLeft > 0 && !(wallRunRef && wallRunRef.IsRunning)) // <-- bloquea si wallrun activo
            {
                DoDoubleJumpWithAngle();  // <-- ángulo/carry corregido
                jumpsLeft--;
            }
        }

        // Vel objetivo (NO slide)
        if (!isSliding && isGrounded)
            targetSpeed = Input.GetKey(crouchKey) ? crouchSpeed : (sprintHeld ? sprintSpeed : walkSpeed);

        if (isSliding)
            timeAirWhileSliding = isGrounded ? 0f : timeAirWhileSliding + Time.deltaTime;

        // Head height
        if (head)
        {
            float targetY = (isCrouching || isSliding) ? headCrouchY : headStandY;
            Vector3 hp = head.localPosition; hp.y = Mathf.Lerp(hp.y, targetY, 12f * Time.deltaTime);
            head.localPosition = hp;
        }

        // reset saltos extra
        if (!wasGrounded && isGrounded)
            jumpsLeft = extraJumps;

        wasGrounded = isGrounded;

        // decaimiento no-cap
        if (suppressAirCapUntil > 0f && Time.time >= suppressAirCapUntil) suppressAirCapUntil = 0f;
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;

        if (slideExitLockTimer > 0f) slideExitLockTimer   -= Time.fixedDeltaTime;
        if (airControlBlockTimer > 0f) airControlBlockTimer -= Time.fixedDeltaTime;
        if (steerBoostTimer > 0f) steerBoostTimer -= Time.fixedDeltaTime;

        if (slideNoise)
        {
            Vector3 hv0 = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            slideNoise.speed = hv0.magnitude;
        }

        // Resolver slide-jump (buffer + coyote)
        if (slideJumpQueued)
        {
            bool bufferAlive      = Time.time <= slideJumpQueuedUntil;
            bool canBecauseGround = isGrounded;
            bool canBecauseCoyote = timeAirWhileSliding <= slideJumpCoyote;
            bool slopeOk          = !slideJumpOnlyOnSlope || slideOnSlopeAtStart;

            if (bufferAlive && (canBecauseGround || canBecauseCoyote) && slopeOk)
            {
                slideJumpQueued = false;

                if (isSliding)
                {
                    isSliding = false;
                    StopCrouch();
                    if (slideNoise) slideNoise.sliding = false;
                }

                Vector3 fwd = GetCamPlanarForwardSafe();
                Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                float baseForward = Mathf.Max(0f, Vector3.Dot(planarVel, fwd)); // solo componente hacia adelante

                float desiredHoriz= Mathf.Clamp(baseForward + slideJumpHorizBonus, slideJumpHorizFloor, slideJumpHorizMax);
                float angRad      = Mathf.Clamp(slideJumpAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
                float desiredVert = Mathf.Min(desiredHoriz * Mathf.Tan(angRad), slideJumpVertMax);

                // seteo directa la comp. horiz en dirCam y elevo vertical (sin matar ascenso mayor)
                float vyKeep = Mathf.Max(0f, rb.velocity.y);
                rb.velocity = fwd * desiredHoriz + Vector3.up * vyKeep;
                rb.AddForce(Vector3.up * desiredVert, ForceMode.VelocityChange);

                airControlBlockTimer = afterSlideJumpNoControlTime;
                suppressAirCapUntil = Mathf.Max(suppressAirCapUntil, Time.time + djNoCapTime);
                return;
            }
            else if (!bufferAlive) slideJumpQueued = false;
        }

        // SLIDE
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
                float mag  = Mathf.Pow(Mathf.Abs(inputXRaw), slideSteerYawExponent);
                float yaw  = slideSteerYawDegPerSec * mag * sign * Time.fixedDeltaTime;
                fwdTmp = Quaternion.AngleAxis(yaw, n) * fwdTmp;
            }
            fwdTmp = Vector3.ProjectOnPlane(fwdTmp, n).normalized;

            slideForwardCurrent = (alignToFallLine && downSlope != Vector3.zero)
                ? Vector3.Slerp(fwdTmp, downSlope, fallLineAlignStrength * Time.fixedDeltaTime).normalized
                : fwdTmp;

            Vector3 forwardOnSlope = slideForwardCurrent;
            Vector3 rightOnSlope   = (downSlope != Vector3.zero)
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
            float magInput  = Mathf.Pow(Mathf.Abs(raw), slideStrafeExponent);
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
            float effLatMax   = slideMaxLateralSpeed * ((steerBoostTimer > 0f) ? steerBoostMaxLatMult : 1f);
            float targetLat   = Mathf.Clamp(inputLateral * slideMaxLateralSpeed, -effLatMax, effLatMax);

            vFwd = Mathf.MoveTowards(vFwd, targetFwd, slideAccelToConst * Time.fixedDeltaTime);
            vLat = Mathf.MoveTowards(vLat, targetLat,  effLatAccel       * Time.fixedDeltaTime);

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

        // Aire
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

        // Suelo, mov. base (NO slide)
        Vector3 desired = inputDir * targetSpeed;
        Vector3 vel = rb.velocity;
        float accel = 20f;
        vel.x = Mathf.Lerp(vel.x, desired.x, accel * Time.fixedDeltaTime);
        vel.z = Mathf.Lerp(vel.z, desired.z, accel * Time.fixedDeltaTime);
        rb.velocity = vel;
    }

    // ==== Helpers ====

    void DoDoubleJumpWithAngle()
    {
        // 1) Dirección SIEMPRE = forward planar de la cámara (con fallback robusto).
        Vector3 dirCam = GetCamPlanarForwardSafe();

        // 2) Carry actual y PROYECCIÓN hacia adelante (solo componente útil).
        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float carryForward = Mathf.Max(0f, Vector3.Dot(hv, dirCam));

        // 3) Objetivo horizontal: mínimo + bonus si hace falta.
        float horiz = Mathf.Max(carryForward, djMinHorizSpeed);
        if (carryForward < djMinHorizSpeed * 0.75f) horiz += djForwardBonus;

        // 4) Objetivo vertical según ángulo, con techo.
        float angRad = Mathf.Clamp(djAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
        float vertByAngle = Mathf.Min(horiz * Mathf.Tan(angRad), djVertMax);

        // Asegura al menos la fuerza base del doble salto si el ángulo da muy poco.
        float vert = Mathf.Max(vertByAngle, doubleJumpForce);

        // Mantener ascenso si ya ibas subiendo (no lo reduzco).
        float vyKeep = Mathf.Max(0f, rb.velocity.y);

        // 5) Aplicar: set horizontal exacto en dirCam + conservar ascenso + sumar vertical del ángulo.
        rb.velocity = dirCam * horiz + Vector3.up * vyKeep;
        rb.AddForce(Vector3.up * vert, ForceMode.VelocityChange);

        // 6) Ventana sin cap para no cortar el impulso recién generado.
        suppressAirCapUntil = Mathf.Max(suppressAirCapUntil, Time.time + djNoCapTime);

        if (audioSource != null && doubleJumpClip != null)
        {
            audioSource.PlayOneShot(doubleJumpClip);
        }
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
            // fallback: si la cámara apunta demasiado abajo, usa el último forward bueno o el vel planar
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

    void StartCrouch(){ isCrouching = true; col.height = crouchHeight; col.center = new Vector3(0f, crouchHeight * 0.5f, 0f); }
    void StopCrouch(){ isCrouching = false; col.height = standHeight; col.center = new Vector3(0f, standHeight * 0.5f, 0f); }
}