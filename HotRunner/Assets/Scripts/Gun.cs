using System.Collections;
using System.Collections.Generic;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.VFX;
#endif
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Gun : MonoBehaviour
{
    [Header("Input")]
    public KeyCode aimKey  = KeyCode.Mouse1;
    public KeyCode fireKey = KeyCode.Mouse0;

    [Header("Refs (overlay)")]
    public Transform weaponRoot;
    public Vector3 holsterLocalPos   = new Vector3(0.2f, -0.35f, -0.5f);
    public Vector3 holsterLocalEuler = new Vector3(12f, 25f, 0f);
    public Vector3 aimLocalPos       = new Vector3(0.1f, -0.12f, 0.2f);
    public Vector3 aimLocalEuler     = new Vector3(0f, 0f, 0f);
    public float   equipLerp         = 12f;

    [Header("Disparo (proyectil)")]
    public Transform  muzzle;
    public Rigidbody  bulletPrefab;       // opcional
    public float      bulletSpeed   = 120f;
    public float      bulletLife    = 3f;

    [Header("Muzzle VFX")]
    public GameObject muzzleVfxPrefab;
    public bool   parentMuzzleVfx   = true;
    public float  muzzleVfxLife     = 0.6f;

    [Header("Muzzle VFX - Avanzado")]
    public Camera gunCamera;
    public bool   muzzleVfxWorldSpace = false; // LOCAL por defecto
    public bool   muzzleVfxTestAtOrigin = false;

    [Header("Aim / Hitscan")]
    public float     maxRange = 300f;
    public LayerMask hitMask = ~0;

    [Header("Tracer (Trail)")]
    public Material tracerMaterial;              // NO usar el mismo que el fogonazo
    public float tracerTime = 0.18f;
    public float tracerMinVertexDistance = 0.04f;
    public AnimationCurve tracerWidth = AnimationCurve.EaseInOut(0, 0.16f, 1, 0.0f);
    public Gradient tracerColor;
    public bool  forceImmediateTracer = true;
    public float tracerVisualSpeed    = 300f;
    public int   tracerCornerVerts    = 8;
    public int   tracerCapVerts       = 8;

    [Tooltip("Renderizar el tracer en una capa del mundo (ej. Default) para que lo vea la Main Camera.")]
    public bool tracerOnWorldLayer = true;
    [Tooltip("Índice de Layer para el tracer (0 = Default).")]
    public int tracerLayer = 0;

    [Header("Impacto (opcional)")]
    public GameObject impactVfxPrefab;
    public float      impactVfxLife = 1.0f;

    // >>> NUEVO: daño / destrucción seguro <<<
    [Header("Daño / Destrucción")]
    public float damage = 25f;
    public bool  destroyTaggedBreakables = false;
    public string breakableTag = "Breakable";
    public float impactImpulse = 30f;

    [Header("Retroceso (arma)")]
    public Vector3 recoilKickPos   = new Vector3(0f, 0.02f, -0.08f);
    public Vector3 recoilKickEuler = new Vector3(-6f, 0f, 0f);
    public float   recoilUpTime    = 0.04f;
    public float   recoilRelaxTime = 0.22f;

    [Header("Enfriamiento")]
    public float  shotCooldown = 5f;
    public Image  hudIcon;
    public Color  hudReadyColor    = Color.white;
    public Color  hudCooldownColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    public bool   hudUseFillCooldown = true;

    [Header("Audio (opcional)")]
    public AudioSource sfxOneShot;
    public AudioClip sfxDraw;
    public AudioClip sfxHolster;
    public AudioClip sfxFire;
    public AudioClip sfxDenied;

    [Header("Camera Kick")]
    public CameraKick camKick;
    public Vector3 camKickOffset = new Vector3(0.02f, 0.012f, -0.12f);
    public float  camKickRollDeg  = 2.2f;
    public float  camKickFovPunch = 2.4f;
    public float  camKickUpTime   = 0.03f;
    public float  camKickRelax    = 0.22f;

    // ---- runtime ----
    Vector3   targetPos;
    Quaternion targetRot;
    Vector3   recoilVelPos, recoilVelEuler, curLocalEuler;
    float     nextShotReadyTime = 0f;
    bool Ready => Time.time >= nextShotReadyTime;

    void Reset()
    {
        if (tracerColor == null || tracerColor.colorKeys == null || tracerColor.colorKeys.Length == 0)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f,0.75f,0.35f), 0.12f),
                    new GradientColorKey(new Color(0.6f,0.8f,1f), 0.6f),
                    new GradientColorKey(new Color(0.2f,0.4f,1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.12f),
                    new GradientAlphaKey(0.35f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            tracerColor = g;
        }
    }

    void Awake()
    {
        if (!weaponRoot) weaponRoot = transform;
        weaponRoot.localPosition = holsterLocalPos;
        weaponRoot.localRotation = Quaternion.Euler(holsterLocalEuler);
        curLocalEuler = holsterLocalEuler;
        targetPos = holsterLocalPos;
        targetRot = Quaternion.Euler(holsterLocalEuler);

        if (!gunCamera)
        {
            gunCamera = GetComponentInChildren<Camera>(true);
            if (!gunCamera && Camera.main) gunCamera = Camera.main;
        }

        SetupHudIcon();
    }

    void Update()
    {
        bool aimHeld  = Input.GetKey(aimKey);
        bool fireDown = Input.GetKeyDown(fireKey);

        // Equip/Holster
        if (aimHeld)
        {
            bool wasHolstered = targetPos == holsterLocalPos;
            targetPos = aimLocalPos;
            targetRot = Quaternion.Euler(aimLocalEuler);
            if (wasHolstered && sfxDraw) PlayOnce(sfxDraw, 0.9f);
        }
        else
        {
            bool wasAiming = targetPos == aimLocalPos;
            targetPos = holsterLocalPos;
            targetRot = Quaternion.Euler(holsterLocalEuler);
            if (wasAiming && sfxHolster) PlayOnce(sfxHolster, 0.9f);
        }

        // Lerp entre poses
        weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, targetPos, equipLerp * Time.deltaTime);
        weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, targetRot, equipLerp * Time.deltaTime);

        // Disparo
        if (aimHeld && fireDown)
        {
            if (!Ready) { if (sfxDenied) PlayOnce(sfxDenied, 1f); }
            else ShootNow();
        }

        // Relajación de retroceso
        weaponRoot.localPosition = Vector3.SmoothDamp(weaponRoot.localPosition, targetPos, ref recoilVelPos, recoilRelaxTime);
        Vector3 eul = Vector3.SmoothDamp(curLocalEuler, targetRot.eulerAngles, ref recoilVelEuler, recoilRelaxTime);
        weaponRoot.localRotation = Quaternion.Euler(eul);
        curLocalEuler = eul;

        UpdateHudIcon();
    }

    void ShootNow()
    {
        // 1) Fogonazo
        SpawnMuzzleVFX();

        // 2) Aim al centro de pantalla
        Vector3 rayOrigin, rayDir;
        GetAimRay(out rayOrigin, out rayDir);

        // 3) Raycast
        Vector3 start = muzzle ? muzzle.position : transform.position;
        Vector3 end   = start + rayDir * maxRange;
        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;

            // Impact VFX
            if (impactVfxPrefab)
            {
                var ivfx = Instantiate(impactVfxPrefab, end, Quaternion.LookRotation(hit.normal));
                SetLayerRecursively(ivfx, tracerOnWorldLayer ? tracerLayer : gameObject.layer);
                Destroy(ivfx, Mathf.Max(0.05f, impactVfxLife));
            }

            // Impulso físico
            if (hit.rigidbody)
                hit.rigidbody.AddForceAtPosition(rayDir * impactImpulse, hit.point, ForceMode.Impulse);

            // >>> APLICAR DAÑO (robusto, sin SendMessage y sin depender de sobrecargas) <<<
            var hitGO = hit.rigidbody ? hit.rigidbody.gameObject : hit.collider.gameObject;
            bool applied = TryApplyDamageOn(hitGO, damage, hit.point, rayDir);

            // Fallback: destruir por Tag opcional
            if (!applied && destroyTaggedBreakables && hitGO.CompareTag(breakableTag))
            {
                Destroy(hitGO);
            }
        }

        // 4) Proyectil físico (opcional) + tracer
        if (bulletPrefab && muzzle)
        {
            Vector3 dirToEnd = (end - muzzle.position).normalized;
            var rb = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(dirToEnd));
            rb.velocity = dirToEnd * bulletSpeed;
            Destroy(rb.gameObject, Mathf.Max(0.05f, bulletLife));
            if (forceImmediateTracer) StartCoroutine(SpawnLineTracer(start, end));
        }
        else
        {
            StartCoroutine(SpawnLineTracer(start, end));
        }

        // 5) Audio
        if (sfxFire) PlayOnce(sfxFire, 1f);

        // 6) Retroceso arma
        weaponRoot.localPosition += recoilKickPos;
        curLocalEuler += recoilKickEuler;
        CancelInvoke(nameof(ResetRecoilImmediate));
        Invoke(nameof(ResetRecoilImmediate), Mathf.Max(0.01f, recoilUpTime));

        // 7) Camera kick
        if (camKick)
            camKick.KickAndShake(camKickOffset, camKickRollDeg, camKickFovPunch, camKickUpTime, camKickRelax);

        // 8) Cooldown
        nextShotReadyTime = Time.time + Mathf.Max(0.01f, shotCooldown);
    }

    void ResetRecoilImmediate() { }

    // === Tracer animado (TrailRenderer) ===
    System.Collections.IEnumerator SpawnLineTracer(Vector3 start, Vector3 end)
    {
        GameObject go = new GameObject("TracerTemp");
        go.transform.position = start;
        go.layer = tracerOnWorldLayer ? tracerLayer : gameObject.layer;

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = Mathf.Max(0.02f, tracerTime);
        tr.minVertexDistance = Mathf.Max(0.001f, tracerMinVertexDistance);
        tr.widthCurve = tracerWidth;
        tr.colorGradient = tracerColor;
        tr.numCornerVertices = Mathf.Max(0, tracerCornerVerts);
        tr.numCapVertices = Mathf.Max(0, tracerCapVerts);
        tr.alignment = LineAlignment.View;

        if (tracerMaterial)
        {
            tr.material = tracerMaterial;
        }
        else
        {
            var sh = Shader.Find("Particles/Standard Unlit");
            tr.material = new Material(sh) { renderQueue = 3000 };
            tr.material.SetFloat("_Mode", 2f);
            tr.material.EnableKeyword("_ALPHABLEND_ON");
        }

        var rend = tr as Renderer;
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        float dist = Vector3.Distance(start, end);
        float dur = Mathf.Max(0.02f, dist / Mathf.Max(1f, tracerVisualSpeed));
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            go.transform.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        go.transform.position = end;

        yield return new WaitForSeconds(tr.time);
        Destroy(go);
    }

    // === Fogonazo / Muzzle ===
    void SpawnMuzzleVFX()
    {
        if (!muzzleVfxPrefab) return;

        if (muzzleVfxTestAtOrigin)
        {
            var test = Instantiate(muzzleVfxPrefab, Vector3.zero, Quaternion.identity, null);
            SetLayerRecursively(test, gameObject.layer);
            ForceParticlePlayable(test, muzzleVfxWorldSpace);
            Destroy(test, Mathf.Max(0.1f, muzzleVfxLife));
            return;
        }

        var parent = (parentMuzzleVfx && muzzle) ? muzzle : null;
        var vfx = Instantiate(
            muzzleVfxPrefab,
            muzzle ? muzzle.position : transform.position,
            muzzle ? muzzle.rotation : transform.rotation,
            parent
        );

        if (parent != null)
        {
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localRotation = Quaternion.identity;
            vfx.transform.localScale    = Vector3.one;
        }

        SetLayerRecursively(vfx, gameObject.layer);

        var allPs = vfx.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allPs)
        {
            var main = ps.main;
            main.simulationSpace = muzzleVfxWorldSpace
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
            ps.Clear(true);
            ps.Play(true);
        }

    #if UNITY_2019_3_OR_NEWER
        var vfxGraph = vfx.GetComponent<VisualEffect>();
        if (vfxGraph) vfxGraph.Play();
    #endif

        float life = Mathf.Max(0.05f, muzzleVfxLife);
        foreach (var ps in allPs)
        {
            var main = ps.main;
    #if UNITY_2022_1_OR_NEWER
            float lt = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                       ? main.startLifetime.constantMax
                       : main.startLifetime.constant;
    #else
            float lt = main.startLifetime.constantMax;
    #endif
            life = Mathf.Max(life, main.duration + lt);
        }

        if (gunCamera)
        {
            int layer = vfx.layer;
            bool cameraSeesLayer = (gunCamera.cullingMask & (1 << layer)) != 0;
            if (!cameraSeesLayer)
                Debug.LogWarning($"[Gun] La cámara '{gunCamera.name}' no ve la Layer '{LayerMask.LayerToName(layer)}' del VFX. Ajustá el Culling Mask.");
        }

        Destroy(vfx, life);
    }

    // === Aim Ray ===
    void GetAimRay(out Vector3 origin, out Vector3 dir)
    {
        if (gunCamera)
        {
            origin = gunCamera.transform.position;
            dir = gunCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction;
        }
        else if (muzzle)
        {
            origin = muzzle.position;
            dir    = muzzle.forward;
        }
        else
        {
            origin = transform.position;
            dir    = transform.forward;
        }
    }

    // === HUD ===
    void SetupHudIcon()
    {
        if (!hudIcon) return;
        if (hudUseFillCooldown)
        {
            hudIcon.type = Image.Type.Filled;
            hudIcon.fillMethod = Image.FillMethod.Radial360;
            hudIcon.fillAmount = 1f;
        }
        hudIcon.color = Ready ? hudReadyColor : hudCooldownColor;
    }

    void UpdateHudIcon()
    {
        if (!hudIcon) return;
        if (Ready)
        {
            hudIcon.color = hudReadyColor;
            if (hudUseFillCooldown) hudIcon.fillAmount = 1f;
        }
        else
        {
            hudIcon.color = hudCooldownColor;
            if (hudUseFillCooldown)
            {
                float t = Mathf.InverseLerp(nextShotReadyTime - shotCooldown, nextShotReadyTime, Time.time);
                hudIcon.fillAmount = Mathf.Clamp01(t);
            }
        }
    }

    // === Utils ===
    void PlayOnce(AudioClip clip, float vol)
    {
        if (!clip || sfxOneShot == null) return;
        sfxOneShot.PlayOneShot(clip, vol);
    }

    void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    void ForceParticlePlayable(GameObject root, bool worldSpace)
    {
        var psAll = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psAll)
        {
            var main = ps.main;
            main.simulationSpace = worldSpace
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
            ps.Clear(true);
            ps.Play(true);
        }

    #if UNITY_2019_3_OR_NEWER
        var vfxGraph = root.GetComponent<VisualEffect>();
        if (vfxGraph) vfxGraph.Play();
    #endif
    }

    // --------- DAÑO por reflexión (robusto a diferentes firmas) ---------
    bool TryApplyDamageOn(GameObject go, float amount, Vector3 hitPoint, Vector3 hitDir)
    {
        if (!go) return false;

        // Recorremos TODOS los componentes y buscamos un método "ApplyDamage"
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            Type tp = c.GetType();

            // 1) (float, Vector3, Vector3)
            var m = tp.GetMethod("ApplyDamage", new Type[] { typeof(float), typeof(Vector3), typeof(Vector3) });
            if (m != null) { m.Invoke(c, new object[] { amount, hitPoint, hitDir }); return true; }

            // 2) (float, Vector3)
            m = tp.GetMethod("ApplyDamage", new Type[] { typeof(float), typeof(Vector3) });
            if (m != null) { m.Invoke(c, new object[] { amount, hitPoint }); return true; }

            // 3) (float)
            m = tp.GetMethod("ApplyDamage", new Type[] { typeof(float) });
            if (m != null) { m.Invoke(c, new object[] { amount }); return true; }

            // 4) ()
            m = tp.GetMethod("ApplyDamage", Type.EmptyTypes);
            if (m != null) { m.Invoke(c, null); return true; }
        }
        return false;
    }
}