using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SFXOnEnable : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip clip;
    public AudioMixerGroup outputGroup;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Anti input fantasma")]
    public bool waitForKeyRelease = true;
    public float armDelayRealtime = 0.00f;

    [Header("Opciones")]
    public bool includeMouseButtons = true;
    public bool playOnceGlobal = true;
    public bool debugLogs = false;

    static bool s_played;
    bool _armed;

    void OnEnable()
    {
        _armed = false;
        StopAllCoroutines();
        StartCoroutine(CoArm());
    }

    IEnumerator CoArm()
    {
        yield return null; // evita el input del mismo frame
        if (armDelayRealtime > 0f)
            yield return new WaitForSecondsRealtime(armDelayRealtime);

        if (waitForKeyRelease)
            while (AnyKeyHeld())
                yield return null;

        _armed = true;
        if (debugLogs) Debug.Log("[SFXOnFirstAnyKey] Armed");
    }

    void Update()
    {
        if (!_armed) return;
        if (!clip) return;
        if (playOnceGlobal && s_played) return;

        bool pressed = Input.anyKeyDown;
        if (includeMouseButtons)
            pressed |= Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);

        if (!pressed) return;

        OneShotPlayer.Play(clip, outputGroup, volume);

        if (debugLogs) Debug.Log("[SFXOnFirstAnyKey] Played");
        if (playOnceGlobal) s_played = true;

        _armed = false;
        enabled = false;
    }

    bool AnyKeyHeld()
    {
        bool held = Input.anyKey;
        if (includeMouseButtons)
            held |= Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        return held;
    }

    static class OneShotPlayer
    {
        static GameObject _go;
        static AudioSource _src;

        static void Ensure()
        {
            if (_src) return;
            _go = new GameObject("OneShotPlayer_Persist");
            Object.DontDestroyOnLoad(_go);
            _src = _go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;
            _src.spatialBlend = 0f;
        }

        public static void Play(AudioClip c, AudioMixerGroup group, float vol)
        {
            if (!c) return;
            Ensure();
            _src.outputAudioMixerGroup = group;
            _src.volume = Mathf.Clamp01(vol);
            _src.PlayOneShot(c);
        }
    }
}