using System.Collections;
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
    bool _respawning = false;

    private void OnTriggerEnter(Collider other) => TryRespawn(other);

    private void OnTriggerStay(Collider other)
    {
        TryRespawn(other);
    }

    void TryRespawn(Collider other)
    {
        // ✅ NO RESPAWNEAR si ya estás en Game Over
        if (GameOverManager.IsGameOverActive)
        {
            if (debugLogs)
                Debug.Log("[DeadZoneRespawn] Ignorado: GameOver activo.", this);
            return;
        }

        if (_respawning) return;
        if (Time.time < _nextAllowedTime) return;

        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform.root : other.transform.root;

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

        Rigidbody rb = other.attachedRigidbody;
        CharacterController cc = root.GetComponent<CharacterController>();

        if (debugLogs)
            Debug.Log($"[DeadZoneRespawn] Respawn '{root.name}' -> {respawnPoint.position} (rb={(rb ? rb.name : "null")})", this);

        _respawning = true;
        StartCoroutine(RespawnSequence(root, cc, rb));
    }

    IEnumerator RespawnSequence(Transform root, CharacterController cc, Rigidbody rb)
    {
        bool ccWasEnabled = (cc != null) && cc.enabled;

        bool rbHad = rb != null;
        bool rbWasKinematic = false;
        bool rbWasUsingGravity = false;

        try
        {
            // ✅ Chequeo extra por si se activó GameOver mientras corría la coroutine
            if (GameOverManager.IsGameOverActive)
                yield break;

            if (cc != null && cc.enabled)
                cc.enabled = false;

            if (rbHad)
            {
                rbWasKinematic = rb.isKinematic;
                rbWasUsingGravity = rb.useGravity;

                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Glitch opcional (si existe) — pero NO si GameOver se activó
            if (!GameOverManager.IsGameOverActive && GlitchTransition.Instance != null)
                yield return GlitchTransition.Instance.PlayRoutine();

            if (GameOverManager.IsGameOverActive)
                yield break;

            if (rbHad)
            {
                rb.position = respawnPoint.position;
                rb.rotation = respawnPoint.rotation;

                if (resetearVelocidad)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Physics.SyncTransforms();
            }
            else
            {
                root.position = respawnPoint.position;
                root.rotation = respawnPoint.rotation;
                Physics.SyncTransforms();
            }

            if (rbHad)
            {
                rb.isKinematic = rbWasKinematic;
                rb.useGravity = rbWasUsingGravity;

                if (resetearVelocidad)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            if (cc != null)
                cc.enabled = ccWasEnabled;
        }
        finally
        {
            _respawning = false;
        }
    }
}
