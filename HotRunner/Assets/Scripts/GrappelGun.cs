using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class GrappleGun : MonoBehaviour
{
    [Header("Input")]
    public KeyCode grappleKey = KeyCode.E; // disparo/retarget principal
    public KeyCode retargetKey = KeyCode.Mouse1; // retarget alterno (click)

    [Header("Cut behavior")]
    [Tooltip("Al soltar Mouse1 se corta si la sesión actual fue iniciada con Mouse1.")]
    public bool stopOnRetargetRelease = true;

    [Header("Salida visual")]
    [Tooltip("Avance hacia adelante del origen del tiro (desde la cámara).")]
    public float castStartOffset = 0.20f;

    [Tooltip("Desplazamiento lateral (derecha +) del origen del tiro (desde la cámara).")]
    public float fireRightOffset = 0.25f;

    [Header("Refs")]
    public Transform firePoint; // normalmente la Main Camera
    public LineRenderer lineRenderer; // Use World Space = ON
    public LayerMask grappleMask = ~0;

    //Integración con LedgeMantle
    [Header("LedgeMantle")]
    public LedgeMantle ledgeMantle;

    [Header("Mantle desde Grapple (gating)")]
    public bool enableMantleWhileAttached = true;
    public bool requireHoldGrappleForMantle = true;
    public float mantleTriggerDistance = 3.0f;
    public float mantleMinVerticalDelta = 0.3f;
    public float mantleMaxVerticalDelta = 7.0f;

    [Header("Top Band del anclaje (costado de la plataforma)")]
    public bool useFixedTopBand = true;
    public float topBandMeters = 1.0f;

    [Range(0.05f, 0.95f)]
    public float topBandFraction = 0.5f;
    public bool useAnchorRootBounds = true;

    [Header("Reach / Cast")]
    public float maxGrappleDistance = 90f;
    public float ropeShootSpeed = 200f;
    public float castRadius = 0.10f;
    public float minAnchorDistance = 1.0f;

    [Header("Joint (tracción base)")]
    public float spring = 150f;
    public float damper = 12f;
    public float massScale = 4f;

    [Range(0.2f, 1f)]
    public float startSlackFactor = 0.9f;

    [Header("Reel (encoger cuerda)")]
    public bool autoReelOnAttach = true;
    public float reelSpeed = 42f;

    [Range(0.0f, 1.0f)]
    public float reelSmooth = 0.45f;

    [Header("Pull Assist (fluidez extra)")]
    public float pullAssistAccel = 70f;
    public float pullAssistMaxHorizSpeed = 55f;

    [Header("Hang / Rope Lock (colgado)")]
    public bool lockWhenClose = true;
    public float hangLockDistance = 1.2f;
    public float hangSpringBoost = 1.6f;
    public float hangDamperBoost = 2.0f;

    [Header("Anti-obstrucción")]
    public bool breakIfObstructed = true;
    public float obstructCheckRadius = 0.15f;
    public float obstructIgnoreNear = 0.6f;
    public float obstructIgnoreNearAnchor = 0.5f;

    [Header("Anti-swing")]
    public bool antiSwing = true;
    public float tangentialDamping = 28f;
    public float maxTangentialSpeed = 8f;

    [Range(0f, 1.2f)]
    public float gravityScaleWhileAttached = 0.6f;
    public float swingReelBoost = 1.2f;

    [Header("Robustez re-enganche")]
    [Tooltip("Rechaza reanclar demasiado cerca del punto actual o del último punto usado.")]
    public float minReanchorSeparation = 0.7f;

    [Tooltip("Ignorar el collider del ancla ACTUAL durante el cast.")]
    public bool ignoreAnchorDuringCast = true;

    [Tooltip(
        "Ventana temporal para proteger de 'rebote' contra el ancla anterior (solo en anti-obstrucción)."
    )]
    public float ignoreLastAnchorWindow = 0.5f;

    // una sola cuerda visible “hard-cut” antes de recastear
    [Header("Single Rope (hard cut)")]
    [Tooltip(
        "Si está activo, al recastear corta la cuerda actual, espera 1-2 FixedUpdates y luego dispara la nueva."
    )]
    public bool singleRopeHardCut = true;

    [Tooltip("Cuántos FixedUpdates esperar entre cortar y castear (1 suele bastar).")]
    public int hardCutDelayFrames = 1;

    [Header("Obstruction tweak")]
    [Tooltip("Tiempo tras Begin/Retarget durante el que NO se chequea obstrucción.")]
    public float obstructionGraceAfterRetarget = 0.25f;

    [Header("Debug")]
    public bool debugLogs = false;

    // runtime
    Rigidbody rb;
    SpringJoint joint;
    Vector3 grapplePoint;

    enum State
    {
        Idle,
        Casting,
        Attached,
    }

    State state = State.Idle;

    enum Driver
    {
        None,
        Key,
        Mouse,
    }

    Driver currentDriver = Driver.None;

    Vector3 ropeTip,
        ropeDir;
    float ropeTraveled;

    float targetMaxDistance;
    bool reeling;

    bool isHanging;
    float baseSpring,
        baseDamper;

    Collider[] selfCols;

    // Ancla actual
    Collider anchorCollider;
    Transform anchorRoot;
    RaycastHit attachHit;

    // “Última” ancla (para anti-obstrucción) + “último punto” (para filtro de distancia)
    Transform lastAnchorRoot;
    Vector3 lastAnchorPoint;
    float lastAnchorUntilTime;

    // ventana sin obstrucción
    float noObstructionUntil;

    bool spacePressedBuffered;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (firePoint == null && Camera.main)
            firePoint = Camera.main.transform;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
        }

        selfCols = GetComponentsInChildren<Collider>(true);
        baseSpring = spring;
        baseDamper = damper;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            spacePressedBuffered = true;

        bool keyDown = Input.GetKeyDown(grappleKey);
        bool keyUp = Input.GetKeyUp(grappleKey);
        bool keyHold = Input.GetKey(grappleKey);

        bool mouseDown = Input.GetKeyDown(retargetKey);
        bool mouseUp = Input.GetKeyUp(retargetKey);

        // START / RETARGET
        if (keyDown)
        {
            if (state == State.Idle)
            {
                if (singleRopeHardCut)
                    StartCoroutine(BeginAfterHardCut(Driver.Key));
                else
                    BeginCast(Driver.Key);
            }
            else
            {
                if (singleRopeHardCut)
                    StartCoroutine(RetargetAfterHardCut(Driver.Key));
                else
                    RetargetCast(Driver.Key);
            }
        }

        // Mouse1 NO inicia desde Idle; sólo retarget si hay sesión activa
        if (mouseDown && state != State.Idle)
        {
            if (singleRopeHardCut)
                StartCoroutine(RetargetAfterHardCut(Driver.Mouse));
            else
                RetargetCast(Driver.Mouse);
        }

        // STOP por soltar el botón que inició la sesión
        if (keyUp && currentDriver == Driver.Key)
            StopGrapple();

        if (mouseUp && currentDriver == Driver.Mouse && stopOnRetargetRelease)
            StopGrapple();

        // Reel solo con E
        reeling = (state == State.Attached) && keyHold;
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Casting:
                UpdateCasting();
                break;
            case State.Attached:
                UpdateAttached();
                break;
        }
        spacePressedBuffered = false;
    }

    // CAST helpers
    Vector3 ComputeOrigin()
    {
        if (!firePoint)
            return transform.position;
        return firePoint.position
            + firePoint.right * fireRightOffset
            + firePoint.forward * castStartOffset;
    }

    Vector3 ComputeForward() => (firePoint ? firePoint.forward : transform.forward);

    void BeginCast(Driver driver)
    {
        if (!firePoint)
            return;

        currentDriver = driver;
        state = State.Casting;

        // Guardar info del ancla actual para proteger un rato contra obstrucción y reanclaje-cerca
        StashLastAnchor();

        Vector3 origin = ComputeOrigin();
        ropeTip = origin;
        ropeDir = ComputeForward().normalized;
        ropeTraveled = 0f;

        isHanging = false;
        RestoreJointTune();
        ClearAnchorInfo(); // limpia el ancla ACTUAL (la "última" quedó stasheada)

        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            UpdateLine(ropeTip);
        }

        noObstructionUntil = Time.time + Mathf.Max(0f, obstructionGraceAfterRetarget);

        if (debugLogs)
            Debug.Log($"[GrappleGun] BeginCast() by {driver}");
    }

    // hard cut helpers
    IEnumerator BeginAfterHardCut(Driver d)
    {
        // por si algo quedó enganchado (no debería en Idle), limpieza total
        HardStop();
        // esperar 1..N FixedUpdates para que el motor procese destrucción del joint
        int frames = Mathf.Max(1, hardCutDelayFrames);
        for (int i = 0; i < frames; i++)
            yield return new WaitForFixedUpdate();
        // comenzar
        currentDriver = d;
        state = State.Casting;

        Vector3 origin = ComputeOrigin();
        ropeTip = origin;
        ropeDir = ComputeForward().normalized;
        ropeTraveled = 0f;

        isHanging = false;
        RestoreJointTune();
        ClearAnchorInfo();

        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            UpdateLine(ropeTip);
        }
        noObstructionUntil = Time.time + Mathf.Max(0f, obstructionGraceAfterRetarget);
    }

    IEnumerator RetargetAfterHardCut(Driver d)
    {
        // cortar la cuerda actual y limpiar por completo
        HardStop();
        int frames = Mathf.Max(1, hardCutDelayFrames);
        for (int i = 0; i < frames; i++)
            yield return new WaitForFixedUpdate();
        // arrancar nuevo cast
        currentDriver = d;
        state = State.Casting;

        Vector3 origin = ComputeOrigin();
        ropeTip = origin;
        ropeDir = ComputeForward().normalized;
        ropeTraveled = 0f;

        isHanging = false;
        RestoreJointTune();
        ClearAnchorInfo();

        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            UpdateLine(ropeTip);
        }
        noObstructionUntil = Time.time + Mathf.Max(0f, obstructionGraceAfterRetarget);
    }

    void RetargetCast(Driver driver)
    {
        if (debugLogs)
            Debug.Log($"[GrappleGun] RetargetCast() by {driver}");

        currentDriver = driver;

        // Guardar info del ancla actual antes de destruir el joint
        StashLastAnchor();

        if (joint)
        {
            Destroy(joint);
            joint = null;
        }
        state = State.Casting;

        Vector3 origin = ComputeOrigin();
        ropeTip = origin;
        ropeDir = ComputeForward().normalized;
        ropeTraveled = 0f;

        isHanging = false;
        RestoreJointTune();
        ClearAnchorInfo(); // no borra lastAnchor

        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            UpdateLine(ropeTip);
        }
        noObstructionUntil = Time.time + Mathf.Max(0f, obstructionGraceAfterRetarget);
    }

    void UpdateCasting()
    {
        float step = ropeShootSpeed * Time.fixedDeltaTime;
        Vector3 prevTip = ropeTip;
        Vector3 nextTip = ropeTip + ropeDir * step;

        if (SphereCastFiltered(prevTip, ropeDir, out RaycastHit hit, step, grappleMask))
        {
            if (Vector3.Distance(transform.position, hit.point) >= minAnchorDistance)
            {
                grapplePoint = hit.point;
                ropeTip = grapplePoint;
                if (lineRenderer)
                    UpdateLine(ropeTip);
                AttachJoint(hit);
                return;
            }
        }

        ropeTip = nextTip;
        ropeTraveled += step;
        if (lineRenderer)
            UpdateLine(ropeTip);

        if (ropeTraveled >= maxGrappleDistance)
            StopGrapple();
    }

    // Filtro de impactos para adquirir nuevo ancla
    bool SphereCastFiltered(
        Vector3 origin,
        Vector3 dir,
        out RaycastHit bestHit,
        float dist,
        LayerMask mask
    )
    {
        var hits = Physics.SphereCastAll(
            origin,
            castRadius,
            dir,
            dist,
            mask,
            QueryTriggerInteraction.Ignore
        );
        bestHit = default;
        float best = float.MaxValue;

        bool checkLastPoint = Time.time <= lastAnchorUntilTime;
        float minSq = minReanchorSeparation * minReanchorSeparation;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null)
                continue;

            if (IsOwnCollider(h.collider))
                continue;
            if (ignoreAnchorDuringCast && IsAnchorCollider(h.collider))
                continue;
            if (state == State.Attached && (grapplePoint - h.point).sqrMagnitude < minSq)
                continue;
            if (checkLastPoint && (lastAnchorPoint - h.point).sqrMagnitude < minSq)
                continue;

            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
            }
        }
        return best < float.MaxValue;
    }

    void StashLastAnchor()
    {
        if (anchorRoot != null)
        {
            lastAnchorRoot = anchorRoot;
            lastAnchorPoint = grapplePoint;
            lastAnchorUntilTime = Time.time + Mathf.Max(0.05f, ignoreLastAnchorWindow);
        }
        else
        {
            lastAnchorUntilTime = Time.time;
        }
    }

    // ATTACH
    void AttachJoint(RaycastHit hit)
    {
        attachHit = hit;
        anchorCollider = hit.collider;
        anchorRoot = anchorCollider ? anchorCollider.transform.root : null;

        joint = gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = grapplePoint;

        float dist = Vector3.Distance(transform.position, grapplePoint);
        joint.maxDistance = dist * startSlackFactor;
        joint.minDistance = dist * 0.25f;

        joint.spring = spring;
        joint.damper = damper;
        joint.massScale = massScale;

        targetMaxDistance = joint.maxDistance;
        reeling = autoReelOnAttach;

        state = State.Attached;
        if (lineRenderer)
            UpdateLine(grapplePoint);

        if (debugLogs)
            Debug.Log("[GrappleGun] Attached at " + grapplePoint);
    }

    void UpdateAttached()
    {
        // E + Space -> mantle desde el grapple
        if (enableMantleWhileAttached && ledgeMantle && !ledgeMantle.isMantling)
        {
            bool keyHold = Input.GetKey(grappleKey);
            bool holdOK = !requireHoldGrappleForMantle || keyHold;

            if (holdOK && spacePressedBuffered)
            {
                float distNow = Vector3.Distance(transform.position, grapplePoint);
                float verticalDelta = attachHit.point.y - transform.position.y;

                bool withinDist = distNow <= mantleTriggerDistance;
                bool withinVertical =
                    verticalDelta >= mantleMinVerticalDelta
                    && verticalDelta <= mantleMaxVerticalDelta;
                bool topBandOK = AnchorIsInTopBand(attachHit);

                if (debugLogs)
                    Debug.Log(
                        $"[GrappleGun] MantleTry d:{distNow:F2}/{mantleTriggerDistance} vΔ:{verticalDelta:F2} tb:{topBandOK}"
                    );

                if (withinDist && withinVertical && topBandOK)
                {
                    bool ok = ledgeMantle.TryMantleFromGrapple(attachHit.point, attachHit.normal);
                    if (ok)
                    {
                        StopGrapple();
                        return;
                    }
                }
            }
        }

        // REEL/assist/swing
        Vector3 toHook = grapplePoint - transform.position;
        float distNow2 = toHook.magnitude;
        Vector3 dir = toHook.sqrMagnitude > 1e-8f ? toHook / distNow2 : Vector3.up;

        Vector3 v = rb.velocity;
        float vRad = Vector3.Dot(v, dir);
        Vector3 vTan = v - dir * vRad;
        float tan = vTan.magnitude;

        float reelDt = reelSpeed * Time.fixedDeltaTime;
        float extra = swingReelBoost * tan * Time.fixedDeltaTime;
        float newTarget = Mathf.Max(hangLockDistance, targetMaxDistance - reelDt - extra);

        targetMaxDistance = Mathf.Lerp(
            targetMaxDistance,
            newTarget,
            1f - Mathf.Exp(-reelSmooth * 60f * Time.fixedDeltaTime)
        );
        joint.maxDistance = Mathf.Min(joint.maxDistance, targetMaxDistance);
        joint.minDistance = Mathf.Min(
            joint.minDistance,
            Mathf.Max(hangLockDistance * 0.25f, targetMaxDistance * 0.25f)
        );

        rb.AddForce(dir * pullAssistAccel, ForceMode.Acceleration);

        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (hv.magnitude > pullAssistMaxHorizSpeed)
        {
            hv = hv.normalized * pullAssistMaxHorizSpeed;
            rb.velocity = new Vector3(hv.x, rb.velocity.y, hv.z);
        }

        if (antiSwing && tan > 1e-4f)
        {
            rb.AddForce(
                -vTan.normalized * Mathf.Min(tan, maxTangentialSpeed) * tangentialDamping,
                ForceMode.Acceleration
            );
            if (gravityScaleWhileAttached < 1f)
            {
                float k = 1f - gravityScaleWhileAttached;
                rb.AddForce(-Physics.gravity * k, ForceMode.Acceleration);
            }
        }

        // Colgado
        if (lockWhenClose && distNow2 <= hangLockDistance + 0.02f && Input.GetKey(grappleKey))
        {
            if (!isHanging)
            {
                isHanging = true;
                joint.maxDistance = hangLockDistance;
                joint.minDistance = hangLockDistance * 0.98f;
                joint.spring = baseSpring * hangSpringBoost;
                joint.damper = baseDamper * hangDamperBoost;
            }

            float radialSpeed = Vector3.Dot(rb.velocity, dir);
            rb.AddForce(-dir * radialSpeed * 10f, ForceMode.Acceleration);
        }
        else if (isHanging && distNow2 > hangLockDistance + 0.25f)
        {
            isHanging = false;
            RestoreJointTune();
        }

        // Anti-obstrucción (SOLO capas anclables) + tiempo de gracia
        if (breakIfObstructed && Time.time >= noObstructionUntil)
        {
            Vector3 from = ComputeOrigin();
            Vector3 path = grapplePoint - from;
            float total = path.magnitude;

            float startForgive = Mathf.Clamp(obstructIgnoreNear, 0f, total);
            float endForgive = Mathf.Clamp(obstructIgnoreNearAnchor, 0f, total - startForgive);
            float castLen = total - startForgive - endForgive;

            if (castLen > 0.01f)
            {
                Vector3 castFrom = from + path.normalized * startForgive;

                // SOLO capas "anclables" (las del grapple)
                int mask = grappleMask;

                if (
                    Physics.SphereCast(
                        castFrom,
                        obstructCheckRadius,
                        path.normalized,
                        out RaycastHit hit,
                        castLen,
                        mask,
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    bool isOwn = IsOwnCollider(hit.collider);
                    bool isCurrent = IsAnchorCollider(hit.collider);
                    bool isOld =
                        (Time.time <= lastAnchorUntilTime) && IsOldAnchorCollider(hit.collider);

                    if (!isOwn && !isCurrent && !isOld)
                        StopGrapple();
                }
            }
        }

        if (lineRenderer)
            UpdateLine(grapplePoint);
    }

    void RestoreJointTune()
    {
        if (joint == null)
            return;
        joint.spring = baseSpring;
        joint.damper = baseDamper;
    }

    // VISUAL
    void UpdateLine(Vector3 end)
    {
        if (!lineRenderer)
            return;
        Vector3 start = ComputeOrigin();
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    // STOP
    public void StopGrapple()
    {
        if (debugLogs)
            Debug.Log("[GrappleGun] StopGrapple()");

        // Guarda ancla actual para la ventana anti-rebote
        StashLastAnchor();

        if (joint)
            Destroy(joint);
        joint = null;

        // apagar y resetear la línea, evita “tira al inicio”
        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            Vector3 s = ComputeOrigin();
            lineRenderer.SetPosition(0, s);
            lineRenderer.SetPosition(1, s);
        }

        state = State.Idle;
        currentDriver = Driver.None;
        isHanging = false;
        reeling = false;

        // limpiar ancla actual y punto (evita referencias viejas)
        grapplePoint = transform.position;
        ClearAnchorInfo();
    }

    // Corte duro: NO stashea last anchor y limpia todo
    void HardStop()
    {
        if (joint)
            Destroy(joint);
        joint = null;

        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            Vector3 s = ComputeOrigin();
            lineRenderer.SetPosition(0, s);
            lineRenderer.SetPosition(1, s);
        }

        state = State.Idle;
        currentDriver = Driver.None;
        isHanging = false;
        reeling = false;

        grapplePoint = transform.position;
        ClearAnchorInfo();

        lastAnchorRoot = null;
        lastAnchorPoint = Vector3.zero;
        lastAnchorUntilTime = 0f;
    }

    void ClearAnchorInfo()
    {
        anchorCollider = null;
        anchorRoot = null;
        attachHit = default;
    }

    // Helpers
    bool IsOwnCollider(Collider c)
    {
        if (c == null || selfCols == null)
            return false;
        for (int i = 0; i < selfCols.Length; i++)
            if (selfCols[i] == c)
                return true;
        return false;
    }

    bool IsAnchorCollider(Collider c)
    {
        if (c == null)
            return false;
        if (anchorCollider != null && c == anchorCollider)
            return true;

        if (anchorRoot != null)
        {
            Transform t = c.transform;
            if (t == anchorRoot || t.IsChildOf(anchorRoot) || anchorRoot.IsChildOf(t))
                return true;
        }
        return false;
    }

    bool IsOldAnchorCollider(Collider c)
    {
        if (c == null || lastAnchorRoot == null)
            return false;
        Transform t = c.transform;
        return (t == lastAnchorRoot || t.IsChildOf(lastAnchorRoot) || lastAnchorRoot.IsChildOf(t));
    }

    // Check de superficie alta de plataforma
    bool AnchorIsInTopBand(RaycastHit hit)
    {
        if (hit.collider == null)
            return false;

        Bounds b = useAnchorRootBounds
            ? GetHierarchyBounds(hit.collider.transform.root)
            : hit.collider.bounds;

        float topY = b.max.y;
        float bottomY = b.min.y;
        float height = Mathf.Max(0.0001f, topY - bottomY);

        float band = useFixedTopBand
            ? Mathf.Min(topBandMeters, height)
            : height * Mathf.Clamp01(topBandFraction);
        float thresholdY = topY - band;

        return hit.point.y >= thresholdY;
    }

    Bounds GetHierarchyBounds(Transform root)
    {
        bool hasAny = false;
        Bounds combined = new Bounds(root.position, Vector3.zero);

        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c == null)
                continue;
            if (!hasAny)
            {
                combined = c.bounds;
                hasAny = true;
            }
            else
                combined.Encapsulate(c.bounds);
        }

        var rends = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r == null)
                continue;
            if (!hasAny)
            {
                combined = r.bounds;
                hasAny = true;
            }
            else
                combined.Encapsulate(r.bounds);
        }

        if (!hasAny)
            combined = new Bounds(root.position, Vector3.one * 0.01f);
        return combined;
    }

    void OnDisable() => HardStop();
}
