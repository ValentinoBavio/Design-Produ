using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistAudioRoot : MonoBehaviour
{
    [Header("Dueños que TIENEN los AudioSources loop (arrastrá acá tus GO de audio)")]
    public GameObject[] owners;

    [Header("Búsqueda")]
    public bool includeChildren = false;
    public bool mirrorOnlyLooping = true;

    [Header("Comportamiento")]
    [Tooltip("Mutea los originales para que NO dupliquen. Los clones son los que se escuchan.")]
    public bool muteOriginals = true;

    [Header("Auto-destruir")]
    public bool destroyOnScene = true;
    public string sceneNameToDestroyOn = "Level1";

    class Mirror
    {
        public AudioSource src;
        public AudioSource clone;
        public bool cloneStarted;
    }

    readonly List<Mirror> _mirrors = new List<Mirror>();
    bool _built;

    void Awake()
    {
        if (!Application.isPlaying) return;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!destroyOnScene) return;

        if (scene.name == sceneNameToDestroyOn)
        {
            // desmuteo originales por si siguen existiendo
            for (int i = 0; i < _mirrors.Count; i++)
                if (_mirrors[i] != null && _mirrors[i].src) _mirrors[i].src.mute = false;

            Destroy(gameObject);
        }
    }

    IEnumerator Start()
    {
        if (!Application.isPlaying) yield break;

        // Esperar 1 frame por si tus AudioSources se crean por script en Awake/Start
        yield return null;

        BuildMirrors();
    }

    void BuildMirrors()
    {
        if (_built) return;
        _built = true;

        _mirrors.Clear();

        if (owners == null || owners.Length == 0)
        {
            Debug.LogWarning("[PersistLoopAudioMirror] No hay owners asignados.");
            return;
        }

        foreach (var o in owners)
        {
            if (!o) continue;

            var sources = includeChildren ? o.GetComponentsInChildren<AudioSource>(true) : o.GetComponents<AudioSource>();
            foreach (var s in sources)
            {
                if (!s) continue;
                if (mirrorOnlyLooping && !s.loop) continue;

                var m = new Mirror();
                m.src = s;

                var go = new GameObject("Mirror_" + s.gameObject.name + "_" + s.GetInstanceID());
                go.transform.SetParent(transform, false);
                DontDestroyOnLoad(go);

                m.clone = go.AddComponent<AudioSource>();
                CopySettings(s, m.clone);

                if (s.clip && s.isPlaying)
                {
                    m.clone.clip = s.clip;
                    m.clone.Play();
                    SafeSyncTimeSamples(s, m.clone);
                    m.cloneStarted = true;
                }

                if (muteOriginals)
                    s.mute = true;

                _mirrors.Add(m);
            }
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_mirrors.Count == 0) return;

        for (int i = 0; i < _mirrors.Count; i++)
        {
            var m = _mirrors[i];
            if (m == null || !m.clone) continue;

            var s = m.src;

            if (s)
            {
                if (muteOriginals)
                    s.mute = true;

                CopySettings(s, m.clone);

                if (m.clone.clip != s.clip)
                {
                    m.clone.clip = s.clip;
                    if (s.clip && s.isPlaying)
                    {
                        if (!m.clone.isPlaying) m.clone.Play();
                        SafeSyncTimeSamples(s, m.clone);
                        m.cloneStarted = true;
                    }
                }

                if (s.clip && s.isPlaying)
                {
                    if (!m.clone.isPlaying)
                    {
                        m.clone.Play();
                        SafeSyncTimeSamples(s, m.clone);
                        m.cloneStarted = true;
                    }
                    else
                    {
                        int diff = Mathf.Abs(SafeGetTimeSamples(s) - SafeGetTimeSamples(m.clone));
                        if (diff > 4000) // ~0.09s a 44.1k
                            SafeSyncTimeSamples(s, m.clone);
                    }
                }
                // Si el original se detuvo, el clone sigue solo.
            }
        }
    }

    static void CopySettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.volume = from.volume;
        to.pitch = from.pitch;
        to.loop = true;
        to.spatialBlend = from.spatialBlend;
        to.panStereo = from.panStereo;
        to.priority = from.priority;
        to.ignoreListenerPause = from.ignoreListenerPause;
        to.ignoreListenerVolume = from.ignoreListenerVolume;
    }

    static int SafeGetTimeSamples(AudioSource a)
    {
        try { return a.timeSamples; } catch { return 0; }
    }

    static void SafeSyncTimeSamples(AudioSource from, AudioSource to)
    {
        if (!from.clip || !to.clip) return;
        try
        {
            int ts = from.timeSamples;
            ts = Mathf.Clamp(ts, 0, to.clip.samples - 1);
            to.timeSamples = ts;
        }
        catch { }
    }
}