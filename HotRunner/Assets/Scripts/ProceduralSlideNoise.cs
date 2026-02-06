using System;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class ProceduralSlideNoise : MonoBehaviour
{
    [Header("Control externo")]
    public bool sliding = false;  // true al iniciar slide, false al terminar
    public float speed = 0f;      // velocidad horizontal (m/s)

    [Header("Ganancia por velocidad")]
    public float minSpeed = 2f;
    public float maxSpeed = 18f;
    public float minGainDb = -30f;
    public float maxGainDb = -8f;

    [Header("Color del ruido")]
    [Range(0f, 1f)] public float brightAtMin = 0.2f;
    [Range(0f, 1f)] public float brightAtMax = 0.85f;

    [Header("Envolvente (suavizado)")]
    public float attackTime = 0.06f;
    public float releaseTime = 0.12f;

    [Header("Silent Gate")]
    public float hardGateLinear = 0.0001f; // ~ -66 dB

    [Header("Debug")]
    public bool debugLogs = false;

    // --- internos ---
    AudioSource src;
    int sampleRate;
    float curLinGain;   // ganancia lineal suavizada por-sample
    float lpState;      // estado del low-pass
    System.Random rng;

    // mute que se aplica en main thread
    volatile bool wantMute = true;
    bool appliedMute = true;

    [Header("Audio Mixer (Master global)")]
    [Tooltip("Arrastrá el AudioMixer asset acá.")]
    public AudioMixer audioMixer;

    [Tooltip("Nombre EXACTO del parámetro expuesto del Master en el mixer (ej: MasterVolume).")]
    public string masterParam = "MasterVolume";

    [Tooltip("Si está activo, al iniciar aplica el volumen guardado en PlayerPrefs (key: 'Master') al mixer.")]
    public bool applySavedMasterOnStart = true;

    [Tooltip("Límites para cuando se guarda en dB.")]
    public float minDbClamp = -80f;
    public float maxDbClamp = 0f;

    void Awake()
    {
        var audios = GetComponents<AudioSource>();
        if (audios.Length > 1)
        {
            Debug.LogError($"[ProceduralSlideNoise] Hay {audios.Length} AudioSources en '{name}'. " +
                           "Dejalo con UNO solo en este GameObject.");
        }

        sampleRate = AudioSettings.outputSampleRate;

        src = GetComponent<AudioSource>();
        src.playOnAwake = true;
        src.loop = true;
        src.spatialBlend = 0f; // 2D
        src.volume = 0.015f;

        // clip dummy para disparar OnAudioFilterRead
        var dummy = AudioClip.Create("ProceduralSlideNoiseDummy", sampleRate, 1, sampleRate, false);
        src.clip = dummy;
        src.Play();

        rng = new System.Random(Environment.TickCount ^ GetInstanceID());

        // arranque silencioso
        curLinGain = 0f;
        lpState = 0f;
        wantMute = true;
        appliedMute = true;
        src.mute = true;
    }

    void Start()
    {
        if (!applySavedMasterOnStart) return;

        // "Master" puede estar guardado como lineal (0..1) o como dB (negativo).
        float saved = PlayerPrefs.GetFloat("Master", 1f);

        if (audioMixer != null && !string.IsNullOrEmpty(masterParam))
        {
            float dbToApply = SavedMasterToDb(saved);
            audioMixer.SetFloat(masterParam, dbToApply);

            if (debugLogs)
                Debug.Log($"[ProceduralSlideNoise] Apply Master '{masterParam}' = {dbToApply:0.0} dB (raw saved={saved:0.###})");
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[ProceduralSlideNoise] No se aplicó Master: AudioMixer o masterParam no asignado.");
        }
    }

    // -------------------- API para tu MainMenu (por si querés llamarlo) --------------------

    /// <summary>
    /// Slider en 0..1 (lineal). Convierte a dB, aplica al mixer y guarda PlayerPrefs("Master") en LINEAL.
    /// </summary>
    public void SetMasterVolumeLinear(float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);

        if (audioMixer != null && !string.IsNullOrEmpty(masterParam))
            audioMixer.SetFloat(masterParam, LinearToDb(linear01));

        PlayerPrefs.SetFloat("Master", linear01);
    }

    /// <summary>
    /// Si tu MainMenu ya trabaja en dB (ej -30..0), usa esto. Guarda PlayerPrefs("Master") en dB.
    /// </summary>
    public void SetMasterVolumeDb(float db)
    {
        float clamped = Mathf.Clamp(db, minDbClamp, maxDbClamp);

        if (audioMixer != null && !string.IsNullOrEmpty(masterParam))
            audioMixer.SetFloat(masterParam, clamped);

        PlayerPrefs.SetFloat("Master", clamped);
    }

    float SavedMasterToDb(float saved)
    {
        // Si está en [0..1] => es lineal
        if (saved >= 0f && saved <= 1.0f)
            return LinearToDb(saved);

        // Si no, asumimos que es dB guardado (negativo típicamente)
        return Mathf.Clamp(saved, minDbClamp, maxDbClamp);
    }

    // -------------------- Loop principal --------------------

    void Update()
    {
        if (sliding)
        {
            if (!src.isPlaying)
            {
                if (debugLogs) Debug.Log("[ProceduralSlideNoise] Revive Play()");
                src.Play();
            }
            if (wantMute) wantMute = false;
        }
        else
        {
            if (curLinGain <= hardGateLinear) wantMute = true;
        }

        if (appliedMute != wantMute)
        {
            src.mute = wantMute;
            appliedMute = wantMute;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float targetLin;
        if (!sliding)
        {
            targetLin = 0f;
        }
        else
        {
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
            float db = Mathf.Lerp(minGainDb, maxGainDb, Mathf.Clamp01(t));
            targetLin = Mathf.Pow(10f, db / 20f);
        }

        float atkTau = Mathf.Max(0.001f, attackTime);
        float relTau = Mathf.Max(0.001f, releaseTime);
        float atkCoeff = 1f - Mathf.Exp(-1f / (sampleRate * atkTau));
        float relCoeff = 1f - Mathf.Exp(-1f / (sampleRate * relTau));

        float brightT = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        float bright = Mathf.Lerp(brightAtMin, brightAtMax, Mathf.Clamp01(brightT));
        float cutoffHz = Mathf.Lerp(800f, 8000f, bright);
        float alpha = Mathf.Exp(-2f * Mathf.PI * cutoffHz / Mathf.Max(1, sampleRate));

        bool gateSilence = (!sliding && curLinGain <= hardGateLinear && targetLin <= hardGateLinear);
        if (gateSilence)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            lpState = 0f;
            wantMute = true;
            return;
        }

        wantMute = false;

        for (int i = 0; i < data.Length; i += channels)
        {
            float coeff = (targetLin > curLinGain) ? atkCoeff : relCoeff;
            curLinGain += (targetLin - curLinGain) * coeff;

            float n = (float)(rng.NextDouble() * 2.0 - 1.0);

            lpState = (1f - alpha) * n + alpha * lpState;

            float sample = lpState * curLinGain;

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;
        }
    }

    static float LinearToDb(float linear)
    {
        return Mathf.Log10(Mathf.Max(0.0001f, linear)) * 20f;
    }
}
