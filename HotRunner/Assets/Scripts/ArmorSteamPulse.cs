using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class ArmorSteamPulse : MonoBehaviour
{
    [Header("Particles (puff)")]
    [SerializeField] private ParticleSystem[] steamSystems; // L y R
    [SerializeField] private int emitCount = 18;

    [Header("Timing (puff)")]
    [SerializeField] private float interval = 5f;
    [SerializeField] private Vector2 intervalJitter = new Vector2(0f, 0f);

    [Header("Audio - Puff (oneshot)")]
    [SerializeField] private AudioClip[] puffClips;
    [SerializeField] private AudioMixerGroup puffMixerGroup;
    [SerializeField, Range(0f, 1f)] private float puffVolume = 0.9f;

    [Header("Audio - Armor Loop (constante)")]
    [SerializeField] private AudioClip armorLoopClip;
    [SerializeField] private AudioMixerGroup armorMixerGroup;
    [SerializeField, Range(0f, 1f)] private float armorLoopVolume = 0.12f;

    [Header("Armor Loop Pitch (random)")]
    [SerializeField] private float armorPitchMin = 0.98f;
    [SerializeField] private float armorPitchMax = 1.02f;

    private AudioSource _srcLoop;   // loop de armadura (manual)
    private AudioSource _srcPuff;   // puffs oneshot

    private Coroutine _coPulse;
    private Coroutine _coArmorLoop;
    private bool _stopped;

    void Awake()
    {
        EnsureSources();
        ApplyMixerGroups();
    }

    void OnEnable()
    {
        _stopped = false;

        if (GameOverManager.IsGameOverActive)
        {
            StopSteamNow();
            return;
        }

        StartArmorLoop();

        if (_coPulse != null) StopCoroutine(_coPulse);
        _coPulse = StartCoroutine(CoPulse());
    }

    void Update()
    {
        if (!_stopped && GameOverManager.IsGameOverActive)
            StopSteamNow();
    }

    void OnDisable()
    {
        StopSteamNow();
    }

    IEnumerator CoPulse()
    {
        yield return new WaitForSeconds(Random.Range(0.1f, 0.4f));

        while (true)
        {
            if (GameOverManager.IsGameOverActive || _stopped)
                yield break;

            DoPulse();

            float t = interval + Random.Range(intervalJitter.x, intervalJitter.y);
            if (t < 0.1f) t = 0.1f;

            yield return new WaitForSeconds(t);
        }
    }

    void DoPulse()
    {
        if (_stopped) return;
        if (GameOverManager.IsGameOverActive) return;

        // Partículas
        if (steamSystems != null)
        {
            for (int i = 0; i < steamSystems.Length; i++)
            {
                var ps = steamSystems[i];
                if (ps) ps.Emit(emitCount);
            }
        }

        // Sonido puff
        if (puffClips != null && puffClips.Length > 0 && _srcPuff != null)
        {
            var clip = puffClips[Random.Range(0, puffClips.Length)];
            if (clip) _srcPuff.PlayOneShot(clip, puffVolume);
        }
    }

    void StartArmorLoop()
    {
        if (_srcLoop == null) return;
        if (armorLoopClip == null) return;

        ApplyMixerGroups();

        // Loop manual: re-randomiza pitch cada vez que vuelve a arrancar
        if (_coArmorLoop != null) StopCoroutine(_coArmorLoop);
        _coArmorLoop = StartCoroutine(CoArmorLoopManual());
    }

    IEnumerator CoArmorLoopManual()
    {
        // seguridad por si el usuario puso min > max
        float min = armorPitchMin;
        float max = armorPitchMax;
        if (max < min) { float tmp = min; min = max; max = tmp; }
        if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f))
        {
            min = 1f; max = 1f;
        }

        while (!_stopped && !GameOverManager.IsGameOverActive)
        {
            if (armorLoopClip == null) yield break;

            _srcLoop.clip = armorLoopClip;
            _srcLoop.volume = armorLoopVolume;

            float p = Random.Range(min, max);
            if (Mathf.Abs(p) < 0.01f) p = 0.01f; // evita division por 0
            _srcLoop.pitch = p;

            _srcLoop.Stop();
            _srcLoop.Play();

            // Espera REAL (no depende de timeScale) para que funcione igual con slowmo
            float dur = armorLoopClip.length / Mathf.Abs(_srcLoop.pitch);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, dur));
        }
    }

    void EnsureSources()
    {
        var sources = GetComponents<AudioSource>();

        if (sources == null || sources.Length == 0)
        {
            _srcLoop = gameObject.AddComponent<AudioSource>();
            _srcPuff = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            _srcLoop = sources[0];
            _srcPuff = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            _srcLoop = sources[0];
            _srcPuff = sources[1];
        }

        if (_srcLoop != null)
        {
            _srcLoop.playOnAwake = false;
            _srcLoop.loop = false; // IMPORTANT: loop manual
        }

        if (_srcPuff != null)
        {
            _srcPuff.playOnAwake = false;
            _srcPuff.loop = false;
        }
    }

    void ApplyMixerGroups()
    {
        if (_srcLoop != null && armorMixerGroup != null)
            _srcLoop.outputAudioMixerGroup = armorMixerGroup;

        if (_srcPuff != null && puffMixerGroup != null)
            _srcPuff.outputAudioMixerGroup = puffMixerGroup;

        if (_srcLoop != null)
            _srcLoop.volume = armorLoopVolume;
    }

    public void StopSteamNow()
    {
        if (_stopped) return;
        _stopped = true;

        if (_coPulse != null)
        {
            StopCoroutine(_coPulse);
            _coPulse = null;
        }

        if (_coArmorLoop != null)
        {
            StopCoroutine(_coArmorLoop);
            _coArmorLoop = null;
        }

        if (_srcPuff != null) _srcPuff.Stop();

        if (_srcLoop != null)
        {
            _srcLoop.Stop();
            _srcLoop.clip = null;
        }

        // limpiar partículas
        if (steamSystems != null)
        {
            for (int i = 0; i < steamSystems.Length; i++)
            {
                var ps = steamSystems[i];
                if (!ps) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
