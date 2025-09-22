using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerMovementAdvanced_ : MonoBehaviour
{
    [Header("Refs")]
    public Transform head; // "Head" (padre de la Main Camera)

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl; // tap = slide, hold = crouch

    [Header("Speeds")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 3.5f;

    [Header("Layers")]
    public LayerMask groundMask = ~0; // Debe incluir Slope
    public LayerMask slopeMask;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Crouch / Slide (colisión)")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float headStandY = 0.9f;
    public float headCrouchY = 0.6f;

    [Header("Slide Core")]
    public float minSpeedToSlide = 0f;
    public float slideImpulse = 10f;           // impulso inicial
    public float slideMinSpeedExit = 2.5f;
    public float slideExtraGravity = 20f;
    public float slideGroundCoyote = 0.15f;

    [Header("Slide Steering")]
    public float slopeAssist = 0f;

    [Header("Slide Speed Model (constante)")]
    public float slideConstantSpeed = 16f;     // velocidad objetivo del slide
    public float slideAccelToConst = 40f;      // qué tan rápido converge a esa velocidad
    [Range(0f, 1f)]
    public float keepHeading = 0.85f;          // 1 = mantiene rumbo inicial

    [Header("Slide Strafe (control lateral)")]
    [Range(0f, 2f)]
    public float slideStrafeInfluence = 0.8f;  // cuánto desvía A/D durante el slide
    [Range(0.5f, 3f)]
    public float slideStrafeExponent = 1.4f;   // >1 = más precisión al inicio, más mordida al final
    [Range(0f, 1f)]
    public float slideViewTurnAssist = 0.5f;   // ayuda a girar hacia donde mirás durante el slide

    [Header("Slide Jump — alcance")]
    [Range(5f, 60f)]
    public float slideJumpAngleDeg = 28f;
    public float slideJumpHorizFloor = 12f;
    public float slideJumpHorizBonus = 6f;
    public float slideJumpHorizMax = 22f;
    public float slideJumpVertMax = 9f;
    public bool slideJumpProjectOnSlope = true; // no afecta la dirección (solo cálculo auxiliar)

    [Header("Slide Jump — responsiveness")]
    public bool  slideJumpOnlyOnSlope = true;  // SOLO salto largo si el slide empezó en slope
    public float slideJumpBuffer = 0.12f;      // ventana para colar el Space
    public float slideJumpCoyote = 0.12f;      // ventana airborne en la que igual salta
    public float slideJumpExitLock = 0.10f;    // bloquea salida del slide tras pedir salto

    [Header("AIR CONTROL (no frena el impulso)")]
    public float airControlAccel = 10f;
    public float airMaxHorizontalSpeed = 40f;
    public float afterSlideJumpNoControlTime = 0.18f;

    [Header("Debug")]
    public bool debugRays = false;

    Rigidbody rb;
    CapsuleCollider col;

    Vector3 inputDir;
    float inputXRaw;
    bool isGrounded;
    bool isCrouching;
    bool isSliding;
    float targetSpeed;
    float timeAirWhileSliding;
    Vector3 lastGroundNormal = Vector3.up;
    bool lastGroundIsSlope = false;

    // Slide jump control
    bool   slideJumpQueued;
    float  slideJumpQueuedUntil;   // Time.time límite del buffer
    Vector3 queuedJumpForward;     // no se usa para dirección final (solo respaldo)
    float  airControlBlockTimer;
    float  slideExitLockTimer;     // impide terminar el slide justo cuando pediste salto

    // rumbo al iniciar slide
    Vector3 slideStartDirPlanar = Vector3.forward;

    // recordar si el slide comenzó sobre slope
    bool slideOnSlopeAtStart = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        col.height = standHeight;
        col.center = new Vector3(0f, standHeight * 0.5f, 0f);

        if (head != null)
            head.localPosition = new Vector3(
                head.localPosition.x,
                headStandY,
                head.localPosition.z
            );
    }

    void Update()
    {
        if (rb != null && rb.isKinematic)
            return;

        Vector3 planarForward = head
            ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized
            : Vector3.forward;
        Vector3 planarRight = head
            ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized
            : Vector3.right;

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
        }
        else
        {
            lastGroundIsSlope = false;
        }

        bool crouchHeld = Input.GetKey(crouchKey);
        bool crouchPressed = Input.GetKeyDown(crouchKey);
        bool jumpPressed = Input.GetKeyDown(jumpKey);
        bool sprintHeld = Input.GetKey(sprintKey);

        float horizontalSpeed = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;
        bool movingForward = Vector3.Dot(planarForward, inputDir) > 0.25f;

        // START SLIDE — solo si estoy sobre Slope
        if (!isSliding
            && crouchPressed
            && isGrounded
            && lastGroundIsSlope
            && (horizontalSpeed > minSpeedToSlide || movingForward))
        {
            StartCrouch();
            isSliding = true;
            timeAirWhileSliding = 0f;

            slideOnSlopeAtStart = true;

            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            Vector3 dir = (planarVel.sqrMagnitude > 0.01f ? planarVel : inputDir).normalized;
            if (dir == Vector3.zero) dir = planarForward;

            slideStartDirPlanar = dir;

            rb.AddForce(dir * slideImpulse, ForceMode.VelocityChange);

            Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hv.magnitude < slideConstantSpeed)
                rb.velocity = dir * slideConstantSpeed + Vector3.up * rb.velocity.y;
        }

        // Crouch normal (hold)
        if (!isSliding)
        {
            if (crouchHeld) StartCrouch();
            else            StopCrouch();
        }

        // JUMP (buffer + coyote)
        if (jumpPressed)
        {
            if (isSliding)
            {
                QueueSlideJump(planarForward);
            }
            else if (isGrounded)
            {
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        // Vel objetivo (NO slide)
        if (!isSliding && isGrounded)
        {
            if      (crouchHeld) targetSpeed = crouchSpeed;
            else if (sprintHeld) targetSpeed = sprintSpeed;
            else                 targetSpeed = walkSpeed;
        }

        if (isSliding)
        {
            if (!isGrounded) timeAirWhileSliding += Time.deltaTime;
            else             timeAirWhileSliding = 0f;
        }

        // Head height
        if (head != null)
        {
            float targetY = (isCrouching || isSliding) ? headCrouchY : headStandY;
            Vector3 hp = head.localPosition;
            hp.y = Mathf.Lerp(hp.y, targetY, 12f * Time.deltaTime);
            head.localPosition = hp;
        }
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic)
            return;

        if (slideExitLockTimer > 0f) slideExitLockTimer -= Time.fixedDeltaTime;
        if (airControlBlockTimer > 0f) airControlBlockTimer -= Time.fixedDeltaTime;

        // Slide-Jump: buffer + coyote + requisito "empezó en slope" (si está activado)
        if (slideJumpQueued)
        {
            bool bufferAlive = Time.time <= slideJumpQueuedUntil;
            bool canBecauseGround = isGrounded;
            bool canBecauseCoyote = timeAirWhileSliding <= slideJumpCoyote;

            bool slopeOk = true;
            if (slideJumpOnlyOnSlope)
                slopeOk = slideOnSlopeAtStart; // <-- requisito simplificado: empezó en slope

            if (bufferAlive && (canBecauseGround || canBecauseCoyote) && slopeOk)
            {
                slideJumpQueued = false;

                if (isSliding)
                {
                    isSliding = false;
                    StopCrouch();
                }

                // Dirección: SIEMPRE horizontal hacia donde mirás (sin apuntar al piso)
                Vector3 fwd = head
                    ? Vector3.ProjectOnPlane(head.forward, Vector3.up)
                    : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
                fwd.Normalize();

                // Boost horizontal + componente vertical por ángulo configurado
                Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                float baseHoriz = planarVel.magnitude;
                float desiredHoriz = Mathf.Clamp(baseHoriz + slideJumpHorizBonus, slideJumpHorizFloor, slideJumpHorizMax);
                float angRad = Mathf.Clamp(slideJumpAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
                float desiredVert = Mathf.Min(desiredHoriz * Mathf.Tan(angRad), slideJumpVertMax);

                rb.velocity = fwd * desiredHoriz + Vector3.up * desiredVert;

                airControlBlockTimer = afterSlideJumpNoControlTime;
                return;
            }
            else if (!bufferAlive)
            {
                slideJumpQueued = false;
            }
        }

        if (isSliding)
        {
            rb.AddForce(Vector3.down * slideExtraGravity, ForceMode.Acceleration);

            // normal / direcciones sobre superficie
            Vector3 n = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal.normalized;
            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, n);
            if (downSlope.sqrMagnitude > 1e-6f) downSlope.Normalize();

            Vector3 rightSlope;
            if (downSlope == Vector3.zero)
                rightSlope = head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right;
            else
                rightSlope = Vector3.Cross(n, downSlope).normalized;

            if (slopeAssist > 0f && downSlope != Vector3.zero)
                rb.AddForce(downSlope * slopeAssist, ForceMode.Acceleration);

            // cortar slide si piso suelo no-slope (salvo lock tras pedir salto)
            if (isGrounded && !lastGroundIsSlope && slideExitLockTimer <= 0f)
            {
                isSliding = false;
                StopCrouch();
                return;
            }

            // --- VELOCIDAD CONSTANTE + STRAFE ---
            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            Vector3 dirNow = (planarVel.sqrMagnitude > 0.01f) ? planarVel.normalized : slideStartDirPlanar;
            Vector3 baseDir = Vector3.Slerp(dirNow, slideStartDirPlanar, keepHeading).normalized;

            if (slideViewTurnAssist > 0f && head)
            {
                Vector3 camPlanar = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                baseDir = Vector3.Slerp(baseDir, camPlanar, slideViewTurnAssist * Time.fixedDeltaTime * 10f).normalized;
            }

            Vector3 forwardOnSlope = baseDir;
            Vector3 rightOnSlope   = rightSlope;

            float raw = Mathf.Clamp(inputXRaw, -1f, 1f);
            float sign = Mathf.Sign(raw);
            float mag = Mathf.Pow(Mathf.Abs(raw), slideStrafeExponent);
            float lateral = sign * mag * slideStrafeInfluence;

            Vector3 biasedDir = (forwardOnSlope + rightOnSlope * lateral);
            if (biasedDir.sqrMagnitude > 1e-6f) biasedDir.Normalize();
            else biasedDir = forwardOnSlope;

            Vector3 targetPlanar = biasedDir * slideConstantSpeed;
            planarVel = Vector3.MoveTowards(planarVel, targetPlanar, slideAccelToConst * Time.fixedDeltaTime);

            rb.velocity = new Vector3(planarVel.x, rb.velocity.y, planarVel.z);
            // --- /VELOCIDAD CONSTANTE + STRAFE ---

            bool crouchHeld = Input.GetKey(crouchKey);
            float planarSpeed = planarVel.magnitude;
            if (slideExitLockTimer <= 0f)
            {
                if (!crouchHeld || planarSpeed < slideMinSpeedExit || timeAirWhileSliding > slideGroundCoyote)
                {
                    isSliding = false;
                    StopCrouch();
                }
            }
            return;
        }

        // Aire
        if (!isGrounded)
        {
            if (airControlBlockTimer <= 0f)
            {
                Vector3 planarForward = head ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : Vector3.forward;
                Vector3 planarRight = head ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized : Vector3.right;
                Vector3 wish = (planarRight * Input.GetAxisRaw("Horizontal") + planarForward * Input.GetAxisRaw("Vertical")).normalized;
                if (wish.sqrMagnitude > 0.01f)
                    rb.AddForce(wish * airControlAccel, ForceMode.Acceleration);
            }

            Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hv.magnitude > airMaxHorizontalSpeed)
            {
                hv = hv.normalized * airMaxHorizontalSpeed;
                rb.velocity = new Vector3(hv.x, rb.velocity.y, hv.z);
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

    // Helpers
    void QueueSlideJump(Vector3 cameraPlanarForward)
    {
        slideJumpQueued = true;
        slideJumpQueuedUntil = Time.time + slideJumpBuffer;
        slideExitLockTimer = slideJumpExitLock;

        // respaldo (ya no define la dirección final)
        queuedJumpForward = head
            ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    bool GroundInfo(out RaycastHit hit)
    {
        Vector3 origin = col.bounds.center;
        float rayDist = col.bounds.extents.y + 0.06f;
        bool ok = Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            rayDist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
        if (debugRays)
            Debug.DrawRay(origin, Vector3.down * rayDist, ok ? Color.green : Color.red);
        return ok;
    }

    void StartCrouch()
    {
        isCrouching = true;
        col.height = crouchHeight;
        col.center = new Vector3(0f, crouchHeight * 0.5f, 0f);
    }

    void StopCrouch()
    {
        isCrouching = false;
        col.height = standHeight;
        col.center = new Vector3(0f, standHeight * 0.5f, 0f);
    }
}