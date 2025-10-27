using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BulletProjectile : MonoBehaviour
{
    [Header("Vida")]
    public float life = 3f;

    [Header("Trail (se crea/ajusta en runtime)")]
    public Material trailMaterial;     // Unlit/Transparent recomendado (opcional)
    public float trailTime = 0.18f;    // duración visible
    public float minVertexDistance = 0.04f;
    public AnimationCurve trailWidth = AnimationCurve.EaseInOut(0, 0.16f, 1, 0.0f);
    public Gradient trailColor;

    [Header("Colisión (opcional)")]
    public LayerMask hitMask = ~0;     // si querés filtrar impactos
    public GameObject impactVfxPrefab; // opcional

    TrailRenderer tr;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureTrail();
    }

    void OnEnable()
    {
        // limpiar rastro anterior si se reusa el prefab
        if (tr) tr.Clear();
        if (life > 0f) Destroy(gameObject, life);
    }

    void EnsureTrail()
    {
        tr = GetComponent<TrailRenderer>();
        if (!tr) tr = gameObject.AddComponent<TrailRenderer>();

        tr.time = Mathf.Max(0.01f, trailTime);
        tr.minVertexDistance = Mathf.Max(0.005f, minVertexDistance);
        tr.widthCurve = trailWidth != null ? trailWidth : AnimationCurve.EaseInOut(0, 0.16f, 1, 0f);
        tr.colorGradient = (trailColor != null && trailColor.colorKeys.Length > 0)
            ? trailColor
            : DefaultGradient();

        tr.alignment = LineAlignment.View;               // siempre “de cara” a la cámara
        tr.textureMode = LineTextureMode.Stretch;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.numCornerVertices = 4;
        tr.numCapVertices = 2;

        // Material
        if (trailMaterial)
        {
            tr.material = trailMaterial;
        }
        else
        {
            // Material fallback (unlit blanco)
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 3000;
            tr.material = mat;
        }
    }

    Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f,0.75f,0.35f), 0.12f),
                new GradientColorKey(new Color(0.6f,0.8f,1f), 0.6f),
                new GradientColorKey(new Color(0.2f,0.4f,1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.12f),
                new GradientAlphaKey(0.35f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return g;
    }

    void OnCollisionEnter(Collision c)
    {
        // Impacto opcional + destrucción
        if (((1 << c.gameObject.layer) & hitMask) != 0)
        {
            if (impactVfxPrefab)
            {
                var v = Instantiate(impactVfxPrefab, c.contacts[0].point,
                                    Quaternion.LookRotation(c.contacts[0].normal));
                Destroy(v, 2f);
            }
        }
        Destroy(gameObject);
    }
}