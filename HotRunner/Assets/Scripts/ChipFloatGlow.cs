using UnityEngine;

[DisallowMultipleComponent]
public class ChipFloatGlow : MonoBehaviour
{
    [Header("CHIP (opcional)")]
    public bool floatChip = true;
    public float floatAmplitude = 0.15f;
    public float floatFrequency = 1.2f;

    public bool spinChip = true;
    public Vector3 chipSpinSpeed = new Vector3(0f, 110f, 0f);

    [Header("ORBIT RING (MESH 3D - sin planos)")]
    public bool enableOrbitRing = true;

    [Tooltip("Radio base del anillo (centro del tubo).")]
    public float ringRadius = 0.55f;

    [Tooltip("Radio del tubo (grosor 3D del anillo).")]
    public float tubeRadius = 0.03f;

    [Range(24, 256)] public int ringSegments = 96;
    [Range(6, 32)] public int tubeSegments = 12;

    [Header("ORBITA")]
    public Vector3 orbitTiltEuler = new Vector3(25f, 0f, 0f);
    public Vector3 orbitPrecessionSpeed = new Vector3(25f, 90f, 10f);
    public float ringSpinInPlane = 80f;

    [Header("CORONA (puntas)")]
    [Range(4, 80)] public int crownSpikes = 24;
    public float crownDepthMin = 0.02f;
    public float crownDepthMax = 0.12f;
    public float crownPulseSpeed = 1.2f;
    [Range(1f, 6f)] public float crownSharpness = 2.4f;
    public float crownTravelSpeed = 0.6f;

    [Header("MATERIAL / SHADER (Rainbow + Noise)")]
    [Tooltip("Arrastrá acá el material del Shader Graph (SG_RingRainbowNoise). Si está vacío, usa fallback.")]
    public Material ringMaterialOverride;

    [Tooltip("Tinte multiplicador (si tu shader usa _Tint).")]
    public Color ringTint = Color.white;

    [Range(0f, 1f)]
    public float ringAlpha = 0.6f;  // _Alpha

    [Tooltip("Brillo/emisión (si tu shader usa _Emission).")]
    public float emission = 2.0f;   // _Emission

    [Tooltip("Velocidad del arcoíris (si tu shader usa _HueSpeed).")]
    public float hueSpeed = 0.35f;  // _HueSpeed

    [Tooltip("Escala del ruido (si tu shader usa _NoiseScale).")]
    public float noiseScale = 6.0f; // _NoiseScale

    [Tooltip("Fuerza del ruido sobre el color (si tu shader usa _NoiseStrength).")]
    public float noiseStrength = 0.25f; // _NoiseStrength

    [Tooltip("Scroll del ruido (si tu shader usa _NoiseScroll).")]
    public float noiseScroll = 0.5f; // _NoiseScroll

    // -------- runtime --------
    Vector3 _startLocalPos;
    float _phase;

    Transform _rig;
    Transform _pivot;

    MeshFilter _mf;
    MeshRenderer _mr;
    Mesh _mesh;

    Vector3[] _verts;
    Vector3[] _normals;
    Vector2[] _uvs;
    int[] _tris;

    int _cachedRingSeg;
    int _cachedTubeSeg;
    bool _cachedEnable;

    static Material s_fallbackMat;
    static MaterialPropertyBlock s_mpb;

    const string RIG_NAME_A = "OrbitRingRig";
    const string RIG_NAME_B = "OrbitalRing";

