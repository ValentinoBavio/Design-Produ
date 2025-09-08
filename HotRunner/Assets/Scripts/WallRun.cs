using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallRun : MonoBehaviour
{
    [Header("Referencias")]
    public Transform head;
    public CameraFollow cameraFollow;

    [Header("Layers")]
    public LayerMask wallMask = ~0;
    public LayerMask groundMask = ~0;

    [Header("Detection")]
    public float wallCheckDistance = 0.8f;
    public float maxWallRunTime = 5.0f;

    [Header("Forces")]
    public float wallRunForce = 14f;
    public float wallStickForce = 25f;

    [Header("Speed Cap")]
    public float maxWallPlanarSpeed = 11f;
    public float capHardness = 1f;

    [Header("Vertical Hold")]
    public bool holdHeight = true;
    public float verticalDamping = 40f;

    [Header("Wall Jump")]
    public KeyCode jumpKey = KeyCode.Space;
    public float wallJumpUpForce = 7f;
    public float wallJumpSideForce = 8f;
    public float wallJumpCooldownTime = 0.15f;

    [Header("Exit & Cooldowns")]
    public float detachImpulse = 1.5f;
    public float reattachCooldown = 0.12f;
    public float sameWallCooldown = 0.40f;

    [Range(0.80f, 0.99f)]
    public float sameWallDotThreshold = 0.92f;

    [Header("Camera Tilt")]
    public float camTilt = 12f;
    public float camTiltSpeed = 10f;

    Rigidbody rb;
    CapsuleCollider col;

    bool isWallRunning;
    bool leftWall,
        rightWall;
    Vector3 wallNormal;
    float runTimer;
    float camTiltTarget;
    float wallJumpCooldown;

    float reattachTimer = 0f;
    float sameWallBlockTimer = 0f;
    Vector3 lastWallNormal = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        if (head == null)
            head = transform;
        if (cameraFollow == null && Camera.main)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Update()
    {
        DetectWalls();

        bool forwardHeld = Input.GetAxisRaw("Vertical") > 0.1f;
        bool grounded = IsGrounded();

        if (wallJumpCooldown > 0f)
            wallJumpCooldown -= Time.deltaTime;
        if (reattachTimer > 0f)
            reattachTimer -= Time.deltaTime;
        if (sameWallBlockTimer > 0f)
            sameWallBlockTimer -= Time.deltaTime;

        if (
            !isWallRunning
            && !grounded
            && forwardHeld
            && (leftWall || rightWall)
            && wallJumpCooldown <= 0f
            && reattachTimer <= 0f
            && !IsBlockedBySameWall()
        )
        {
            StartWallRun();
        }

        if (isWallRunning)
        {
            runTimer += Time.deltaTime;

            bool lostWall = !(leftWall || rightWall);
            bool timeOver = runTimer >= maxWallRunTime;

            if (!forwardHeld || lostWall || timeOver)
                StopWallRun(timeOver || lostWall);
            else if (Input.GetKeyDown(jumpKey))
                DoWallJump();
        }

        if (cameraFollow != null)
            cameraFollow.roll = Mathf.Lerp(
                cameraFollow.roll,
                camTiltTarget,
                camTiltSpeed * Time.deltaTime
            );
    }

    void FixedUpdate()
    {
        if (!isWallRunning)
            return;

        Vector3 along = Vector3.Cross(wallNormal, Vector3.up);
        Vector3 planarForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (Vector3.Dot(along, planarForward) < 0f)
            along = -along;

        Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 alongComp = Vector3.Project(planarVel, along);
        float alongSpeed = alongComp.magnitude;

        float throttle = Mathf.Clamp01(
            (maxWallPlanarSpeed - alongSpeed) / Mathf.Max(0.001f, maxWallPlanarSpeed)
        );
        if (throttle > 0f)
            rb.AddForce(along * (wallRunForce * throttle), ForceMode.Acceleration);

        rb.AddForce(-wallNormal * wallStickForce, ForceMode.Acceleration);

        if (capHardness > 0f && alongSpeed > maxWallPlanarSpeed)
        {
            Vector3 perpComp = planarVel - alongComp;
            Vector3 cappedAlong = along.normalized * maxWallPlanarSpeed;
            Vector3 newPlanar = perpComp + cappedAlong;
            rb.velocity = new Vector3(newPlanar.x, rb.velocity.y, newPlanar.z);
        }

        if (holdHeight)
        {
            rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
            float newY = Mathf.MoveTowards(
                rb.velocity.y,
                0f,
                verticalDamping * Time.fixedDeltaTime
            );
            rb.velocity = new Vector3(rb.velocity.x, newY, rb.velocity.z);
        }
    }

    void DetectWalls()
    {
        leftWall = rightWall = false;
        wallNormal = Vector3.zero;

        Vector3 origin = col.bounds.center;
        Vector3 leftDir = -Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        Vector3 rightDir = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;

        if (
            Physics.Raycast(
                origin,
                leftDir,
                out RaycastHit lHit,
                wallCheckDistance,
                wallMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            leftWall = true;
            wallNormal = lHit.normal;
        }

        if (
            Physics.Raycast(
                origin,
                rightDir,
                out RaycastHit rHit,
                wallCheckDistance,
                wallMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            rightWall = true;
            if (
                !leftWall
                || Vector3.Dot(rHit.normal, head.forward) > Vector3.Dot(wallNormal, head.forward)
            )
                wallNormal = rHit.normal;
        }

        Debug.DrawRay(origin, leftDir * wallCheckDistance, leftWall ? Color.green : Color.gray);
        Debug.DrawRay(origin, rightDir * wallCheckDistance, rightWall ? Color.green : Color.gray);
        if (wallNormal != Vector3.zero)
            Debug.DrawRay(origin, wallNormal, Color.cyan);

        camTiltTarget = isWallRunning ? (rightWall ? camTilt : (leftWall ? -camTilt : 0f)) : 0f;
    }

    bool IsGrounded()
    {
        Vector3 origin = col.bounds.center;
        float rayDist = col.bounds.extents.y + 0.06f;
        return Physics.Raycast(
            origin,
            Vector3.down,
            rayDist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    bool IsBlockedBySameWall()
    {
        if (
            sameWallBlockTimer <= 0f
            || lastWallNormal == Vector3.zero
            || wallNormal == Vector3.zero
        )
            return false;
        float dot = Vector3.Dot(lastWallNormal.normalized, wallNormal.normalized);
        return dot >= sameWallDotThreshold;
    }

    void StartWallRun()
    {
        isWallRunning = true;
        runTimer = 0f;
        camTiltTarget = rightWall ? camTilt : (leftWall ? -camTilt : 0f);
    }

    void StopWallRun(bool blockSameWall = false)
    {
        isWallRunning = false;
        camTiltTarget = 0f;

        if (detachImpulse > 0f && wallNormal != Vector3.zero)
            rb.AddForce(wallNormal.normalized * detachImpulse, ForceMode.VelocityChange);

        reattachTimer = Mathf.Max(reattachTimer, reattachCooldown);

        if (blockSameWall && wallNormal != Vector3.zero)
        {
            lastWallNormal = wallNormal;
            sameWallBlockTimer = Mathf.Max(sameWallBlockTimer, sameWallCooldown);
        }
    }

    void DoWallJump()
    {
        Vector3 jumpDir = wallNormal.normalized * wallJumpSideForce + Vector3.up * wallJumpUpForce;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(jumpDir, ForceMode.VelocityChange);

        wallJumpCooldown = wallJumpCooldownTime;
        lastWallNormal = wallNormal;
        sameWallBlockTimer = Mathf.Max(sameWallBlockTimer, sameWallCooldown);
        reattachTimer = Mathf.Max(reattachTimer, reattachCooldown);

        StopWallRun();
    }
}
