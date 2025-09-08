using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrappleCrosshair : MonoBehaviour
{
    [Header("Refs (opcional)")]
    public GrappleGun grapple;
    public Transform firePointFallback;

    [Header("Raycast (si no asignás 'grapple')")]
    public LayerMask grappleMask = ~0;
    public float    maxGrappleDistance = 90f;
    public KeyCode  grappleKey = KeyCode.E;

    [Header("Estilo de la cruz")]
    public float length = 14f;
    public float thickness = 3f;
    public float gap = 6f;
    public Color colorIdle  = Color.white;
    public Color colorValid = Color.green;
    public Color colorHeld  = new Color(1f, 0.85f, 0.2f);

    [Header("Opcional: escalado por resolución")]
    public bool scaleWithHeight = true;
    public float referenceHeight = 1080f;

    static Texture2D _tex;

    void OnGUI()
    {
        if (_tex == null)
        {
            _tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        Transform fireT;
        LayerMask mask;
        float maxDist;

        if (grapple != null)
        {
            fireT  = grapple.firePoint != null ? grapple.firePoint : (Camera.main ? Camera.main.transform : transform);
            mask   = grapple.grappleMask;
            maxDist = grapple.maxGrappleDistance;
            grappleKey = grapple.grappleKey;
        }
        else
        {
            fireT  = firePointFallback != null ? firePointFallback : (Camera.main ? Camera.main.transform : transform);
            mask   = grappleMask;
            maxDist = maxGrappleDistance;
        }

        bool hasHit = false;
        if (fireT != null)
            hasHit = Physics.Raycast(fireT.position, fireT.forward, out _, maxDist, mask, QueryTriggerInteraction.Ignore);

        Color col = colorIdle;
        if (Input.GetKey(grappleKey))
            col = hasHit ? colorHeld : colorIdle;
        else if (hasHit)
            col = colorValid;

        float scale = 1f;
        if (scaleWithHeight && referenceHeight > 0f)
            scale = Screen.height / referenceHeight;

        float L = Mathf.Round(length   * scale);
        float T = Mathf.Round(thickness* scale);
        float G = Mathf.Round(gap      * scale);

        float cx = Mathf.Round(Screen.width * 0.5f);
        float cy = Mathf.Round(Screen.height * 0.5f);

        GUI.color = col;

        GUI.DrawTexture(new Rect(cx - T * 0.5f, cy - G - L, T, L), _tex);

        GUI.DrawTexture(new Rect(cx - T * 0.5f, cy + G,     T, L), _tex);

        GUI.DrawTexture(new Rect(cx - G - L,   cy - T*0.5f, L, T), _tex);

        GUI.DrawTexture(new Rect(cx + G,       cy - T*0.5f, L, T), _tex);

        GUI.color = Color.white;
    }
}