using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectile : MonoBehaviour
{
    [Header("Vida")]
    public float life = 3f;

    [Header("Trail")]
    public float trailTime = 0.25f;
    public float minVertexDistance = 0.02f;
    public AnimationCurve trailWidth = AnimationCurve.EaseInOut(0, 0.2f, 1, 0.02f);
    public Gradient trailColor;
    public Material trailMaterial;
    [Range(0,8)] public int trailCornerVerts = 2;
    [Range(0,8)] public int trailCapVerts    = 2;

    [Header("Impacto")]
    public GameObject impactVfxPrefab;
    public float impactVfxLife = 1.0f;
    public bool destroyOnImpact = true;

    TrailRenderer tr;
    Rigidbody rb;
    Collider col;
    float dieAt;

    void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if (col) col.isTrigger = false;

        // Configurar/crear TrailRenderer
        tr = GetComponent<TrailRenderer>();
        if (!tr) tr = gameObject.AddComponent<TrailRenderer>();

        tr.time = Mathf.Max(0.05f, trailTime);
        tr.minVertexDistance = Mathf.Max(0.001f, minVertexDistance);
        tr.widthCurve = trailWidth;

        if (trailColor != null && trailColor.colorKeys != null && trailColor.colorKeys.Length > 0)
            tr.colorGradient = trailColor;

        if (!trailMaterial)
        {
            // Material por defecto para ver el trail YA (unlit)
            var sh = Shader.Find("Sprites/Default");
            if (sh) trailMaterial = new Material(sh);
        }
        if (trailMaterial) tr.material = trailMaterial;

        tr.numCornerVertices = trailCornerVerts;
        tr.numCapVertices    = trailCapVerts;
        tr.alignment = LineAlignment.View;
        tr.receiveShadows = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.emitting = true; // importante: emite mientras la bala vive
    }

    void OnEnable()
    {
        dieAt = Time.time + Mathf.Max(0.05f, life);
    }

    void Update()
    {
        if (Time.time >= dieAt)
            StartCoroutine(KillAfterTrailDries());
    }

    void OnCollisionEnter(Collision c)
    {
        Vector3 pos = transform.position;
        Vector3 nrm = -transform.forward;
        if (c.contactCount > 0) { pos = c.contacts[0].point; nrm = c.contacts[0].normal; }
        SpawnImpact(pos, nrm);

        if (destroyOnImpact)
            StartCoroutine(KillAfterTrailDries());
    }

    void OnTriggerEnter(Collider _)
    {
        SpawnImpact(transform.position, -transform.forward);
        if (destroyOnImpact)
            StartCoroutine(KillAfterTrailDries());
    }

    void SpawnImpact(Vector3 pos, Vector3 normal)
    {
        if (!impactVfxPrefab) return;
        var v = Instantiate(impactVfxPrefab, pos, Quaternion.LookRotation(normal));
        Destroy(v, Mathf.Max(0.1f, impactVfxLife));
    }

    IEnumerator KillAfterTrailDries()
    {
        // Detener física/colisiones pero dejar el trail visible
        if (rb) { rb.isKinematic = true; rb.velocity = Vector3.zero; }
        if (col) col.enabled = false;

        // Cortar emisión y esperar a que se “seque” el rastro
        if (tr)
        {
            tr.emitting = false;
            yield return new WaitForSeconds(tr.time);
        }

        Destroy(gameObject);
    }
}