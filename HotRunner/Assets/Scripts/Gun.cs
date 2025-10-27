using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Gun : MonoBehaviour
{
    [Header("Input")]
    public KeyCode aimKey  = KeyCode.Mouse1; // mantener para equipar/desenfundar
    public KeyCode fireKey = KeyCode.Mouse0; // presionar mientras se mantiene aimKey

    [Header("Refs (overlay)")]
    [Tooltip("Nodo raíz del arma bajo la GunCamera (no la Cámara principal).")]
    public Transform weaponRoot; 
    [Tooltip("Posición local guardada (fundado).")]
    public Vector3 holsterLocalPos = new Vector3(0.2f, -0.35f, -0.5f);
    public Vector3 holsterLocalEuler = new Vector3(12f, 25f, 0f);
    [Tooltip("Posición local apuntando (en pantalla).")]
    public Vector3 aimLocalPos = new Vector3(0.1f, -0.12f, 0.2f);
    public Vector3 aimLocalEuler = new Vector3(0f, 0f, 0f);
    [Tooltip("Suavizado de transición fundado <-> apuntando.")]
    public float equipLerp = 12f;

    [Header("Disparo (proyectil)")]
    [Tooltip("Punto de salida del proyectil (boca del arma).")]
    public Transform muzzle;
    [Tooltip("Prefab de proyectil con Rigidbody (el TrailRenderer lo agregamos/ajustamos por código).")]
    public Rigidbody bulletPrefab;
    public float bulletSpeed = 120f;
    public float bulletLife = 3f;

    [Header("Muzzle VFX")]
    [Tooltip("Prefab del VFX (ParticleSystem o GameObject) para la boca del arma.")]
    public GameObject muzzleVfxPrefab;
    [Tooltip("Si es true, el VFX quedará como hijo del muzzle (útil para armas en primera persona).")]
    public bool parentMuzzleVfx = true;
    [Tooltip("Tiempo de vida forzado si el prefab no tiene ParticleSystem (0 = auto).")]
    public float muzzleVfxLife = 0.6f;

    [Header("Tracer (Trail)")]
    [Tooltip("Material para el Trail (unlit recomendado).")]
    public Material tracerMaterial;
    [Tooltip("Duración visible del rastro (seg).")]
    public float tracerTime = 0.18f;
    [Tooltip("Distancia mínima entre vértices del trail (más chico = más suave).")]
    public float tracerMinVertexDistance = 0.04f;
    [Tooltip("Curva de ancho (0..1). 0=>inicio del trail, 1=>cola del trail.")]
    public AnimationCurve tracerWidth = AnimationCurve.EaseInOut(0, 0.16f, 1, 0.0f);
    [Tooltip("Gradiente de color/alfa para el tracer.")]
    public Gradient tracerColor;

    [Header("Retroceso (arma)")]
    public Vector3 recoilKickPos = new Vector3(0f, 0.02f, -0.08f); // desplazamiento local
    public Vector3 recoilKickEuler = new Vector3(-6f, 0f, 0f);      // rotación local (pitch)
    public float recoilUpTime = 0.04f;
    public float recoilRelaxTime = 0.22f;

    [Header("Enfriamiento")]
    public float shotCooldown = 5f;   // segundos
    public Image  hudIcon;            // ícono en UI
    public Color  hudReadyColor = Color.white;
    public Color  hudCooldownColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    public bool   hudUseFillCooldown = true;

    [Header("Audio (opcional)")]
    public AudioSource sfxOneShot;
    public AudioClip sfxDraw;
    public AudioClip sfxHolster;
    public AudioClip sfxFire;
    public AudioClip sfxDenied; // click si está en cooldown

    [Header("Camera Kick")]
    [Tooltip("Asigna la Main Camera con CameraKick.cs")]
    public CameraKick camKick;
    public Vector3 camKickOffset = new Vector3(0.02f, 0.012f, -0.12f);
    public float camKickRollDeg = 2.2f;
    public float camKickFovPunch = 2.4f;
    public float camKickUpTime = 0.03f;
    public float camKickRelax = 0.22f;

    // ---- runtime ----
    Vector3 targetPos;
    Quaternion targetRot;
    Vector3 recoilVelPos;
    Vector3 recoilVelEuler;
    Vector3 curLocalEuler;

    float nextShotReadyTime = 0f;
    bool Ready => Time.time >= nextShotReadyTime;

    void Reset()
    {
        equipLerp = 12f;
        bulletSpeed = 120f;
        bulletLife = 3f;
        shotCooldown = 5f;

        // Tracer defaults (colores de “bala luminosa”)
        if (tracerColor == null || tracerColor.colorKeys.Length == 0)
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

        // arranca fundado
        weaponRoot.localPosition = holsterLocalPos;
        weaponRoot.localRotation = Quaternion.Euler(holsterLocalEuler);
        curLocalEuler = holsterLocalEuler;
        targetPos = holsterLocalPos;
        targetRot = Quaternion.Euler(holsterLocalEuler);

        SetupHudIcon();
    }

    void Update()
    {
        bool aimHeld = Input.GetKey(aimKey);
        bool fireDown = Input.GetKeyDown(fireKey);

        // ----- Equip / Holster -----
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

        // Lerp suave entre poses
        weaponRoot.localPosition = Vector3.Lerp(
            weaponRoot.localPosition, targetPos, equipLerp * Time.deltaTime);
        weaponRoot.localRotation = Quaternion.Slerp(
            weaponRoot.localRotation, targetRot, equipLerp * Time.deltaTime);

        // ----- Disparo -----
        if (aimHeld && fireDown)
        {
            if (!Ready)
            {
                if (sfxDenied) PlayOnce(sfxDenied, 1f);
            }
            else
            {
                ShootNow();
            }
        }

        // Retroceso (relajación suave después del disparo)
        weaponRoot.localPosition = Vector3.SmoothDamp(
            weaponRoot.localPosition, targetPos, ref recoilVelPos, recoilRelaxTime);
        Vector3 eul = Vector3.SmoothDamp(
            curLocalEuler, targetRot.eulerAngles, ref recoilVelEuler, recoilRelaxTime);
        weaponRoot.localRotation = Quaternion.Euler(eul);
        curLocalEuler = eul;

        UpdateHudIcon();
    }

    void ShootNow()
    {
        // MUZZLE VFX
        if (muzzle && muzzleVfxPrefab)
        {
            var vfx = Instantiate(muzzleVfxPrefab, muzzle.position, muzzle.rotation,
                                  parentMuzzleVfx ? muzzle : null);
            float life = muzzleVfxLife;
            var ps = vfx.GetComponent<ParticleSystem>();
            if (ps)
            {
                var m = ps.main;
                life = Mathf.Max(life, m.duration + m.startLifetime.constantMax);
            }
            if (life <= 0f) life = 0.6f;
            Destroy(vfx, life);
        }

        // PROYECTIL + TRACER
        if (muzzle && bulletPrefab)
        {
            var rb = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
            rb.velocity = muzzle.forward * bulletSpeed;

            // Configurar/crear el tracer por código
            var proj = rb.GetComponent<BulletProjectile>();
            if (!proj) proj = rb.gameObject.AddComponent<BulletProjectile>();

            proj.life = bulletLife;
            proj.trailTime = tracerTime;
            proj.minVertexDistance = tracerMinVertexDistance;
            proj.trailWidth = tracerWidth;
            proj.trailColor = tracerColor;
            proj.trailMaterial = tracerMaterial; // opcional; si es null usa material por defecto

            // (Si querés simular penetración/impacto, podés setear capas y OnCollisionEnter en BulletProjectile)
        }

        // Audio
        if (sfxFire) PlayOnce(sfxFire, 1f);

        // Retroceso (arma)
        weaponRoot.localPosition += recoilKickPos;
        curLocalEuler += recoilKickEuler;
        CancelInvoke(nameof(ResetRecoilImmediate));
        Invoke(nameof(ResetRecoilImmediate), Mathf.Max(0.01f, recoilUpTime));

        // Camera kick (potente)
        if (camKick)
        {
            camKick.KickAndShake(
                camKickOffset,
                camKickRollDeg,
                camKickFovPunch,
                camKickUpTime,
                camKickRelax
            );
        }

        // Cooldown
        nextShotReadyTime = Time.time + Mathf.Max(0.01f, shotCooldown);
    }

    void ResetRecoilImmediate() { /* SmoothDamp hace el resto */ }

    // -------- HUD cooldown --------
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

    // -------- utility --------
    void PlayOnce(AudioClip clip, float vol)
    {
        if (!clip || sfxOneShot == null) return;
        sfxOneShot.PlayOneShot(clip, vol);
    }
}