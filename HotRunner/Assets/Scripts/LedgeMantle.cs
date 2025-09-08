using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LedgeMantle : MonoBehaviour
{
    [Header("Referencias")]
    public Rigidbody rb;
    public CapsuleCollider capsule;
    public Transform cameraPivot;

    [Header("Capas y detección (pared)")]
    [Tooltip("Capas a las que el SphereCast puede pegar para detectar la pared.")]
    public LayerMask climbableLayers = ~0;

    [Tooltip("Distancia base del SphereCast hacia adelante.")]
    public float forwardCheckDist = 0.8f;

    [Tooltip("Radio del SphereCast hacia adelante.")]
    public float forwardCheckRadius = 0.25f;

    [Tooltip("Altura mínima del borde (desde los pies).")]
    public float minLedgeHeight = 0.6f;

    [Tooltip("Altura máxima del borde (desde los pies).")]
    public float maxLedgeHeight = 1.6f;

    [Header("Robustez de casteo")]
    [Tooltip("Retroceso base del origen del SphereCast para no nacer dentro del collider.")]
    public float castBackoff = 0.12f;

    [Tooltip("Cantidad de retrocesos extra si no encuentra pared (por fila).")]
    public int backoffSteps = 2;

    [Tooltip("Tamaño de cada retroceso extra.")]
    public float backoffStep = 0.08f;

    [Tooltip("Desplazamiento lateral del origen para reintentos (por lado).")]
    public float lateralProbe = 0.14f;

    [Tooltip("Cantidad de escalones laterales por lado (1 => -1,0,+1).")]
    public int lateralStepsPerSide = 1;

    [Tooltip("Extra que se suma a la distancia del SphereCast para dar más margen.")]
    public float forwardDistBoost = 0.35f;

    [Header("Detección - tapa (superficie superior)")]
    [Tooltip("Cuánto 'mete' el punto por encima del borde antes del ray vertical.")]
    public float overShoot = 0.30f;

    [Tooltip("Altura desde la cual rayamos hacia abajo para encontrar la tapa.")]
    public float topDownCheck = 1.5f;

    [Tooltip("Permitir que el ray vertical hacia la tapa golpee cualquier layer (~0).")]
    public bool topRayHitsAnyLayer = true;

    [Tooltip("Si no usás 'any layer', especificá capas de tapa; vacío cae a climbableLayers.")]
    public LayerMask topSurfaceLayers = 0;

    [Header("Auto-boost para casos difíciles/altos")]
    [Tooltip("Si el primer intento falla, aumentamos overshoot/topDown/distancia y reintentamos.")]
    public bool enableAutoBoostOnFail = true;
    public float boostOvershoot = 0.18f;
    public float boostTopDown = 0.6f;
    public float boostForwardDist = 0.25f;

    [Header("Fallback multi-dirección")]
    [Tooltip("Si falla con la dirección dada, intentar también -forward y ±lateral.")]
    public bool multiDirFallback = true;

    [Tooltip("Probar diagonales (±45°) además de las 4 cardinales.")]
    public bool includeDiagonals = true;

    [Header("Espacio de aterrizaje")]
    public float standBackOffset = 0.16f;
    public float standUpLift = 0.02f;

    [Header("Movimiento del Mantle")]
    public float mantleDuration = 0.25f;
    public AnimationCurve mantleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip(
        "Si es true, intenta alinear al jugador mirando fuera de la pared (sujeto al modo de yaw)."
    )]
    public bool alignToWallNormal = true;
    public float rotationLerp = 12f;

    public enum YawAlignMode
    {
        None,
        Clamp,
        OnlyIfWithinThreshold,
    }

    [Header("Rotación avanzada (Yaw)")]
    [Tooltip("Selecciona el modo de alineación de yaw durante el mantle.")]
    public YawAlignMode yawAlignMode = YawAlignMode.OnlyIfWithinThreshold;

    [Tooltip("Máximo de grados de giro de yaw permitidos (sólo en modo Clamp).")]
    [Range(0f, 180f)]
    public float maxYawClamp = 20f;

    [Tooltip("Sólo girar si el ángulo es menor/igual a este umbral (modo OnlyIfWithinThreshold).")]
    [Range(0f, 180f)]
    public float yawThreshold = 15f;

    [Header("Animación de cámara (opcional)")]
    public float cameraLift = 0.15f;
    public AnimationCurve cameraCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Control de otros sistemas")]
    public bool isMantling;
    public Behaviour movementScriptToDisable; // ej. PlayerMovementAdvanced_
    public Behaviour[] extraScriptsToDisable;

    [Header("Input")]
    public KeyCode mantleKey = KeyCode.Space;
    public float mantleCooldown = 0.15f;

    [Header("Eventos")]
    public UnityEvent OnMantleStart;
    public UnityEvent OnMantleEnd;

    [Header("Grapple (opcional)")]
    public float grappleOverShootBonus = 0.20f;

    [Header("Debug")]
    public bool debugDraw = false;

    float _lastMantleTime;
    Vector3 _camLocalStart;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (Camera.main)
            cameraPivot = Camera.main.transform;
    }

    void Awake()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();
        if (!capsule)
            capsule = GetComponent<CapsuleCollider>();
        if (cameraPivot)
            _camLocalStart = cameraPivot.localPosition;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (isMantling)
            return;
        if (Input.GetKeyDown(mantleKey))
            TryStartMantle();
    }

    public bool TryMantleFromGrapple(Vector3 attachPoint, Vector3 surfaceNormal)
    {
        if (isMantling)
            return false;
        if (Time.time - _lastMantleTime < mantleCooldown)
            return false;

        Vector3 preferDir = Vector3.ProjectOnPlane(-surfaceNormal, Vector3.up);
        if (preferDir.sqrMagnitude < 1e-6f)
            preferDir = (attachPoint - transform.position).normalized;

        float over = overShoot + grappleOverShootBonus;

        if (
            FindLedgeAnyDir(
                preferDir,
                over,
                topDownCheck,
                forwardCheckDist + forwardDistBoost,
                out var _,
                out var wallNormal,
                out var topPoint,
                out var standPos
            )
        )
        {
            StartCoroutine(CoMantle(wallNormal, standPos, topPoint));
            _lastMantleTime = Time.time;
            return true;
        }
        return false;
    }

    // Mantle normal
    void TryStartMantle()
    {
        if (Time.time - _lastMantleTime < mantleCooldown)
            return;

        Vector3 preferDir = transform.forward;

        if (
            FindLedgeAnyDir(
                preferDir,
                overShoot,
                topDownCheck,
                forwardCheckDist + forwardDistBoost,
                out var _,
                out var wallNormal,
                out var topPoint,
                out var standPos
            )
        )
        {
            StartCoroutine(CoMantle(wallNormal, standPos, topPoint));
            _lastMantleTime = Time.time;
        }
    }

    // Detección en 1..8 direcciones
    bool FindLedgeAnyDir(
        Vector3 preferDir,
        float overShootValue,
        float topDown,
        float castDist,
        out Vector3 hitPoint,
        out Vector3 wallNormal,
        out Vector3 topPoint,
        out Vector3 standPos
    )
    {
        hitPoint = Vector3.zero;
        wallNormal = Vector3.forward;
        topPoint = Vector3.zero;
        standPos = Vector3.zero;

        Vector3 fwd = Vector3.ProjectOnPlane(preferDir, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
            fwd = transform.forward;
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        Vector3[] dirs;
        if (includeDiagonals)
        {
            Vector3 fwdR = (fwd + right).normalized;
            Vector3 fwdL = (fwd - right).normalized;
            Vector3 backR = (-fwd + right).normalized;
            Vector3 backL = (-fwd - right).normalized;

            dirs = multiDirFallback
                ? new[] { fwd, -fwd, right, -right, fwdR, fwdL, backR, backL }
                : new[] { fwd };
        }
        else
        {
            dirs = multiDirFallback ? new[] { fwd, -fwd, right, -right } : new[] { fwd };
        }

        for (int i = 0; i < dirs.Length; i++)
        {
            if (
                FindLedgeWithDir(
                    dirs[i],
                    overShootValue,
                    topDown,
                    castDist,
                    out hitPoint,
                    out wallNormal,
                    out topPoint,
                    out standPos
                )
            )
                return true;
        }
        return false;
    }

    // Detección para una dirección
    bool FindLedgeWithDir(
        Vector3 forwardDir,
        float overShootValue,
        float topDown,
        float castDist,
        out Vector3 hitPoint,
        out Vector3 wallNormal,
        out Vector3 topPoint,
        out Vector3 standPos
    )
    {
        hitPoint = Vector3.zero;
        wallNormal = Vector3.forward;
        topPoint = Vector3.zero;
        standPos = Vector3.zero;

        // Dirección horizontal estable
        Vector3 dirH = Vector3.ProjectOnPlane(forwardDir, Vector3.up);
        if (dirH.sqrMagnitude < 1e-6f)
            dirH = transform.forward;
        dirH.Normalize();

        float feetLift = Mathf.Max(0.05f, capsule ? (capsule.radius * 0.5f) : 0.1f);
        Vector3 originFeet = transform.position + Vector3.up * feetLift;

        // Alturas de muestreo
        Vector3 minOrigin = originFeet + Vector3.up * minLedgeHeight;
        Vector3 maxOrigin = originFeet + Vector3.up * maxLedgeHeight;

        // Declaradas antes para evitar error CS0165
        RaycastHit minHit = new RaycastHit();
        RaycastHit maxHit = new RaycastHit();

        bool minOk = TryWallSweep(minOrigin, dirH, castDist, out minHit);
        bool maxOk = TryWallSweep(maxOrigin, dirH, castDist, out maxHit);

        if (!minOk && !maxOk)
        {
            if (enableAutoBoostOnFail)
            {
                float distBoosted = castDist + boostForwardDist;
                minOk = TryWallSweep(minOrigin, dirH, distBoosted, out minHit);
                maxOk = TryWallSweep(maxOrigin, dirH, distBoosted, out maxHit);
                if (!minOk && !maxOk)
                    return false;
            }
            else
            {
                return false;
            }
        }

        RaycastHit wallHit = minOk ? minHit : maxHit;

        // Asegurar que la normal mira contra la dirección del cast (evita flips)
        if (Vector3.Dot(wallHit.normal, -dirH) < 0f)
            wallHit.normal = -wallHit.normal;

        hitPoint = wallHit.point;
        wallNormal = wallHit.normal;

        // Overshoot / top-down (con posible boost si la pared es alta)
        float overUse = overShootValue;
        float topDownUse = topDown;

        float localYFromFeet = wallHit.point.y - originFeet.y;
        if (enableAutoBoostOnFail && localYFromFeet > minLedgeHeight + 0.2f)
        {
            overUse += boostOvershoot;
            topDownUse += boostTopDown;
        }

        // Ray vertical a la tapa de la plataforma
        int maskTop = topRayHitsAnyLayer
            ? ~0
            : ((topSurfaceLayers.value != 0) ? topSurfaceLayers.value : climbableLayers.value);

        Vector3 over = wallHit.point + (-wallHit.normal * overUse) + Vector3.up * topDownUse;

        if (
            !Physics.Raycast(
                over,
                Vector3.down,
                out RaycastHit topHit,
                topDownUse + 0.3f,
                maskTop,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            // Un reintento algo más alto
            if (
                !Physics.Raycast(
                    over + Vector3.up * 0.2f,
                    Vector3.down,
                    out topHit,
                    topDownUse + 0.5f,
                    maskTop,
                    QueryTriggerInteraction.Ignore
                )
            )
                return false;
        }

        topPoint = topHit.point;

        // Posición final y chequeo de espacio
        float halfHeight = capsule ? (capsule.height * 0.5f) : 0.9f;
        Vector3 back = (-wallNormal) * standBackOffset;
        standPos =
            new Vector3(topPoint.x, topPoint.y + halfHeight + standUpLift, topPoint.z) + back;

        if (!HasCapsuleSpace(standPos))
            return false;

        if (debugDraw)
        {
            Debug.DrawLine(minOrigin, minOrigin + dirH * castDist, Color.yellow, 0.25f);
            Debug.DrawLine(maxOrigin, maxOrigin + dirH * castDist, Color.yellow, 0.25f);
            Debug.DrawRay(wallHit.point, wallNormal * 0.5f, Color.cyan, 0.25f);
            Debug.DrawLine(over, topPoint, Color.green, 0.25f);
        }

        return true;
    }

    // Barrido de spherecasts con backoff y laterales; devuelve el hit más cercano válido
    bool TryWallSweep(Vector3 baseOrigin, Vector3 dirH, float dist, out RaycastHit bestHit)
    {
        bestHit = default;
        float best = float.MaxValue;

        Vector3 side = Vector3.Cross(Vector3.up, dirH).normalized;
        int latSteps = Mathf.Max(0, lateralStepsPerSide);
        int totalBackoffs = Mathf.Max(0, backoffSteps);

        for (int b = 0; b <= totalBackoffs; b++)
        {
            float back = castBackoff + b * Mathf.Max(0f, backoffStep);
            Vector3 rowOrigin = baseOrigin - dirH * back;

            for (int s = -latSteps; s <= latSteps; s++)
            {
                Vector3 o = rowOrigin + side * s * lateralProbe;
                if (
                    Physics.SphereCast(
                        o,
                        forwardCheckRadius,
                        dirH,
                        out RaycastHit h,
                        dist,
                        climbableLayers,
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    if (h.distance < best)
                    {
                        best = h.distance;
                        bestHit = h;
                    }
                }
            }
        }
        return best < float.MaxValue && bestHit.collider != null;
    }

    bool HasCapsuleSpace(Vector3 center)
    {
        if (!capsule)
            return true;
        float radius = capsule.radius * 0.95f;
        float halfHeight = Mathf.Max(radius + 0.01f, capsule.height * 0.5f - radius);
        Vector3 p1 = center + Vector3.up * halfHeight;
        Vector3 p2 = center - Vector3.up * halfHeight;
        return !Physics.CheckCapsule(p1, p2, radius, ~0, QueryTriggerInteraction.Ignore);
    }

    System.Collections.IEnumerator CoMantle(Vector3 wallNormal, Vector3 standPos, Vector3 topPoint)
    {
        isMantling = true;
        OnMantleStart?.Invoke();

        bool movementWasEnabled = false;
        if (movementScriptToDisable)
        {
            movementWasEnabled = movementScriptToDisable.enabled;
            movementScriptToDisable.enabled = false;
        }
        foreach (var b in extraScriptsToDisable)
            if (b && b.enabled)
                b.enabled = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // Alineación según modo
        Quaternion targetRot = startRot;
        if (alignToWallNormal && yawAlignMode != YawAlignMode.None)
        {
            Vector3 desiredFwd = Vector3.ProjectOnPlane(-wallNormal, Vector3.up);
            if (desiredFwd.sqrMagnitude > 1e-6f)
            {
                desiredFwd.Normalize();

                // Forward actual (solo yaw)
                Vector3 curFwd = Vector3
                    .ProjectOnPlane(startRot * Vector3.forward, Vector3.up)
                    .normalized;
                if (curFwd.sqrMagnitude < 1e-6f)
                    curFwd = transform.forward;

                float yawDelta = Vector3.SignedAngle(curFwd, desiredFwd, Vector3.up);

                switch (yawAlignMode)
                {
                    case YawAlignMode.Clamp:
                    {
                        float clamped = Mathf.Clamp(yawDelta, -maxYawClamp, maxYawClamp);
                        desiredFwd = Quaternion.AngleAxis(clamped, Vector3.up) * curFwd;
                        targetRot = Quaternion.LookRotation(desiredFwd, Vector3.up);
                        break;
                    }
                    case YawAlignMode.OnlyIfWithinThreshold:
                    {
                        if (Mathf.Abs(yawDelta) <= yawThreshold)
                            targetRot = Quaternion.LookRotation(desiredFwd, Vector3.up);
                        else
                            targetRot = startRot; // si es mayor al umbral, no gira nada
                        break;
                    }
                }
            }
        }

        // Neutralizar física durante el movimiento
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        Vector3 camLocal0 = cameraPivot ? cameraPivot.localPosition : Vector3.zero;
        if (cameraPivot && _camLocalStart == Vector3.zero)
            _camLocalStart = camLocal0;

        float t = 0f;
        float dur = Mathf.Max(0.01f, mantleDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = mantleCurve.Evaluate(Mathf.Clamp01(t));

            Vector3 pos = Vector3.Lerp(startPos, standPos, k);
            rb.MovePosition(pos);

            // Suavizado de rotación
            float s = Mathf.Clamp01(rotationLerp * Time.deltaTime);
            Quaternion rotStep = Quaternion.Slerp(startRot, targetRot, k);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, rotStep, s));

            // Cámara opcional
            if (cameraPivot)
            {
                float c = cameraCurve.Evaluate(Mathf.Clamp01(t));
                cameraPivot.localPosition = Vector3.Lerp(
                    camLocal0,
                    _camLocalStart + Vector3.up * cameraLift,
                    c
                );
            }

            yield return null;
        }

        rb.MovePosition(standPos);
        rb.MoveRotation(targetRot);

        if (cameraPivot)
            cameraPivot.localPosition = _camLocalStart;

        rb.useGravity = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (movementScriptToDisable)
            movementScriptToDisable.enabled = movementWasEnabled;
        foreach (var b in extraScriptsToDisable)
            if (b)
                b.enabled = true;

        isMantling = false;
        OnMantleEnd?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        if (!debugDraw)
            return;

        Gizmos.color = Color.yellow;

        float feetLift = 0.05f;
        if (capsule)
            feetLift = Mathf.Max(0.05f, capsule.radius * 0.5f);

        Vector3 originFeet = transform.position + Vector3.up * feetLift;

        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 1e-6f)
            fwd = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        Vector3 minOrigin = originFeet + Vector3.up * minLedgeHeight;
        Vector3 maxOrigin = originFeet + Vector3.up * maxLedgeHeight;

        float dist = forwardCheckDist + forwardDistBoost;

        Vector3 minO = minOrigin - fwd * castBackoff;
        Vector3 maxO = maxOrigin - fwd * castBackoff;

        Gizmos.DrawWireSphere(minO + fwd * dist, forwardCheckRadius);
        Gizmos.DrawWireSphere(maxO + fwd * dist, forwardCheckRadius);
        Gizmos.DrawLine(minO, minO + fwd * dist);
        Gizmos.DrawLine(maxO, maxO + fwd * dist);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(minO + right * lateralProbe + fwd * dist, forwardCheckRadius);
        Gizmos.DrawWireSphere(minO - right * lateralProbe + fwd * dist, forwardCheckRadius);
        Gizmos.DrawWireSphere(maxO + right * lateralProbe + fwd * dist, forwardCheckRadius);
        Gizmos.DrawWireSphere(maxO - right * lateralProbe + fwd * dist, forwardCheckRadius);
    }
}
