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
    public float slideImpulse = 10f;
    public float slideFriction = 2f;
    public float slideMinSpeedExit = 2.5f;
    public float slideExtraGravity = 20f;
    public float slideGroundCoyote = 0.15f;

    [Header("Slide Steering")]
    public float slideSteerAccel = 25f;
    public float slideMaxSpeed = 14f;
    public float slopeAssist = 0f;

    [Header("Slide Jump — alcance")]
    [Range(5f, 60f)]
    public float slideJumpAngleDeg = 28f;
    public float slideJumpHorizFloor = 12f;
    public float slideJumpHorizBonus = 6f;
    public float slideJumpHorizMax = 22f;
    public float slideJumpVertMax = 9f;
    public bool slideJumpProjectOnSlope = true;

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

    bool slideJumpQueued;
    Vector3 queuedJumpForward;
    float airControlBlockTimer;

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
        // (mantle/grapple)
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

        // Ground info, guarda tambien si el suelo es Slope
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

        // START SLIDE
        if (
            !isSliding
            && crouchPressed
            && isGrounded
            && (horizontalSpeed > minSpeedToSlide || movingForward)
        )
        {
            StartCrouch();
            isSliding = true;
            timeAirWhileSliding = 0f;

            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            Vector3 dir = (planarVel.sqrMagnitude > 0.01f ? planarVel : inputDir).normalized;
            rb.AddForce(dir * slideImpulse, ForceMode.VelocityChange);
        }

        // Crouch normal
        if (!isSliding)
        {
            if (crouchHeld)
                StartCrouch();
            else
                StopCrouch();
        }

        // JUMP
        if (jumpPressed)
        {
            if (isSliding)
            {
                if (isGrounded && lastGroundIsSlope)
                {
                    // SOLO en Slope se permite el impulso
                    QueueSlideJump(planarForward);
                }
                else if (isGrounded)
                {
                    // En suelo que NO es Slope: salto normal (sin boost)
                    isSliding = false;
                    StopCrouch();
                    rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                    rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
                }
                // Si no está grounded, ignoramos
            }
            else if (isGrounded)
            {
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            }
        }

        // Vel objetivo (SOLO en suelo)
        if (!isSliding && isGrounded)
        {
            if (crouchHeld)
                targetSpeed = crouchSpeed;
            else if (sprintHeld)
                targetSpeed = sprintSpeed;
            else
                targetSpeed = walkSpeed;
        }

        if (isSliding)
        {
            if (!isGrounded)
                timeAirWhileSliding += Time.deltaTime;
            else
                timeAirWhileSliding = 0f;
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
        // nunca escribir rb.velocity si está kinemático (mantle/otros)
        if (rb == null || rb.isKinematic)
            return;

        // aplicar slide-jump
        if (slideJumpQueued)
        {
            slideJumpQueued = false;

            if (isSliding)
            {
                isSliding = false;
                StopCrouch();
            }

            Vector3 n =
                (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal.normalized;
            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            // dir horizontal base
            Vector3 fwd;
            if (planarVel.sqrMagnitude > 0.01f)
                fwd = planarVel.normalized;
            else if (slideJumpProjectOnSlope)
            {
                Vector3 camPlanar = head
                    ? Vector3.ProjectOnPlane(head.forward, Vector3.up)
                    : transform.forward;
                Vector3 camOnSlope = Vector3.ProjectOnPlane(camPlanar, n);
                fwd = camOnSlope.sqrMagnitude > 1e-6f ? camOnSlope.normalized : queuedJumpForward;
            }
            else
                fwd = queuedJumpForward;
            if (fwd == Vector3.zero)
                fwd = transform.forward;

            // velocidad priorizando horizontal
            float baseHoriz = planarVel.magnitude;
            float desiredHoriz = Mathf.Clamp(
                baseHoriz + slideJumpHorizBonus,
                slideJumpHorizFloor,
                slideJumpHorizMax
            );
            float angRad = Mathf.Clamp(slideJumpAngleDeg, 5f, 60f) * Mathf.Deg2Rad;
            float desiredVert = Mathf.Min(desiredHoriz * Mathf.Tan(angRad), slideJumpVertMax);

            rb.velocity = fwd * desiredHoriz + Vector3.up * desiredVert;

            airControlBlockTimer = afterSlideJumpNoControlTime;
            return;
        }

        if (isSliding)
        {
            rb.AddForce(Vector3.down * slideExtraGravity, ForceMode.Acceleration);

            Vector3 n =
                (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal.normalized;
            Vector3 downSlope = Vector3.ProjectOnPlane(Vector3.down, n);
            if (downSlope.sqrMagnitude > 1e-6f)
                downSlope.Normalize();

            Vector3 rightSlope;
            if (downSlope == Vector3.zero)
                rightSlope = head
                    ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized
                    : Vector3.right;
            else
                rightSlope = Vector3.Cross(n, downSlope).normalized;

            if (slopeAssist > 0f && downSlope != Vector3.zero)
                rb.AddForce(downSlope * slopeAssist, ForceMode.Acceleration);

            if (Mathf.Abs(inputXRaw) > 0.01f)
                rb.AddForce(rightSlope * (inputXRaw * slideSteerAccel), ForceMode.Acceleration);

            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            float maxDelta = slideFriction * Time.fixedDeltaTime;
            planarVel = Vector3.MoveTowards(planarVel, Vector3.zero, maxDelta);
            planarVel = Vector3.ClampMagnitude(planarVel, slideMaxSpeed);
            rb.velocity = new Vector3(planarVel.x, rb.velocity.y, planarVel.z);

            float planarSpeed = planarVel.magnitude;
            bool crouchHeld = Input.GetKey(crouchKey);
            if (
                !crouchHeld
                || planarSpeed < slideMinSpeedExit
                || timeAirWhileSliding > slideGroundCoyote
            )
            {
                isSliding = false;
                StopCrouch();
            }
            return;
        }

        // Aire, no destruimos impulso
        if (!isGrounded)
        {
            if (airControlBlockTimer > 0f)
                airControlBlockTimer -= Time.fixedDeltaTime;
            else
            {
                Vector3 planarForward = head
                    ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized
                    : Vector3.forward;
                Vector3 planarRight = head
                    ? Vector3.ProjectOnPlane(head.right, Vector3.up).normalized
                    : Vector3.right;
                Vector3 wish = (
                    planarRight * Input.GetAxisRaw("Horizontal")
                    + planarForward * Input.GetAxisRaw("Vertical")
                ).normalized;
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

        // Suelo, mov. base
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
        isSliding = false;
        StopCrouch();
        slideJumpQueued = true;
        queuedJumpForward = cameraPlanarForward;
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
