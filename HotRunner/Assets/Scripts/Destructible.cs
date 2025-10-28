using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Destructible : MonoBehaviour
{
    [Header("Vida")]
    public float maxHealth = 50f;
    public float currentHealth = -1f;

    [Header("Referencias")]
    [Tooltip("Malla intacta (se desactiva al romper). Puede ser este mismo objeto o un hijo.")]
    public GameObject intactRoot;
    [Tooltip("Raíz con los 'chunks' fracturados (se activa al romper). Cada chunk debe tener Collider. Rigidbody se agrega por código si falta.")]
    public GameObject fracturedRoot;

    [Header("Fuerzas de ruptura")]
    [Tooltip("Impulso lineal aplicado a cada chunk en la dirección del impacto.")]
    public float perChunkImpulse = 4f;
    [Tooltip("Explosión radial desde el punto de impacto.")]
    public float explosionForce = 120f;
    public float explosionRadius = 2.5f;
    public float explosionUpwards = 0.25f;

    [Header("Post ruptura")]
    public bool disableCollidersOnIntact = true;
    public bool addRigidbodiesIfMissing = true;
    public bool useGravityOnChunks = true;
    public float chunkDrag = 0.2f;
    public float chunkAngularDrag = 0.2f;
    [Tooltip("Si > 0, destruye automáticamente cada chunk tras este tiempo.")]
    public float chunkLifetime = 6f;

    [Header("Sonido/VFX (opcional)")]
    public AudioSource oneShot;
    public AudioClip breakSfx;
    public GameObject breakVfxPrefab;
    public float breakVfxLife = 2f;

    bool _broken;

    void Reset()
    {
        if (currentHealth < 0f) currentHealth = maxHealth;
        // Detección automática básica
        if (!intactRoot) intactRoot = this.gameObject;
        if (!fracturedRoot)
        {
            // Buscar un hijo llamado "Fractured" si existe
            var t = transform.Find("Fractured");
            if (t) fracturedRoot = t.gameObject;
        }
    }

    void Awake()
    {
        if (currentHealth < 0f) currentHealth = maxHealth;

        if (!intactRoot) intactRoot = this.gameObject;

        // Al iniciar: intacto ON, fracturado OFF
        if (intactRoot) intactRoot.SetActive(true);
        if (fracturedRoot) fracturedRoot.SetActive(false);

        // (opcional) Desactivar colliders del intacto para que no se dupliquen con los del padre
        if (disableCollidersOnIntact && intactRoot)
        {
            foreach (var col in intactRoot.GetComponentsInChildren<Collider>(true))
                col.enabled = true; // normalmente se dejan ON para colisionar mientras está intacto
        }
    }

    // ------------- API de daño -------------
    // Mínima: solo daño
    public void ApplyDamage(float damage)
    {
        ApplyDamage(damage, transform.position, Vector3.zero);
    }

    // Completa: daño + punto + impulso (dirección * magnitud)
    public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitImpulse)
    {
        if (_broken) return;

        currentHealth -= Mathf.Max(0f, damage);
        if (currentHealth > 0f) return;

        BreakNow(hitPoint, hitImpulse);
    }

    // Para compatibilidad con firmas alternativas (por si llamás con normal)
    public void ApplyDamageAtPoint(float damage, Vector3 hitPoint)
    {
        ApplyDamage(damage, hitPoint, Vector3.zero);
    }

    // ------------- Ruptura -------------
    public void BreakNow(Vector3 hitPoint, Vector3 hitImpulse)
    {
        if (_broken) return;
        _broken = true;

        // VFX/SFX
        if (breakVfxPrefab)
        {
            var vfx = Instantiate(breakVfxPrefab, hitPoint, Quaternion.identity);
            Destroy(vfx, Mathf.Max(0.1f, breakVfxLife));
        }
        if (oneShot && breakSfx) oneShot.PlayOneShot(breakSfx, 1f);

        // Toggle visual
        if (intactRoot) intactRoot.SetActive(false);
        if (fracturedRoot) fracturedRoot.SetActive(true);

        // Preparar chunks
        if (fracturedRoot)
        {
            var chunks = fracturedRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in chunks)
            {
                if (t == fracturedRoot.transform) continue;

                // Collider
                var col = t.GetComponent<Collider>();
                if (!col)
                {
                    // opcional: auto-collider si faltaba
                    col = t.gameObject.AddComponent<MeshCollider>();
                    (col as MeshCollider).convex = true;
                }
                col.enabled = true;

                // Rigidbody
                var rb = t.GetComponent<Rigidbody>();
                if (!rb && addRigidbodiesIfMissing)
                    rb = t.gameObject.AddComponent<Rigidbody>();

                if (rb)
                {
                    rb.useGravity = useGravityOnChunks;
                    rb.drag = chunkDrag;
                    rb.angularDrag = chunkAngularDrag;

                    // Impulso base hacia el exterior
                    if (perChunkImpulse > 0f && hitImpulse != Vector3.zero)
                        rb.AddForce(hitImpulse.normalized * perChunkImpulse, ForceMode.Impulse);

                    // Fuerza de explosión radial desde el punto de impacto
                    if (explosionForce > 0f)
                        rb.AddExplosionForce(explosionForce, hitPoint, Mathf.Max(0.01f, explosionRadius), explosionUpwards, ForceMode.Impulse);
                }

                if (chunkLifetime > 0f)
                    Destroy(t.gameObject, chunkLifetime);
            }
        }

        // (Opcional) destruir el contenedor después de que caigan los chunks
        // Destroy(gameObject, Mathf.Max(0.1f, chunkLifetime));
    }
}