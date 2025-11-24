using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Audio;
using UnityEngine.UI;


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
    [Range(0f,1f)] public float brightAtMin = 0.2f;
    [Range(0f,1f)] public float brightAtMax = 0.85f;

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

    public AudioMixer audioMixer;   // Poné el mixer acá
    public Slider volumeSlider;     // Poné el slider del menú de pausa

    void Start()
    {
        // Carga el volumen guardado previamente (opcional)
        float savedVolume = PlayerPrefs.GetFloat("Master", 1f);
        audioMixer.SetFloat("Master", savedVolume);
        volumeSlider.value = savedVolume;
    }

    public void SetVolume(float volume)
    {
        audioMixer.SetFloat("Master", volume);
        PlayerPrefs.SetFloat("Master", volume);
    }
    void Awake()
    {
        // Asegurá que este GO tenga SOLO 1 AudioSource (el de este script)
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

        rng = new System.Random(System.Environment.TickCount ^ GetInstanceID());

        // arranque silencioso
        curLinGain = 0f;
        lpState = 0f;
        wantMute = true;
        appliedMute = true;
        src.mute = true;
    }

    void Update()
    {
        // Auto-revivir la fuente si hace falta cuando estamos slideando
        if (sliding)
        {
            if (!src.isPlaying)
            {
                if (debugLogs) Debug.Log("[ProceduralSlideNoise] Revive Play()");
                src.Play();
            }
            if (wantMute) wantMute = false; // desmutear en cuanto hay slide
        }
        else
        {
            // si no hay slide y estamos abajo del gate, pedimos mute (se aplicará acá)
            if (curLinGain <= hardGateLinear) wantMute = true;
        }

        // aplicar mute/unmute en main thread
        if (appliedMute != wantMute)
        {
            src.mute = wantMute;
            appliedMute = wantMute;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // 1) Target gain (lineal)
        float targetLin;
        if (!sliding)
        {
            targetLin = 0f; // silencio real si no hay slide
        }
        else
        {
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
            float db = Mathf.Lerp(minGainDb, maxGainDb, Mathf.Clamp01(t));
            targetLin = Mathf.Pow(10f, db / 20f);
        }

        // 2) Coeficientes per-sample (sin Time.deltaTime)
        float atkTau = Mathf.Max(0.001f, attackTime);
        float relTau = Mathf.Max(0.001f, releaseTime);
        float atkCoeff = 1f - Mathf.Exp(-1f / (sampleRate * atkTau));
        float relCoeff = 1f - Mathf.Exp(-1f / (sampleRate * relTau));

        // 3) Low-pass según brillo
        float brightT = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        float bright  = Mathf.Lerp(brightAtMin, brightAtMax, Mathf.Clamp01(brightT));
        float cutoffHz = Mathf.Lerp(800f, 8000f, bright);
        float alpha = Mathf.Exp(-2f * Mathf.PI * cutoffHz / Mathf.Max(1, sampleRate));

        // Gate duro solo cuando NO hay slide y estamos bien abajo
        bool gateSilence = (!sliding && curLinGain <= hardGateLinear && targetLin <= hardGateLinear);
        if (gateSilence)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            lpState = 0f;
            wantMute = true; // se aplicará en Update
            return;
        }

        wantMute = false; // hay actividad, asegurá unmute

        // 4) Procesamiento
        for (int i = 0; i < data.Length; i += channels)
        {
            // envelope attack/release
            float coeff = (targetLin > curLinGain) ? atkCoeff : relCoeff;
            curLinGain += (targetLin - curLinGain) * coeff;

            // white noise [-1..1]
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);

            // low-pass
            lpState = (1f - alpha) * n + alpha * lpState;

            float sample = lpState * curLinGain;

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;
        }
    }
}