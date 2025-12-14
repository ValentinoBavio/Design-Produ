using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DeadzoneRespawn : MonoBehaviour
{
    [Header("Respawn (por Inspector)")]
    public Transform[] puntosRespawn;

    public enum ModoElegir { Primero, Random, RoundRobin, IndiceFijo }
    public ModoElegir modo = ModoElegir.Primero;
    public int indiceFijo = 0;

    [Header("Timer")]
    [Tooltip("A=true reinicia. B=false mantiene.")]
    public bool reiniciarTimer = true;
    public ControladorJuego controladorJuego;

    [Header("Anti-loop / Anti-doble")]
    public float cooldown = 0.35f;
    public bool ignorarColisionDuranteCooldown = true;
    public float spawnUpOffset = 0.20f;

    [Header("Si los deadzones se solapan, el de mayor prioridad gana")]
    public int prioridad = 0;

    [Header("Debug")]
    public bool debugLogs = false;

    Collider _dzCol;
    int _rr;

    // ---- Estado global por Rigidbody (para evitar doble por colliders / solapes) ----
    struct Request
    {
        public DeadzoneRespawn zone;
        public Transform punto;
        public bool reiniciarTimer;
        public int prioridad;
        public float time;
    }

    static readonly Dictionary<int, Request> _pending = new Dictionary<int, Request>();
    static readonly Dictionary<int, float> _lastRespawnTime = new Dictionary<int, float>();
    static readonly HashSet<int> _executing = new HashSet<int>();

    void Awake()
    {
        _dzCol = GetComponent<Collider>();
        if (_dzCol) _dzCol.isTrigger = true;

        if (!controladorJuego)
            controladorJuego = FindObjectOfType<ControladorJuego>();
    }

    void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponentInParent<Rigidbody>();
        if (rb == null) return;

        int id = rb.GetInstanceID();
        float now = Time.time;

        // Cooldown: si recién respawneó, ignorar para no entrar en loop
        if (_lastRespawnTime.TryGetValue(id, out float last) && (now - last) < cooldown)
            return;

        Transform punto = ElegirPunto();
        if (punto == null) return;

        // Guardar request (si hay solape, gana el de mayor prioridad; si empatan, el último)
        if (_pending.TryGetValue(id, out var prev))
        {
            bool shouldReplace =
                (prioridad > prev.prioridad) ||
                (prioridad == prev.prioridad && now >= prev.time);

            if (shouldReplace)
            {
                _pending[id] = new Request
                {
                    zone = this,
                    punto = punto,
                    reiniciarTimer = reiniciarTimer,
                    prioridad = prioridad,
                    time = now
                };
            }
        }
        else
        {
            _pending[id] = new Request
            {
                zone = this,
                punto = punto,
                reiniciarTimer = reiniciarTimer,
                prioridad = prioridad,
                time = now
            };
        }

        // Ejecutar 1 sola vez por RB (al final del frame)
        if (!_executing.Contains(id))
        {
            _executing.Add(id);
            StartCoroutine(EjecutarRespawnFinDeFrame(rb, id));
        }
    }

    IEnumerator EjecutarRespawnFinDeFrame(Rigidbody rb, int id)
    {
        // Espera a que se procesen TODOS los triggers de este frame
        yield return new WaitForEndOfFrame();

        if (rb == null)
        {
            _pending.Remove(id);
            _executing.Remove(id);
            yield break;
        }

        if (!_pending.TryGetValue(id, out var req) || req.punto == null || req.zone == null)
        {
            _pending.Remove(id);
            _executing.Remove(id);
            yield break;
        }

        var zone = req.zone;

        if (zone.debugLogs)
            Debug.Log($"[DeadZoneRespawn] '{zone.name}' -> '{req.punto.name}' | reiniciarTimer={req.reiniciarTimer} | prioridad={req.prioridad}");

        // Ignorar colisión temporal con EL deadzone ganador para evitar re-trigger por overlap
        if (zone.ignorarColisionDuranteCooldown && zone._dzCol != null)
            zone.StartCoroutine(zone.IgnorarColisionTemporal(rb, zone.cooldown));

        zone.Respawnear(rb, req.punto.position, req.punto.rotation);

        if (zone.controladorJuego)
        {
            if (req.reiniciarTimer) zone.controladorJuego.ActivarTemporizador();
            else                    zone.controladorJuego.ReanudarTemporizadorSinReset();
        }

        _lastRespawnTime[id] = Time.time;

        _pending.Remove(id);
        _executing.Remove(id);
    }

    Transform ElegirPunto()
    {
        if (puntosRespawn == null || puntosRespawn.Length == 0) return null;

        switch (modo)
        {
            case ModoElegir.Random:
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    var t = puntosRespawn[Random.Range(0, puntosRespawn.Length)];
                    if (t != null) return t;
                }
                break;
            }
            case ModoElegir.RoundRobin:
            {
                for (int k = 0; k < puntosRespawn.Length; k++)
                {
                    _rr = (_rr + 1) % puntosRespawn.Length;
                    if (puntosRespawn[_rr] != null) return puntosRespawn[_rr];
                }
                break;
            }
            case ModoElegir.IndiceFijo:
            {
                indiceFijo = Mathf.Clamp(indiceFijo, 0, puntosRespawn.Length - 1);
                if (puntosRespawn[indiceFijo] != null) return puntosRespawn[indiceFijo];
                break;
            }
        }

        // Primero (o fallback)
        for (int i = 0; i < puntosRespawn.Length; i++)
            if (puntosRespawn[i] != null) return puntosRespawn[i];

        return null;
    }

    void Respawnear(Rigidbody rb, Vector3 pos, Quaternion rot)
    {
        Vector3 destino = pos + Vector3.up * spawnUpOffset;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = destino;
        rb.rotation = rot;

        rb.Sleep();
    }

    IEnumerator IgnorarColisionTemporal(Rigidbody rb, float secs)
    {
        if (_dzCol == null || rb == null) yield break;

        var cols = rb.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c) Physics.IgnoreCollision(_dzCol, c, true);
        }

        yield return new WaitForSeconds(secs);

        if (_dzCol == null || rb == null) yield break;

        cols = rb.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c) Physics.IgnoreCollision(_dzCol, c, false);
        }
    }
}