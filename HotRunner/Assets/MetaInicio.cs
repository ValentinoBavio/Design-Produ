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
    [SerializeField] private float pitch = 1f;

    [Header("Opcional")]
    [SerializeField] private bool destruirAlTocar = true;

    bool usado = false;

    private void Reset()
    {
        // Asegura trigger
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (usado) return;
        if (!other.CompareTag("Player")) return;

        usado = true;

        if (controladorJuego)
        {
            // comportamiento actual
            controladorJuego.ActivarTemporizador();

            // Si querés un tiempo distinto a 60, mirá la nota abajo.
            // (Sin un método público en ControladorJuego no se puede setear "tiempo max" prolijo).
        }

        PlayOneShotRuteado();

        // Evitar doble trigger
        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        if (destruirAlTocar)
            Destroy(gameObject);
    }

    void PlayOneShotRuteado()
    {
        if (!sfx) return;

        // Crear un GO temporal para que el audio NO se corte al destruir este pickup
        GameObject go = new GameObject("SFX_MetaInicio");
        go.transform.position = transform.position;

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f;          // 2D
        a.volume = volumen;
        a.pitch = pitch;
        a.outputAudioMixerGroup = mixerGroup;

        a.clip = sfx;
        a.Play();

        Destroy(go, sfx.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));
    }
}