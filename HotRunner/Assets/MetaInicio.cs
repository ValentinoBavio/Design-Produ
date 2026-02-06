using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MetaInicio : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ControladorJuego controladorJuego;

    [Header("Tiempo al activar (segundos)")]
    [Tooltip("Si lo dejás en 60, queda igual que ahora. Para otros valores, ver nota abajo.")]
    [SerializeField] private float tiempoAlActivar = 60f;

    [Header("Audio (respeta AudioMixer)")]
    [SerializeField] private AudioClip sfx;
    [SerializeField] [Range(0f, 1f)] private float volumen = 0.9f;
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("Opcional")]
    [SerializeField] private bool destruirAlTocar = true;

    // ========================= Giro Tic-Tac =========================
    [Header("Giro Tic-Tac (tuerca/gear)")]
    [Tooltip("Arrastrá acá el Transform que querés que gire (la tuerca/gear). NO el holograma.")]
    [SerializeField] private Transform piezaQueGira;

    [Tooltip("Segundos por tick (ej 1 = reloj).")]
    [SerializeField] private float tickInterval = 1.0f;

    [Tooltip("Grados por tick. 6 = segundero clásico.")]
    [SerializeField] private float gradosPorTick = 6f;

    [Tooltip("Duración del movimiento del tick principal (más chico = más 'clack').")]
    [SerializeField] private float duracionSnap = 0.08f;

    [Tooltip("Eje local de giro. Normalmente Z (0,0,1).")]
    [SerializeField] private Vector3 ejeLocal = new Vector3(0f, 0f, 1f);

    [Tooltip("Horario = sentido de las agujas del reloj.")]
    [SerializeField] private bool sentidoHorario = true;

    [Tooltip("Si está asignado (por ej el holograma hijo del gear), este NO girará aunque su padre gire.")]
    [SerializeField] private Transform hologramaNoGirar;

    // ========================= Rebote tipo disco =========================
    [Header("Rebote / Retroceso (tipo disco teléfono)")]
    [SerializeField] private bool usarRebote = true;

    [Tooltip("Cuánto se pasa del ángulo final antes de volver (grados). Ej: 1 a 3.")]
    [SerializeField] private float overshootGrados = 1.5f;

    [Tooltip("Duración del 'pasarse' (seg).")]
    [SerializeField] private float overshootDur = 0.04f;

    [Tooltip("Duración del regreso/asentado (seg).")]
    [SerializeField] private float settleDur = 0.06f;

    // ========================= SFX TicTac sincronizado por proximidad =========================
    [Header("SFX Tic-Tac (SINCRONIZADO al movimiento)")]
    [Tooltip("Clip del tic-tac (puede durar 1s). Se reproduce 1 vez por tick, sincronizado.")]
    [SerializeField] private AudioClip sfxTicTac;

    [SerializeField] [Range(0f, 1f)] private float volumenTicTac = 0.9f;

    [Tooltip("Distancia para que el objeto 'active' el sonido (lógica).")]
    [SerializeField] private float distanciaProximidad = 6f;

    [Tooltip("Opcional: si lo dejás vacío, busca el Player por tag al primer uso.")]
    [SerializeField] private Transform player;

    [Header("Audio 3D (esto define cuánto se escucha realmente)")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 12f;

    [Tooltip("Si el 'tic' del clip no está exactamente al inicio, ajustá este offset (segundos).")]
    [SerializeField] private float audioOffsetSeconds = 0f;

    // ========================= Flotación =========================
    [Header("Flotación suave")]
    [SerializeField] private bool flotar = true;
    [SerializeField] private float amplitudFlote = 0.08f;
    [SerializeField] private float frecuenciaFlote = 1.2f;

    // ========================= Estado interno =========================
    bool usado = false;

    float _tickTimer = 0f;
    bool _snapping = false;

    Vector3 _startLocalPos;
    Quaternion _holoWorldRotInicial;

    AudioSource _asTicTac;
    bool _playerCerca = false;

    private void Awake()
    {
        _startLocalPos = transform.localPosition;

        if (hologramaNoGirar)
            _holoWorldRotInicial = hologramaNoGirar.rotation;

        // AudioSource 3D (UNO por objeto)
        if (sfxTicTac)
        {
            _asTicTac = gameObject.AddComponent<AudioSource>();
            _asTicTac.playOnAwake = false;
            _asTicTac.loop = false;
            _asTicTac.clip = sfxTicTac;

            _asTicTac.spatialBlend = 1f; // 3D
            _asTicTac.outputAudioMixerGroup = mixerGroup;
            _asTicTac.rolloffMode = AudioRolloffMode.Linear;
            _asTicTac.minDistance = Mathf.Max(0.01f, minDistance);
            _asTicTac.maxDistance = Mathf.Max(_asTicTac.minDistance + 0.01f, maxDistance);
            _asTicTac.dopplerLevel = 0f;
            _asTicTac.volume = volumenTicTac;
        }
    }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void Update()
    {
        if (usado) return;

        // Flotación suave
        if (flotar)
        {
            float y = Mathf.Sin(Time.time * (Mathf.PI * 2f) * frecuenciaFlote) * amplitudFlote;
            transform.localPosition = _startLocalPos + new Vector3(0f, y, 0f);
        }

        // Proximidad (solo lógica)
        UpdateProximity();

        // Giro por ticks
        if (piezaQueGira && tickInterval > 0.001f && !_snapping)
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickInterval)
            {
                _tickTimer = 0f;
                StartCoroutine(CoTickSnap());
            }
        }

        // Bloquear rotación del holograma si es hijo del gear
        if (hologramaNoGirar)
            hologramaNoGirar.rotation = _holoWorldRotInicial;
    }

    private void UpdateProximity()
    {
        if (!sfxTicTac) { _playerCerca = false; return; }

        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }
        if (!player) { _playerCerca = false; return; }

        float d = Vector3.Distance(player.position, transform.position);
        _playerCerca = (d <= distanciaProximidad);
    }

    private IEnumerator CoTickSnap()
    {
        _snapping = true;

        // ✅ Audio sincronizado: se dispara EXACTAMENTE al iniciar el tick del movimiento
        PlayTicTacSynced();

        Vector3 axis = ejeLocal.sqrMagnitude < 0.0001f ? Vector3.forward : ejeLocal.normalized;
        float sign = sentidoHorario ? -1f : 1f;
        float ang = gradosPorTick * sign;

        Quaternion from = piezaQueGira.localRotation;
        Quaternion target = from * Quaternion.AngleAxis(ang, axis);

        if (!usarRebote || Mathf.Abs(overshootGrados) < 0.0001f)
        {
            float t = 0f;
            float dur = Mathf.Max(0.001f, duracionSnap);

            while (t < dur)
            {
                t += Time.deltaTime;
                piezaQueGira.localRotation = Quaternion.Slerp(from, target, Mathf.Clamp01(t / dur));
                yield return null;
            }

            piezaQueGira.localRotation = target;
            _snapping = false;
            yield break;
        }

        Quaternion overshoot = from * Quaternion.AngleAxis(ang + (overshootGrados * sign), axis);

        // 1) ir al overshoot
        {
            float t = 0f;
            float dur = Mathf.Max(0.001f, overshootDur);
            while (t < dur)
            {
                t += Time.deltaTime;
                piezaQueGira.localRotation = Quaternion.Slerp(from, overshoot, Mathf.Clamp01(t / dur));
                yield return null;
            }
            piezaQueGira.localRotation = overshoot;
        }

        // 2) volver/asentar al target
        {
            float t = 0f;
            float dur = Mathf.Max(0.001f, settleDur);
            while (t < dur)
            {
                t += Time.deltaTime;
                piezaQueGira.localRotation = Quaternion.Slerp(overshoot, target, Mathf.Clamp01(t / dur));
                yield return null;
            }
            piezaQueGira.localRotation = target;
        }

        _snapping = false;
    }

    private void PlayTicTacSynced()
    {
        if (!_playerCerca) return;
        if (!_asTicTac || !sfxTicTac) return;

        // Reinicia el mismo source -> NO se apila, y queda siempre alineado al movimiento
        _asTicTac.volume = volumenTicTac;

        _asTicTac.Stop();
        _asTicTac.clip = sfxTicTac;

        float off = Mathf.Max(0f, audioOffsetSeconds);
        if (off > 0f && sfxTicTac.length > 0.01f)
            off = Mathf.Min(off, sfxTicTac.length - 0.01f);

        _asTicTac.time = off;
        _asTicTac.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (usado) return;
        if (!other.CompareTag("Player")) return;

        usado = true;

        if (controladorJuego)
        {
            controladorJuego.ActivarTemporizador();
        }

        PlayOneShotRuteado();

        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        if (destruirAlTocar)
            Destroy(gameObject);
    }

    void PlayOneShotRuteado()
    {
        if (!sfx) return;

        GameObject go = new GameObject("SFX_MetaInicio");
        go.transform.position = transform.position;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f; // 2D
        a.volume = volumen;
        a.outputAudioMixerGroup = mixerGroup;

        a.clip = sfx;
        a.Play();

        Destroy(go, sfx.length);
    }
}
