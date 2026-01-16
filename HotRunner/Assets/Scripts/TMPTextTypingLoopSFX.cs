using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPTextTypingLoopSFX : MonoBehaviour
{
    public AudioClip typingLoop;
    public AudioMixerGroup sfxGroup;

    [Range(0f, 1f)] public float volume = 0.6f;
    public bool usarUnscaledTime = true;

    [Tooltip("Si el texto no cambia por este tiempo, corta el loop.")]
    public float idleStopDelay = 0.08f;

    TextMeshProUGUI _tmp;
    AudioSource _src;

    string _lastText = "";
    float _lastChangeTime;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();

        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = true;
        _src.volume = volume;
        if (sfxGroup) _src.outputAudioMixerGroup = sfxGroup;
    }

    void OnDisable()
    {
        if (_src && _src.isPlaying) _src.Stop();
    }

    void Update()
    {
        if (!typingLoop) return;

        float now = usarUnscaledTime ? Time.unscaledTime : Time.time;

        if (_tmp.text != _lastText)
        {
            _lastText = _tmp.text;
            _lastChangeTime = now;

            if (!_src.isPlaying)
            {
                _src.clip = typingLoop;
                _src.volume = volume;
                _src.Play();
            }
        }

        if (_src.isPlaying && (now - _lastChangeTime) > idleStopDelay)
        {
            _src.Stop();
        }
    }
}