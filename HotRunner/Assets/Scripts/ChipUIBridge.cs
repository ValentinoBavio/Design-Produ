using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChipUIBridge : MonoBehaviour
{
    [Header("Setup")]
    public Canvas canvasDestino;
    public RectTransform rawImageParent;
    public Vector2 uiSize = new Vector2(96, 96);
    public int rtSize = 256;
    public string layerUI3D = "UI3D";

    [Header("Cámara")]
    [Range(-10, 60)] public float tiltGrados = 20f;
    public float fov = 40f;
    public Color bgColor = new Color(0, 0, 0, 0);

    [Header("Idle")]
    public bool usarFlotacion = true;
    public float bobAmplitude = 0.07f;
    public float bobFreq = 1.6f;
    public bool usarRotacion = true;
    public float velRotY = 60f;

    [Header("Pulse (cuando lo agarrás)")]
    public float pulseEscala = 1.25f;
    public float pulseRecuperacion = 10f;

    [Header("Opcional")]
    public string rawName = "Raw_Chip";
    public bool destruirRawAlDestruir = true;

    Camera camUI;
    RenderTexture rt;
    RawImage raw;

    Vector3 escalaInicial;
    Vector3 posInicialLocal;
    float multPulse = 1f;
    float t0;

    void Start()
    {
        if (!canvasDestino)
        {
            Debug.LogWarning("ChipUIBridge: asigná canvasDestino.");
            enabled = false;
            return;
        }

        int layer = LayerMask.NameToLayer(layerUI3D);
        if (layer < 0)
        {
            Debug.LogWarning("ChipUIBridge: no existe la capa '" + layerUI3D + "'. Creala (Add Layer) y reasigná.");
            enabled = false;
            return;
        }

        // IMPORTANTE: esto pone TODO este objeto en UI3D (usalo en un CHIP DUPLICADO sólo para UI)
        SetLayerRecursively(gameObject, layer);

        rt = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);
        rt.Create();

        GameObject goCam = new GameObject("Cam_UI3D_Chip");
        camUI = goCam.AddComponent<Camera>();
        camUI.clearFlags = CameraClearFlags.SolidColor;
        camUI.backgroundColor = bgColor;
        camUI.cullingMask = 1 << layer;
        camUI.orthographic = false;
        camUI.fieldOfView = fov;
        camUI.nearClipPlane = 0.05f;
        camUI.farClipPlane = 100f;
        camUI.targetTexture = rt;

        Bounds b = GetBoundsWorld(transform);
        Vector3 center = b.center;
        float radius = Mathf.Max(0.01f, b.extents.magnitude);

        Vector3 dir = Quaternion.Euler(tiltGrados, 0f, 0f) * Vector3.back;
        camUI.transform.position = center + dir.normalized * (radius * 2.5f);
        camUI.transform.LookAt(center, Vector3.up);

        GameObject goRaw = new GameObject(rawName, typeof(RectTransform), typeof(RawImage));
        goRaw.transform.SetParent(rawImageParent ? rawImageParent : canvasDestino.transform, false);

        RectTransform rtUI = goRaw.GetComponent<RectTransform>();
        rtUI.sizeDelta = uiSize;

        raw = goRaw.GetComponent<RawImage>();
        raw.texture = rt;

        escalaInicial = transform.localScale;
        posInicialLocal = transform.localPosition;
        t0 = Random.value * 10f;
    }

    void Update()
    {
        if (usarRotacion)
            transform.Rotate(0f, velRotY * Time.deltaTime, 0f, Space.Self);

        float bob = 0f;
        if (usarFlotacion)
            bob = Mathf.Sin((t0 + Time.time) * Mathf.PI * 2f * bobFreq) * bobAmplitude;

        float kRec = 1f - Mathf.Exp(-pulseRecuperacion * Time.deltaTime);
        multPulse = Mathf.Lerp(multPulse, 1f, kRec);

        transform.localScale = escalaInicial * multPulse;

        Vector3 basePos = posInicialLocal + new Vector3(0f, bob, 0f);
        if (transform.localPosition != basePos)
            transform.localPosition = basePos;
    }

    // Llamalo cuando el player recolecta un chip
    public void Pulse()
    {
        multPulse = Mathf.Max(multPulse, Mathf.Max(1.01f, pulseEscala));
    }

    void OnDestroy()
    {
        if (camUI) Destroy(camUI.gameObject);

        if (rt)
        {
            rt.Release();
            Destroy(rt);
        }

        if (destruirRawAlDestruir && raw)
            Destroy(raw.gameObject);
    }

    static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            if (child) SetLayerRecursively(child.gameObject, newLayer);
    }

    static Bounds GetBoundsWorld(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(t.position, Vector3.one * 0.1f);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }
}