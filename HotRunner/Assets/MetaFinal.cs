using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MetaFinal : MonoBehaviour
{
    [SerializeField] private ControladorJuego controladorJuego;

    [Header("Sonido (Mixer)")]
    [SerializeField] private AudioClip sonidoVictoria;
    [SerializeField] private AudioMixerGroup mixerSFX; // arrastrá tu grupo SFX acá
    [Range(0f, 1f)] [SerializeField] private float volumen = 1f;

    private bool yaActivado = false;

    private void OnTriggerEnter(Collider other)
    {
        if (yaActivado) return;
        if (!other.CompareTag("Player")) return;

        yaActivado = true;

        controladorJuego.DesactivarTemporizador();
        Debug.Log("Ganaste!");

        if (sonidoVictoria != null)
            PlayOneShotConMixer(sonidoVictoria, transform.position, mixerSFX, volumen);

        FindObjectOfType<LevelEndManager>()?.CompletarNivel();

        Destroy(gameObject);
    }

    private void PlayOneShotConMixer(AudioClip clip, Vector3 pos, AudioMixerGroup group, float vol)
    {
        GameObject go = new GameObject("OneShot_SFX_MetaFinal");
        go.transform.position = pos;

        AudioSource a = go.AddComponent<AudioSource>();
        a.outputAudioMixerGroup = group;   // clave para respetar el mixer
        a.playOnAwake = false;
        a.spatialBlend = 0f;               // 0 = 2D (cambiá a 1 si querés 3D)
        a.volume = vol;

        a.PlayOneShot(clip);
        Destroy(go, clip.length + 0.1f);
    }
}