    void Start()
    {
        _startLocalPos = transform.localPosition;
        _phase = Random.value * 10f;

        EnsureFallback();
        RebuildIfNeeded(force: true);
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        EnsureFallback();
        RebuildIfNeeded(force: false);

        float t = Time.time + _phase;

        if (floatChip)
        {
            float y = Mathf.Sin(t * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
            transform.localPosition = _startLocalPos + Vector3.up * y;
        }

        if (spinChip)
            transform.Rotate(chipSpinSpeed * Time.deltaTime, Space.Self);

        if (!enableOrbitRing || _pivot == null || _mesh == null)
            return;

        _pivot.localRotation = Quaternion.Euler(orbitTiltEuler);
        _pivot.Rotate(orbitPrecessionSpeed * Time.deltaTime, Space.Self);
        _pivot.Rotate(0f, ringSpinInPlane * Time.deltaTime, 0f, Space.Self);

        float pulse01 = (Mathf.Sin(t * crownPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float crownDepth = Mathf.Lerp(crownDepthMin, crownDepthMax, pulse01);

        UpdateTorusMesh(t, crownDepth);
        ApplyShaderParams();
    }

    void RebuildIfNeeded(bool force)
    {
        if (_cachedEnable != enableOrbitRing || _cachedRingSeg != ringSegments || _cachedTubeSeg != tubeSegments)
            force = true;

        _cachedEnable = enableOrbitRing;
        _cachedRingSeg = ringSegments;
        _cachedTubeSeg = tubeSegments;

        if (!force) return;

        CleanupOldRigs();

        if (!enableOrbitRing) return;

        _rig = new GameObject(RIG_NAME_A).transform;
        _rig.SetParent(transform, false);
        _rig.localPosition = Vector3.zero;
        _rig.localRotation = Quaternion.identity;

        _pivot = new GameObject("OrbitRingPivot").transform;
        _pivot.SetParent(_rig, false);
        _pivot.localPosition = Vector3.zero;
        _pivot.localRotation = Quaternion.identity;

        _mf = _pivot.gameObject.AddComponent<MeshFilter>();
        _mr = _pivot.gameObject.AddComponent<MeshRenderer>();

        // material
        _mr.sharedMaterial = ringMaterialOverride != null ? ringMaterialOverride : s_fallbackMat;

        _mesh = new Mesh();
        _mesh.name = "OrbitRing_Torus";
        _mesh.MarkDynamic();
        _mf.sharedMesh = _mesh;

        BuildTorusTopology();
        UpdateTorusMesh(Time.time + _phase, crownDepthMin);
        ApplyShaderParams();
    }

    void CleanupOldRigs()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (ch.name == RIG_NAME_A || ch.name == RIG_NAME_B)
                Destroy(ch.gameObject);
        }

        _rig = null; _pivot = null; _mf = null; _mr = null; _mesh = null;
        _verts = null; _normals = null; _uvs = null; _tris = null;
    }

    void BuildTorusTopology()
    {
        int rs = Mathf.Clamp(ringSegments, 24, 256);
        int ts = Mathf.Clamp(tubeSegments, 6, 32);

        int vCount = (rs + 1) * (ts + 1);
        _verts = new Vector3[vCount];
        _normals = new Vector3[vCount];
        _uvs = new Vector2[vCount];

        int triCount = rs * ts * 2 * 3;
        _tris = new int[triCount];

        int tri = 0;
        for (int r = 0; r < rs; r++)
        {
            for (int s = 0; s < ts; s++)
            {
                int i0 = (r * (ts + 1)) + s;
                int i1 = i0 + 1;
                int i2 = i0 + (ts + 1);
                int i3 = i2 + 1;

                _tris[tri++] = i0; _tris[tri++] = i2; _tris[tri++] = i1;
                _tris[tri++] = i1; _tris[tri++] = i2; _tris[tri++] = i3;
            }
        }

        _mesh.Clear(false);
        _mesh.vertices = _verts;
        _mesh.normals = _normals;
        _mesh.uv = _uvs;
        _mesh.triangles = _tris;
    }

    void UpdateTorusMesh(float t, float crownDepth)
    {
        int rs = Mathf.Clamp(ringSegments, 24, 256);
        int ts = Mathf.Clamp(tubeSegments, 6, 32);

        if (_verts == null || _verts.Length != (rs + 1) * (ts + 1))
            return;

        float twoPi = Mathf.PI * 2f;

        float travel = t * crownTravelSpeed * twoPi;
        int spikes = Mathf.Max(1, crownSpikes);
        float sharp = Mathf.Max(1f, crownSharpness);

        int idx = 0;
        for (int r = 0; r <= rs; r++)
        {
            float ur = r / (float)rs;
            float theta = ur * twoPi;

            float wave = 0.5f + 0.5f * Mathf.Sin(theta * spikes + travel);
            wave = Mathf.Pow(wave, sharp);

            float R = ringRadius + wave * crownDepth;

            Vector3 radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            Vector3 center = radial * R;

            for (int s = 0; s <= ts; s++)
            {
                float us = s / (float)ts;
                float phi = us * twoPi;

                float cp = Mathf.Cos(phi);
                float sp = Mathf.Sin(phi);

                Vector3 normal = (radial * cp + Vector3.up * sp).normalized;
                Vector3 pos = center + normal * tubeRadius;

                _verts[idx] = pos;
                _normals[idx] = normal;

                // UV: X recorre el aro (ur), Y recorre el tubo (us)
                _uvs[idx] = new Vector2(ur, us);

                idx++;
            }
        }

        _mesh.vertices = _verts;
        _mesh.normals = _normals;
        _mesh.uv = _uvs;
        _mesh.RecalculateBounds();
    }

    void EnsureFallback()
    {
        if (s_fallbackMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            s_fallbackMat = new Material(sh);
            s_fallbackMat.name = "ChipFloatGlow_Fallback_SHARED";
        }

        if (s_mpb == null)
            s_mpb = new MaterialPropertyBlock();
    }

    void ApplyShaderParams()
    {
        if (_mr == null) return;

        // si cambiaste material en Play, actualiza
        Material targetMat = ringMaterialOverride != null ? ringMaterialOverride : s_fallbackMat;
        if (_mr.sharedMaterial != targetMat)
            _mr.sharedMaterial = targetMat;

        // set params (si el shader no los usa, no pasa nada)
        s_mpb.Clear();

        s_mpb.SetColor("_Tint", ringTint);
        s_mpb.SetColor("_Color", new Color(ringTint.r, ringTint.g, ringTint.b, ringAlpha)); // fallback

        s_mpb.SetFloat("_Alpha", ringAlpha);
        s_mpb.SetFloat("_Emission", emission);

        s_mpb.SetFloat("_HueSpeed", hueSpeed);
        s_mpb.SetFloat("_NoiseScale", noiseScale);
        s_mpb.SetFloat("_NoiseStrength", noiseStrength);
        s_mpb.SetFloat("_NoiseScroll", noiseScroll);

        _mr.SetPropertyBlock(s_mpb);
    }
}
