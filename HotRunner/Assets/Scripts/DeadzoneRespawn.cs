using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DeadzoneRespawn : MonoBehaviour
{
    [Header("Detectar Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Respawn")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("Cooldown para evitar re-trigger infinito si respawneas dentro del trigger.")]
    [SerializeField] private float cooldownSeg = 0.25f;

    [Header("Opcional")]
    [SerializeField] private bool resetearVelocidad = true;

    [Tooltip("Si activás esto, loguea en consola por qué no respawnea.")]
    [SerializeField] private bool debugLogs = true;

    float _nextAllowedTime = 0f;

    private void OnTriggerEnter(Collider other)
    {
        TryRespawn(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Por si “tunea” el trigger o cae muy rápido, lo seguimos intentando con cooldown.
        TryRespawn(other);
    }

    void TryRespawn(Collider other)
    {
        if (Time.time < _nextAllowedTime) return;

        // agarramos el root del objeto que entró
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform.root : other.transform.root;

        // chequeo de tag en el root (no en el collider hijo)
        if (!root.CompareTag(playerTag))
        {
            if (debugLogs)
                Debug.Log($"[DeadZoneRespawn] Entró '{root.name}' pero NO tiene tag '{playerTag}'. Tag actual: '{root.tag}'", root);
            return;
        }

        if (!respawnPoint)
        {
            if (debugLogs)
                Debug.LogWarning("[DeadZoneRespawn] No hay respawnPoint asignado en el Inspector.", this);
            return;
        }

        _nextAllowedTime = Time.time + Mathf.Max(0.05f, cooldownSeg);

        // Buscar CC/RB en el root
        CharacterController cc = root.GetComponent<CharacterController>();
        Rigidbody rb = root.GetComponent<Rigidbody>();

        if (debugLogs)
            Debug.Log($"[DeadZoneRespawn] Respawn '{root.name}' -> {respawnPoint.position}", this);

        // Teleport seguro
        if (cc)
        {
            cc.enabled = false;
            root.position = respawnPoint.position;
            root.rotation = respawnPoint.rotation;
            cc.enabled = true;
        }
        else
        {
            root.position = respawnPoint.position;
            root.rotation = respawnPoint.rotation;
        }

        if (resetearVelocidad && rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}