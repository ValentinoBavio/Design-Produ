using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrappleGun : MonoBehaviour
{
    [Header("Input")]
    public KeyCode grappleKey = KeyCode.E; // mantener para sostener; soltar = cortar

    [Header("Salida visual (origen del cast)")]
    [Tooltip("Avance hacia adelante del origen del tiro (desde la cámara).")]
    public float castStartOffset = 0.20f;
    [Tooltip("Desplazamiento lateral (derecha +) del origen del tiro (desde la cámara).")]
    public float fireRightOffset = 0.25f;

    [Header("Refs")]
    public Transform firePoint;            // normalmente la Main Camera
    public LineRenderer lineRenderer;      // Use World Space = ON
    public LayerMask grappleMask = ~0;     // capas válidas de anclaje

    // ---------- UI: Prompt debajo de la mira (opcional) ----------
    [Header("Prompt UI (opcional)")]
    public TextMeshProUGUI promptText;
    public float promptFadeSpeed = 8f;
    public bool showPromptOnlyWhenIdle = true;
    public bool promptStickToBottom = false;
    public float promptBottomY = 120f;

    float promptTargetAlpha;   // 0..1
    Color promptBaseColor;
    bool promptVisible;

    // ---------- UI: Retícula / HUD ----------
    [Header("Retícula (mira)")]
    public Image reticleIcon;
    public Color reticleReadyColor = Color.white;
    public Color reticleNoTargetColor = new Color(1f, 1f, 1f, 0.25f);
    public Color reticleCooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    public TextMeshProUGUI reticleKeyHint;
    public string reticleKeyGlyph = "E";

    [Header("HUD (ícono del Grapple)")]
    public Image grappleHudIcon;
    public Color hudReadyColor = Color.white;
    public Color hudCooldownColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public bool hudUseFillCooldown = true;

    // ---------- Cooldown ----------
    [Header("Cooldown")]
    public bool useCooldown = true;
    [Tooltip("Segundos de cooldown al cortar el grapple o fallar un cast.")]
    public float grappleCooldown = 0.85f;

    float nextReadyTime = 0f;
    bool Ready => !useCooldown || Time.time >= nextReadyTime;
    float CooldownRemaining => useCooldown ? Mathf.Max(0f, nextReadyTime - Time.time) : 0f;
    float Cooldown01 => useCooldown ? Mathf.InverseLerp(grappleCooldown, 0f, CooldownRemaining) : 0f;

    // ---------- Alcance / Cast ----------
    [Header("Reach / Cast")]
    public float maxGrappleDistance = 90f;
    public float ropeShootSpeed = 200f;
    public float castRadius = 0.10f;
    public float minAnchorDistance = 1.0f;

    // ---------- Joint (tracción base) ----------
    [Header("Joint (tracción base)")]
    public float spring = 150f;
    public float damper = 12f;
    public float massScale = 4f;
    [Range(0.2f, 1f)] public float startSlackFactor = 0.9f;

    // ---------- Reel ----------
    [Header("Reel (encoger cuerda)")]
    public bool autoReelOnAttach = true;
    public float reelSpeed = 42f;
    [Range(0.0f, 1.0f)] public float reelSmooth = 0.45f;

    // ---------- Assist ----------
    [Header("Pull Assist")]
    public float pullAssistAccel = 70f;
    public float pullAssistMaxHorizSpeed = 55f;

    // ---------- Hang / Lock ----------
    [Header("Hang / Rope Lock")]
    [Tooltip("El lock sólo funciona si mantenés E; al soltar se corta igualmente.")]
    public bool lockWhenClose = true;
    public float hangLockDistance = 1.2f;
    public float hangSpringBoost = 1.6f;
    public float hangDamperBoost = 2.0f;

    // ---------- Anti-obstrucción ----------
    [Header("Anti-obstrucción")]
    public bool breakIfObstructed = true;
    public float obstructCheckRadius = 0.15f;
    public float obstructIgnoreNear = 0.6f;
    public float obstructIgnoreNearAnchor = 0.5f;

    // ---------- Anti-swing / gravedad escalada ----------
    [Header("Anti-swing")]
    public bool antiSwing = true;
    public float tangentialDamping = 28f;
    public float maxTangentialSpeed = 8f;
    [Range(0f, 1.2f)] public float gravityScaleWhileAttached = 0.6f;
    public float swingReelBoost = 1.2f;

    // ---------- Robustez re-enganche ----------
    [Header("Robustez re-enganche")]
    public float minReanchorSeparation = 0.7f;
    public bool ignoreAnchorDuringCast = true;
    public float ignoreLastAnchorWindow = 0.5f;

    [Header("Single Rope (hard cut)")]
    public bool singleRopeHardCut = true;
    public int hardCutDelayFrames = 1;

    [Header("Obstruction tweak")]
    public float obstructionGraceAfterRetarget = 0.25f;

    // ---------- Audio ----------
    [Header("Audio")]
    public AudioSource sfxOneShot;
    public AudioClip sfxShoot;
    public AudioClip sfxAttach;
    public AudioClip sfxFail;
    public AudioClip sfxCooldownDenied;
    public AudioClip sfxGrapplePull;
    public AudioSource sfxPullSource;

    [Header("Debug")]
    public bool debugLogs = false;

    // --------- runtime ---------
    Rigidbody rb;
    SpringJoint joint;
    Vector3 grapplePoint;

    enum State { Idle, Casting, Attached }
    State state = State.Idle;

    Vector3 ropeTip, ropeDir;
    float ropeTraveled;

    float targetMaxDistance;
    bool reeling;

    bool isHanging;
    float baseSpring, baseDamper;

    Collider[] selfCols;

    // Ancla actual
    Collider anchorCollider;
    Transform anchorRoot;
    RaycastHit attachHit;

    // Última ancla (anti-obstrucción)
    Transform lastAnchorRoot;
    Vector3 lastAnchorPoint;
    float lastAnchorUntilTime;

    float noObstructionUntil;

    bool spacePressedBuffered;

    // flags de sesión/audio
    bool castInProgress;
    bool attachedThisCast;
    bool pullPlayedThisAttach;
    bool prevReeling;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (firePoint == null && Camera.main) firePoint = Camera.main.transform;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
        }

        selfCols = GetComponentsInChildren<Collider>(true);
        baseSpring = spring;
        baseDamper = damper;

        // Prompt
        if (promptText)
        {
            promptBaseColor = promptText.color;
            SetPromptAlphaImmediate(0f);
            if (promptStickToBottom)
            {
                var rt = promptText.GetComponent<RectTransform>();
                if (rt) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, promptBottomY);
            }
        }

        // Retícula
        if (reticleIcon) reticleIcon.enabled = false;
        if (reticleKeyHint) reticleKeyHint.gameObject.SetActive(false);

        // HUD
        SetupHudIcon();

        // Audio pull source
        if (sfxPullSource)
        {
            sfxPullSource.loop = false;
            sfxPullSource.playOnAwake = false;
            if (sfxPullSource.clip == null) sfxPullSource.clip = sfxGrapplePull;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            spacePressedBuffered = true;

        bool keyDown  = Input.GetKeyDown(grappleKey);
        bool keyUp    = Input.GetKeyUp(grappleKey);
        bool keyHold  = Input.GetKey(grappleKey);

        // UI
        UpdateAimUI();
        TickPromptFade();
        UpdateHudIcon();

        // HOLD-TO-SUSTAIN: si se suelta E en cualquier estado, cortar
        if (keyUp)
        {
            StopGrapple(); // siempre corta al soltar
            return;
        }

        // INICIO: sólo si está listo y E se presiona
        if (keyDown)
        {
            if (!Ready)
            {
                PlayOneShotSafe(sfxCooldownDenied, 1f);
            }
            else if (state == State.Idle)
            {
                if (singleRopeHardCut) StartCoroutine(BeginAfterHardCut());
                else                   BeginCast();
            }
            else
            {
                // Si ya hay sesión (Casting/Attached) y apretaste de nuevo, reseteo cast
                if (singleRopeHardCut) StartCoroutine(RetargetAfterHardCut());
                else                   RetargetCast();
            }
        }

        // Reel sólo si sostenés E mientras estás Attached
        reeling = (state == State.Attached) && keyHold;

        // one-shot de “grappeling” al empezar a reeler
        if (state == State.Attached && castInProgress && attachedThisCast)
        {
            if (!pullPlayedThisAttach && !prevReeling && reeling)
            {
                PlayPullStartIfNeeded();
                pullPlayedThisAttach = true;
            }
            if (prevReeling && !reeling)
            {
                StopPullNow();
            }
        }
        prevReeling = reeling;
    }

    void FixedUpdate()
    {
        // Si E NO se mantiene: cortar instantáneamente también desde Fixed (por si el Update no alcanzó)
        if (!Input.GetKey(grappleKey))
        {
            if (state != State.Idle)
            {
                StopGrapple();
            }
            spacePressedBuffered = false;
            return;
        }

        switch (state)
        {
            case State.Casting:  UpdateCasting();  break;
            case State.Attached: UpdateAttached(); break;
        }
        spacePressedBuffered = false;
    }

    // ================== CAST helpers ==================
    Vector3 ComputeOrigin()
    {
        if (!firePoint) return transform.position;
        return firePoint.position + firePoint.right * fireRightOffset + firePoint.forward * castStartOffset;
    }
    Vector3 ComputeForward() => (firePoint ? firePoint.forward : transform.forward);

    void BeginCast()
    {
        if (!firePoint) return;

        state = State.Casting;

        castInProgress = true;
        attachedThisCast = false;
        pullPlayedThisAttach = false;
        prevReeling = false;

        PlayOneShotSafe(sfxShoot, 1f);

        StashLastAnchor();

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

        noObstructionUntil = Time.time + Mathf.Max(0, obstructionGraceAfterRetarget);

        if (debugLogs) Debug.Log("[GrappleGun] BeginCast()");
    }

    IEnumerator BeginAfterHardCut()
    {
        HardStop();
        int frames = Mathf.Max(1, hardCutDelayFrames);
        for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        BeginCast();
    }

    IEnumerator RetargetAfterHardCut()
    {
        HardStop();
        int frames = Mathf.Max(1, hardCutDelayFrames);
        for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        BeginCast();
    }

    void RetargetCast()
    {
        if (debugLogs) Debug.Log("[GrappleGun] RetargetCast()");
        StashLastAnchor();

        if (joint)
        {
            Destroy(joint);
            joint = null;
        }
        state = State.Casting;

        castInProgress = true;
        attachedThisCast = false;
        pullPlayedThisAttach = false;
        prevReeling = false;

        PlayOneShotSafe(sfxShoot, 1f);

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
        noObstructionUntil = Time.time + Mathf.Max(0, obstructionGraceAfterRetarget);
    }

    void UpdateCasting()
    {
        // si dejaste de sostener E durante el cast, se corta en FixedUpdate
        float step = ropeShootSpeed * Time.fixedDeltaTime;
        Vector3 prevTip = ropeTip;
        Vector3 nextTip = ropeTip + ropeDir * step;

        if (SphereCastFiltered(prevTip, ropeDir, out RaycastHit hit, step, grappleMask))
        {
            if (Vector3.Distance(transform.position, hit.point) >= minAnchorDistance)
            {
                grapplePoint = hit.point;
                ropeTip = grapplePoint;
                if (lineRenderer) UpdateLine(ropeTip);
                AttachJoint(hit);
                return;
            }
        }

        ropeTip = nextTip;
        ropeTraveled += step;
        if (lineRenderer) UpdateLine(ropeTip);

        if (ropeTraveled >= maxGrappleDistance)
        {
            // fin de cast sin impacto -> FAIL + cooldown
            StopGrapple(true);
        }
    }

    bool SphereCastFiltered(Vector3 origin, Vector3 dir, out RaycastHit bestHit, float dist, LayerMask mask)
    {
        var hits = Physics.SphereCastAll(origin, castRadius, dir, dist, mask, QueryTriggerInteraction.Ignore);
        bestHit = default;
        float best = float.MaxValue;

        bool checkLastPoint = Time.time <= lastAnchorUntilTime;
        float minSq = minReanchorSeparation * minReanchorSeparation;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            if (IsOwnCollider(h.collider)) continue;
            if (ignoreAnchorDuringCast && IsAnchorCollider(h.collider)) continue;
            if (state == State.Attached && (grapplePoint - h.point).sqrMagnitude < minSq) continue;
            if (checkLastPoint && (lastAnchorPoint - h.point).sqrMagnitude < minSq) continue;

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

    // ================== ATTACH ==================
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
        if (lineRenderer) UpdateLine(grapplePoint);

        attachedThisCast = true;
        pullPlayedThisAttach = false;
        prevReeling = false;
        PlayOneShotSafe(sfxAttach, 1f);

        if (debugLogs) Debug.Log("[GrappleGun] Attached at " + grapplePoint);
    }

    void UpdateAttached()
    {
        // HOLD-TO-SUSTAIN: seguridad extra (si por cualquier razón no estás manteniendo E, cortar)
        if (!Input.GetKey(grappleKey))
        {
            StopGrapple();
            return;
        }

        Vector3 toHook = grapplePoint - transform.position;
        float distNow2 = toHook.magnitude;
        Vector3 dir = toHook.sqrMagnitude > 1e-8f ? toHook / distNow2 : Vector3.up;

        // Assist
        rb.AddForce(dir * pullAssistAccel, ForceMode.Acceleration);

        Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (hv.magnitude > pullAssistMaxHorizSpeed)
        {
            hv = hv.normalized * pullAssistMaxHorizSpeed;
            rb.velocity = new Vector3(hv.x, rb.velocity.y, hv.z);
        }

        // Reel
        float tan = 0f;
        {
            Vector3 v = rb.velocity;
            float vRad = Vector3.Dot(v, dir);
            Vector3 vTan = v - dir * vRad;
            tan = vTan.magnitude;
        }

        float reelDt = reelSpeed * Time.fixedDeltaTime;
        float extra = swingReelBoost * tan * Time.fixedDeltaTime;
        float newTarget = Mathf.Max(hangLockDistance, targetMaxDistance - reelDt - extra);

        targetMaxDistance = Mathf.Lerp(targetMaxDistance, newTarget, 1f - Mathf.Exp(-reelSmooth * 60f * Time.fixedDeltaTime));
        if (joint)
        {
            joint.maxDistance = Mathf.Min(joint.maxDistance, targetMaxDistance);
            joint.minDistance = Mathf.Min(joint.minDistance, Mathf.Max(hangLockDistance * 0.25f, targetMaxDistance * 0.25f));
        }

        // Anti-swing + gravedad reducida
        if (antiSwing && tan > 1e-4f)
        {
            Vector3 v = rb.velocity;
            float vRad = Vector3.Dot(v, dir);
            Vector3 vTan = v - dir * vRad;

            rb.AddForce(-vTan.normalized * Mathf.Min(tan, maxTangentialSpeed) * tangentialDamping, ForceMode.Acceleration);

            if (gravityScaleWhileAttached < 1f)
            {
                float k = 1f - gravityScaleWhileAttached;
                rb.AddForce(-Physics.gravity * k, ForceMode.Acceleration);
            }
        }

        // Lock cerca del ancla — SOLO si mantenés E
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

        // Anti-obstrucción
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
                int mask = grappleMask;

                if (Physics.SphereCast(castFrom, obstructCheckRadius, path.normalized, out RaycastHit hit, castLen, mask, QueryTriggerInteraction.Ignore))
                {
                    bool isOwn = IsOwnCollider(hit.collider);
                    bool isCurrent = IsAnchorCollider(hit.collider);
                    bool isOld = (Time.time <= lastAnchorUntilTime) && IsOldAnchorCollider(hit.collider);

                    if (!isOwn && !isCurrent && !isOld)
                        StopGrapple();
                }
            }
        }

        if (lineRenderer) UpdateLine(grapplePoint);
    }

    void RestoreJointTune()
    {
        if (!joint) return;
        joint.spring = baseSpring;
        joint.damper = baseDamper;
    }

    // VISUAL
    void UpdateLine(Vector3 end)
    {
        if (!lineRenderer) return;
        Vector3 start = ComputeOrigin();
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    // ================== STOP ==================
    public void StopGrapple(bool playFail = false)
    {
        // FAIL: sólo si veníamos casteando y no pegamos
        if (playFail && castInProgress && !attachedThisCast)
            PlayOneShotSafe(sfxFail, 1f);

        // Cooldown al cortar
        if (useCooldown) nextReadyTime = Time.time + grappleCooldown;

        // Stash para ventana anti-rebote
        StashLastAnchor();

        if (joint) Destroy(joint);
        joint = null;

        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            Vector3 s = ComputeOrigin();
            lineRenderer.SetPosition(0, s);
            lineRenderer.SetPosition(1, s);
        }

        StopPullNow();

        state = State.Idle;
        isHanging = false;
        reeling = false;

        grapplePoint = transform.position;
        ClearAnchorInfo();

        castInProgress = false;
        attachedThisCast = false;
        pullPlayedThisAttach = false;
        prevReeling = false;
    }

    void HardStop()
    {
        if (joint) Destroy(joint);
        joint = null;

        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 2;
            Vector3 s = ComputeOrigin();
            lineRenderer.SetPosition(0, s);
            lineRenderer.SetPosition(1, s);
        }

        StopPullNow();

        state = State.Idle;
        isHanging = false;
        reeling = false;

        grapplePoint = transform.position;
        ClearAnchorInfo();

        lastAnchorRoot = null;
        lastAnchorPoint = Vector3.zero;
        lastAnchorUntilTime = 0f;

        castInProgress = false;
        attachedThisCast = false;
        pullPlayedThisAttach = false;
        prevReeling = false;
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
        if (c == null || selfCols == null) return false;
        for (int i = 0; i < selfCols.Length; i++)
            if (selfCols[i] == c) return true;
        return false;
    }
    bool IsAnchorCollider(Collider c)
    {
        if (c == null) return false;
        if (anchorCollider != null && c == anchorCollider) return true;

        if (anchorRoot != null)
        {
            Transform t = c.transform;
            if (t == anchorRoot || t.IsChildOf(anchorRoot) || anchorRoot.IsChildOf(t)) return true;
        }
        return false;
    }
    bool IsOldAnchorCollider(Collider c)
    {
        if (c == null || lastAnchorRoot == null) return false;
        Transform t = c.transform;
        return (t == lastAnchorRoot || t.IsChildOf(lastAnchorRoot) || lastAnchorRoot.IsChildOf(t));
    }

    Bounds GetHierarchyBounds(Transform root)
    {
        bool hasAny = false;
        Bounds combined = new Bounds(root.position, Vector3.zero);

        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c == null) continue;
            if (!hasAny) { combined = c.bounds; hasAny = true; }
            else combined.Encapsulate(c.bounds);
        }

        var rends = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (!hasAny) { combined = r.bounds; hasAny = true; }
            else combined.Encapsulate(r.bounds);
        }

        if (!hasAny) combined = new Bounds(root.position, Vector3.one * 0.01f);
        return combined;
    }

    void OnDisable() => HardStop();

    // ==================== UI LOGIC ====================
    void SetupHudIcon()
    {
        if (!grappleHudIcon) return;
        if (hudUseFillCooldown)
        {
            grappleHudIcon.fillMethod = Image.FillMethod.Radial360;
            grappleHudIcon.type = Image.Type.Filled;
            grappleHudIcon.fillAmount = 1f;
        }
        grappleHudIcon.color = Ready ? hudReadyColor : hudCooldownColor;
    }

    void UpdateHudIcon()
    {
        if (!grappleHudIcon) return;

        if (useCooldown)
        {
            if (hudUseFillCooldown)
                grappleHudIcon.fillAmount = Ready ? 1f : (1f - Cooldown01);

            grappleHudIcon.color = Ready ? hudReadyColor : hudCooldownColor;
        }
        else
        {
            grappleHudIcon.fillAmount = 1f;
            grappleHudIcon.color = hudReadyColor;
        }
    }

    // Devuelve si hay objetivo válido y actualiza retícula/prompt
    bool UpdateAimUI()
    {
        if (showPromptOnlyWhenIdle && state != State.Idle)
        {
            SetPromptVisible(false);
        }

        bool aimValid = false;
        bool readyNow = Ready;

        if (!firePoint)
        {
            SetReticle(reticleNoTargetColor, false);
            SetPromptVisible(false);
            return false;
        }

        Vector3 origin = ComputeOrigin();
        Vector3 dir = ComputeForward().normalized;

        bool hasHit = Physics.SphereCast(
            origin, castRadius, dir,
            out RaycastHit hit, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore
        );

        if (hasHit)
        {
            if (!IsOwnCollider(hit.collider) && !(ignoreAnchorDuringCast && IsAnchorCollider(hit.collider)))
            {
                float d = Vector3.Distance(transform.position, hit.point);
                aimValid = (d >= minAnchorDistance);
            }
        }

        if (reticleIcon)
        {
            if (!aimValid) SetReticle(reticleNoTargetColor, false);
            else           SetReticle(readyNow ? reticleReadyColor : reticleCooldownColor, true);
        }

        if (reticleKeyHint)
        {
            reticleKeyHint.text = reticleKeyGlyph;
            reticleKeyHint.gameObject.SetActive(aimValid && readyNow && state == State.Idle);
        }

        if (promptText)
            SetPromptVisible(aimValid && (state == State.Idle));

        return aimValid;
    }

    void SetReticle(Color c, bool on)
    {
        reticleIcon.enabled = on;
        reticleIcon.color = c;
    }

    void SetPromptVisible(bool on)
    {
        promptVisible = on;
        promptTargetAlpha = on ? 1f : 0f;
    }

    void TickPromptFade()
    {
        if (!promptText) return;

        Color c = promptText.color;
        float a = Mathf.MoveTowards(c.a, promptTargetAlpha, promptFadeSpeed * Time.deltaTime);
        promptText.color = new Color(promptBaseColor.r, promptBaseColor.g, promptBaseColor.b, a);
    }

    void SetPromptAlphaImmediate(float a)
    {
        if (!promptText) return;
        var col = promptText.color;
        promptText.color = new Color(col.r, col.g, col.b, a);
    }

    // ==================== AUDIO HELPERS ====================
    void PlayOneShotSafe(AudioClip clip, float vol = 1f)
    {
        if (!clip || sfxOneShot == null) return;
        sfxOneShot.PlayOneShot(clip, vol);
    }

    void PlayPullStartIfNeeded()
    {
        if (!sfxPullSource || (sfxPullSource.clip == null && sfxGrapplePull == null)) return;
        if (sfxPullSource.clip == null) sfxPullSource.clip = sfxGrapplePull;
        sfxPullSource.Stop();
        sfxPullSource.time = 0f;
        sfxPullSource.Play();
    }

    void StopPullNow()
    {
        if (sfxPullSource && sfxPullSource.isPlaying)
            sfxPullSource.Stop();
    }
}