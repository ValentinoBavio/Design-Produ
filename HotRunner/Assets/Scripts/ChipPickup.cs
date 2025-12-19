using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(Collider))]
public class ChipPickup : MonoBehaviour
{
    [Header("Chips")]
    public int amount = 1;

    [Header("Quién puede pickear")]
    public string playerTag = "Player";
    public float armDelay = 0.10f;

    [Header("SFX (AudioMixer friendly)")]
    [Tooltip("Arrastrá un AudioSource de tu escena (por ejemplo un AudioManager/SFX) que ya tenga Output -> tu AudioMixerGroup.")]
    public AudioSource sfxSource;

    [Tooltip("Opcional: si NO asignás sfxSource, podés asignar el grupo para el AudioSource temporal.")]
    public AudioMixerGroup outputMixerGroup;

    public AudioClip sfxPickupChip;
    [Range(0f, 1f)] public float sfxVol = 1f;

    [Header("SFX 3D (si creamos uno temporal)")]
    public bool sfx3D = true;
    public float minDistance = 3f;
    public float maxDistance = 60f;

    [Header("VFX (opcional)")]
    public GameObject pickupVFX;
    public float vfxLife = 2f;

    [Header("Flotación + giro (opcional)")]
    public float bobAmplitude = 0.15f;
    public float bobFreq = 1.2f;
    public Vector3 spinDegPerSec = new Vector3(0f, 110f, 0f);

    [Header("Anti doble-trigger")]
    public bool disableAllCollidersOnPickup = true;

    [Header("Debug")]
    public bool debugLogs = false;

    Transform visual;
    Vector3 startPos;
    bool picked = false;
    bool armed = false;

    Collider[] cachedColliders;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // cache colliders (padre + hijos) para apagar al instante al pickear
        cachedColliders = GetComponentsInChildren<Collider>(true);

        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        visual = transform;
        startPos = transform.position;
    }

    void OnEnable()
    {
        picked = false;
        armed = false;
        CancelInvoke(nameof(Armar));
        Invoke(nameof(Armar), armDelay);
    }

    void Armar() => armed = true;

    void Update()
    {
        if (picked) return;

        Vector3 p = startPos;
        p.y += Mathf.Sin(Time.time * bobFreq * Mathf.PI * 2f) * bobAmplitude;
        visual.position = p;

        if (spinDegPerSec.sqrMagnitude > 0.0001f)
            visual.Rotate(spinDegPerSec * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (picked || !armed) return;
        if (!other.CompareTag(playerTag)) return;

        // IMPORTANTÍSIMO: lockear primero para evitar doble pick
        picked = true;
        armed = false;

        if (disableAllCollidersOnPickup && cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
                if (cachedColliders[i]) cachedColliders[i].enabled = false;
        }

        if (debugLogs)
            Debug.Log($"[ChipPickup] Pick OK (amount={amount}) frame={Time.frameCount} obj={name}");

        // TODO: acá sumás chips en tu contador/UI
        // ChipDisplay.Instance?.OnCollected(amount);

        // SFX
        if (sfxPickupChip)
            PlayChipSfx(sfxPickupChip, transform.position, sfxVol);

        // VFX
        if (pickupVFX)
            Destroy(Instantiate(pickupVFX, transform.position, Quaternion.identity), vfxLife);

        Destroy(gameObject);
    }

    void PlayChipSfx(AudioClip clip, Vector3 pos, float vol)
    {
        // 1) Si me diste un AudioSource (recomendado para AudioMixer), uso ese
        if (sfxSource != null)
        {
            // Fix del error: si está disabled o el GO está apagado, lo prendo
            if (!sfxSource.gameObject.activeInHierarchy)
                sfxSource.gameObject.SetActive(true);
            if (!sfxSource.enabled)
                sfxSource.enabled = true;

            sfxSource.PlayOneShot(clip, vol);
            return;
        }

        // 2) Si no hay AudioSource asignado, creo uno temporal (también puede ir al mixer group)
        var go = new GameObject("SFX_ChipPickup_Temp");
        go.transform.position = pos;

        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = vol;
        a.playOnAwake = false;
        a.loop = false;

        if (outputMixerGroup) a.outputAudioMixerGroup = outputMixerGroup;

        if (sfx3D)
        {
            a.spatialBlend = 1f;
            a.dopplerLevel = 0f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = minDistance;
            a.maxDistance = maxDistance;
        }
        else
        {
            a.spatialBlend = 0f;
        }

        // por si Unity lo crea disabled por alguna razón
        a.enabled = true;
        go.SetActive(true);

        a.Play();
        Destroy(go, clip.length + 0.2f);
    }
}