using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecurityCamTour : MonoBehaviour
{
    [Header("Cámara a usar (tu Main Camera)")]
    public Camera cam;

    [Tooltip("Si lo dejás vacío, se mueve cam.transform. Si no hay cam, se mueve este transform.")]
    public Transform rigToMove;

    [Header("Puntos de vista (Transforms en la escena)")]
    public List<Transform> puntos = new List<Transform>();

    [Header("Timing")]
    [Tooltip("Tiempo que se queda en cada punto (paneando).")]
    public float holdSeconds = 1.2f;

    [Tooltip("ON = funciona aunque Time.timeScale sea 0 (resultados). Recomendado ON.")]
    public bool useUnscaledTime = true;

    [Header("Paneo (centro -> derecha -> izquierda -> derecha...)")]
    public bool panear = true;
    public float panAngulo = 12f;

    [Tooltip("Ciclos por segundo. 0.35 suele quedar bien.")]
    public float panVelocidadCiclos = 0.35f;

    [Tooltip("Suaviza el paneo para que no tiemble.")]
    public float panSmoothTime = 0.12f;

    [Tooltip("Al terminar el hold, vuelve suave al centro (yaw=0) para no dejarlo torcido.")]
    public bool panReturnToCenter = true;
    public float panReturnDur = 0.12f;

    [Header("Activación")]
    public bool activoAlInicio = false;

    // ===================== HIDE FP / FX =====================
    [Header("Ocultar durante el tour (opcional)")]
    [Tooltip("Objetos a apagar mientras está activo (brazos, arma, speedlines, etc).")]
    public GameObject[] objectsToHide;

    [Tooltip("Partículas a detener/limpiar mientras está activo (speedlines, etc).")]
    public ParticleSystem[] particlesToStop;

    public bool clearParticlesOnStop = true;

    [Tooltip("Layers a ocultar del render (mejor método si tenés arms/weapon en un layer).")]
    public LayerMask layersToHideWhileActive;

    [Tooltip("Si está ON, aplica el culling mask modificado al activar.")]
    public bool useCullingMaskHide = false;

    // ===================== runtime =====================
    Coroutine _co;
    bool _active;

    // pan runtime
    float _panYaw;
    float _panYawVel;

    // restore camera mask
    int _camMaskOriginal;
    bool _camMaskCached;

    struct ObjState { public GameObject go; public bool wasActive; }
    readonly List<ObjState> _objStates = new List<ObjState>();

    struct PsState { public ParticleSystem ps; public bool wasPlaying; }
    readonly List<PsState> _psStates = new List<PsState>();

    Transform Rig
    {
        get
        {
            if (rigToMove) return rigToMove;
            if (cam) return cam.transform;
            return transform;
        }
    }

    float DT => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Awake()
    {
        if (!cam && Camera.main) cam = Camera.main;
        if (activoAlInicio) Activar(true);
    }

    public void Activar(bool on)
    {
        _active = on;

        if (!cam && Camera.main) cam = Camera.main;

        if (!on)
        {
            if (_co != null) StopCoroutine(_co);
            _co = null;

            RestoreHiddenThings();
            return;
        }

        ApplyHideThings();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RunTour());
    }

    IEnumerator RunTour()
    {
        if (puntos == null || puntos.Count == 0)
            yield break;

        int idx = 0;

        // Hard cut inicial al primer punto
        SnapToPoint(puntos[idx]);

        while (_active)
        {
            // Hold + pan en el punto actual
            yield return HoldAndPan(puntos[idx], holdSeconds);

            // Siguiente punto (hard cut)
            idx = (idx + 1) % puntos.Count;
            SnapToPoint(puntos[idx]);

            yield return null;
        }
    }

    void SnapToPoint(Transform p)
    {
        if (!p) return;

        Rig.position = p.position;
        Rig.rotation = p.rotation;

        // Reset pan para que SIEMPRE arranque centrado y vaya a la derecha
        _panYaw = 0f;
        _panYawVel = 0f;
    }

    IEnumerator HoldAndPan(Transform p, float seconds)
    {
        seconds = Mathf.Max(0.01f, seconds);

        if (!p)
        {
            yield return Wait(seconds);
            yield break;
        }

        Quaternion baseRot = p.rotation;

        float t = 0f;
        float localPanTime = 0f;

        // arrancar centrado
        _panYaw = 0f;
        _panYawVel = 0f;

        while (t < seconds && _active)
        {
            t += DT;

            if (panear)
            {
                localPanTime += DT;

                // sin(0)=0 y sube => primero va a la derecha
                float omega = Mathf.Max(0f, panVelocidadCiclos) * Mathf.PI * 2f;
                float targetYaw = Mathf.Sin(localPanTime * omega) * panAngulo;

                _panYaw = Mathf.SmoothDampAngle(
                    _panYaw, targetYaw,
                    ref _panYawVel,
                    Mathf.Max(0.01f, panSmoothTime),
                    Mathf.Infinity,
                    DT
                );

                Rig.rotation = baseRot * Quaternion.Euler(0f, _panYaw, 0f);
            }
            else
            {
                Rig.rotation = baseRot;
            }

            yield return null;
        }

        if (!_active) yield break;

        if (panear && panReturnToCenter)
        {
            float startYaw = _panYaw;
            float tt = 0f;

            while (tt < 1f && _active)
            {
                tt += DT / Mathf.Max(0.01f, panReturnDur);
                float x = Mathf.Clamp01(tt);

                float yaw = Mathf.Lerp(startYaw, 0f, x);
                Rig.rotation = baseRot * Quaternion.Euler(0f, yaw, 0f);

                yield return null;
            }

            Rig.rotation = baseRot;
        }
        else
        {
            Rig.rotation = baseRot;
        }

        _panYaw = 0f;
        _panYawVel = 0f;
    }

    IEnumerator Wait(float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);
        while (t < seconds && _active)
        {
            t += DT;
            yield return null;
        }
    }

    // ===================== Hide / Restore =====================

    void ApplyHideThings()
    {
        // Camera culling mask
        if (useCullingMaskHide && cam)
        {
            if (!_camMaskCached)
            {
                _camMaskOriginal = cam.cullingMask;
                _camMaskCached = true;
            }

            int hideMask = layersToHideWhileActive.value;
            cam.cullingMask = _camMaskOriginal & ~hideMask;
        }

        // Objects to hide
        _objStates.Clear();
        if (objectsToHide != null)
        {
            for (int i = 0; i < objectsToHide.Length; i++)
            {
                var go = objectsToHide[i];
                if (!go) continue;

                _objStates.Add(new ObjState { go = go, wasActive = go.activeSelf });
                go.SetActive(false);
            }
        }

        // Particles to stop
        _psStates.Clear();
        if (particlesToStop != null)
        {
            for (int i = 0; i < particlesToStop.Length; i++)
            {
                var ps = particlesToStop[i];
                if (!ps) continue;

                _psStates.Add(new PsState { ps = ps, wasPlaying = ps.isPlaying });

                if (clearParticlesOnStop)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    void RestoreHiddenThings()
    {
        // Restore camera mask
        if (useCullingMaskHide && cam && _camMaskCached)
        {
            cam.cullingMask = _camMaskOriginal;
        }

        // Restore objects
        for (int i = 0; i < _objStates.Count; i++)
        {
            var s = _objStates[i];
            if (s.go) s.go.SetActive(s.wasActive);
        }
        _objStates.Clear();

        // Restore particles (solo si estaban playing)
        for (int i = 0; i < _psStates.Count; i++)
        {
            var s = _psStates[i];
            if (!s.ps) continue;

            if (s.wasPlaying)
                s.ps.Play(true);
        }
        _psStates.Clear();
    }
}
