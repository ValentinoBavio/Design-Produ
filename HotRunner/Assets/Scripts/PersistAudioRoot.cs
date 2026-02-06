using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PersistAudioRoot : MonoBehaviour
{
    [Header("Persistencia")]
    [Tooltip("Si este GameObject está bajo un parent, se desparenta automáticamente para que DontDestroyOnLoad funcione.")]
    public bool forceRoot = true;

    [Tooltip("Mantener este GameObject vivo entre escenas.")]
    public bool persistAcrossScenes = true;

    [Header("Mirrors (opcional)")]
    [Tooltip("Si está ON, crea mirrors de los AudioSources que encuentre en hijos (útil para persistir/duplicar fuentes).")]
    public bool buildMirrorsOnStart = true;

    [Tooltip("Incluye AudioSources inactivos al buscar.")]
    public bool includeInactive = true;

    [Tooltip("Si está ON, deshabilita los AudioSources originales después de crear el mirror (evita doble audio).")]
    public bool disableOriginalSources = false;

    [Tooltip("Nombre del contenedor donde se crean los mirrors.")]
    public string mirrorsContainerName = "_AudioMirrors";

    [Header("Debug")]
    public bool logDebug = false;

    // Interno
    private bool _didDontDestroy;
    private Transform _mirrorsContainer;
    private readonly List<AudioSource> _mirrors = new List<AudioSource>();

    void Awake()
    {
        EnsureRootAndPersist();
    }

    // En tu stacktrace se ve que Start es coroutine, lo mantenemos así
    IEnumerator Start()
    {
        // Espera 1 frame por si algo arma audio en Awake/Start de otros scripts
        yield return null;

        EnsureRootAndPersist();

        if (buildMirrorsOnStart)
            BuildMirrors();
    }

    void OnEnable()
    {
        EnsureRootAndPersist();
    }

    void EnsureRootAndPersist()
    {
        if (!Application.isPlaying) return;

        // ✅ 1) Asegurar que sea ROOT para que DontDestroyOnLoad funcione
        if (forceRoot && transform.parent != null)
        {
            if (logDebug) Debug.Log($"[PersistAudioRoot] Desparentando '{name}' para que sea ROOT.");
            transform.SetParent(null, true);
        }

        // ✅ 2) DontDestroyOnLoad SOLO sobre el root gameObject (una sola vez)
        if (persistAcrossScenes && !_didDontDestroy)
        {
            DontDestroyOnLoad(gameObject);
            _didDontDestroy = true;

            if (logDebug) Debug.Log($"[PersistAudioRoot] DontDestroyOnLoad aplicado a '{name}'.");
        }
    }

    [ContextMenu("Rebuild Mirrors")]
    public void BuildMirrors()
    {
        EnsureRootAndPersist();

        // Limpieza previa
        ClearMirrors();

        // Crear/obtener contenedor
        _mirrorsContainer = transform.Find(mirrorsContainerName);
        if (_mirrorsContainer == null)
        {
            var go = new GameObject(mirrorsContainerName);
            go.transform.SetParent(transform, false);
            _mirrorsContainer = go.transform;
        }

        // Buscar audios (excepto los que ya estén dentro del contenedor de mirrors)
        var sources = GetComponentsInChildren<AudioSource>(includeInactive);
        int created = 0;

        foreach (var src in sources)
        {
            if (src == null) continue;

            // Evitar espejar los mirrors mismos
            if (_mirrorsContainer != null && src.transform.IsChildOf(_mirrorsContainer))
                continue;

            // Crear mirror
            var mgo = new GameObject(src.gameObject.name + "_Mirror");
            mgo.transform.SetParent(_mirrorsContainer, false);

            var mirror = mgo.AddComponent<AudioSource>();
            CopyAudioSourceSettings(src, mirror);

            // Si el original estaba sonando, el mirror arranca
            if (src.isPlaying && src.clip != null)
            {
                mirror.time = Mathf.Clamp(src.time, 0f, src.clip.length);
                mirror.Play();
            }
            else if (mirror.playOnAwake && mirror.clip != null)
            {
                // Respeta playOnAwake del original
                mirror.Play();
            }

            _mirrors.Add(mirror);
            created++;

            if (disableOriginalSources)
                src.enabled = false;
        }

        if (logDebug) Debug.Log($"[PersistAudioRoot] Mirrors creados: {created}");
    }

    void CopyAudioSourceSettings(AudioSource src, AudioSource dst)
    {
        // Copia settings comunes (seguro / sin reflection)
        dst.clip = src.clip;
        dst.outputAudioMixerGroup = src.outputAudioMixerGroup;

        dst.mute = src.mute;
        dst.bypassEffects = src.bypassEffects;
        dst.bypassListenerEffects = src.bypassListenerEffects;
        dst.bypassReverbZones = src.bypassReverbZones;

        dst.playOnAwake = src.playOnAwake;
        dst.loop = src.loop;
        dst.priority = src.priority;
        dst.volume = src.volume;
        dst.pitch = src.pitch;
        dst.panStereo = src.panStereo;

        dst.spatialBlend = src.spatialBlend;
        dst.reverbZoneMix = src.reverbZoneMix;
        dst.dopplerLevel = src.dopplerLevel;
        dst.spread = src.spread;

        dst.minDistance = src.minDistance;
        dst.maxDistance = src.maxDistance;
        dst.rolloffMode = src.rolloffMode;

        dst.ignoreListenerVolume = src.ignoreListenerVolume;
        dst.ignoreListenerPause = src.ignoreListenerPause;
    }

    void ClearMirrors()
    {
        _mirrors.Clear();

        var existing = transform.Find(mirrorsContainerName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        _mirrorsContainer = null;
    }
}
