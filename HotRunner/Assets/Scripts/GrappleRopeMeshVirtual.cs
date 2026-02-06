using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GrappleRopeMeshVisual : MonoBehaviour
{
    public enum RopeStyle { Arc, SCurve, Ripple, Random3 }

    [Header("Refs")]
    public GrappleGun grapple;
    public LineRenderer sourceLine; // el LineRenderer que usa GrappleGun (solo data)

    [Header("Mesh")]
    [Range(6, 80)] public int pathSegments = 28;      // puntos a lo largo
    [Range(3, 24)] public int radialSegments = 12;    // lados del tubito
    public float radius = 0.012f;                     // grosor real

    [Header("UV / Tiling")]
    [Tooltip("Metros por 1 repetición de textura a lo largo.")]
    public float tileWorldUnits = 0.35f;
    public float vTiling = 1f;

    [Header("Curva (diagonal tipo Halo)")]
    public float curvePerMeter = 0.06f;
    public float curveMax = 0.55f;
    public float curveSmooth = 12f;

    [Header("Diagonal bias (0=horizontal, 1=vertical)")]
    [Range(0f, 1f)] public float diagonalBias = 0.35f;

    [Header("3 estilos")]
    public RopeStyle style = RopeStyle.Random3;
    public int forcedSide = 0; // 0 random, -1 izq, +1 der

    [Header("Tensión (succión = recta)")]
    public float tensionIn = 18f;
    public float tensionOut = 7f;

    [Header("Retract al soltar (visual)")]
    public bool enableRetract = true;
    public float retractDuration = 0.25f;

    [Header("Ocultar LineRenderer (RECOMENDADO)")]
    public bool hideSourceLine = true;

    // -------- runtime --------
    MeshFilter _mf;
    Mesh _mesh;

    Vector3[] _path;
    float _curveCurrent;
    float _tension01;

    RopeStyle _activeStyle;
    float _sideSign = 1f;

    bool _prevActive;
    Vector3 _lastStart, _lastEnd;

    bool _retracting;
    float _retractT;
    Vector3 _retractEndFrom;

    // cache para evitar GC
    int _cachedSeg = -1, _cachedRing = -1;
    Vector3[] _verts;
    Vector3[] _norms;
    Vector2[] _uvs;
    int[] _tris;

    bool _cachedForceOff;
    bool _forceOffWasCached = false;

    void Reset()
    {
        if (!grapple) grapple = GetComponentInParent<GrappleGun>();
        if (!sourceLine) sourceLine = GetComponentInParent<LineRenderer>();
    }

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        if (!grapple) grapple = GetComponentInParent<GrappleGun>();
        if (!sourceLine) sourceLine = GetComponentInParent<LineRenderer>();

        _mesh = new Mesh();
        _mesh.name = "GrappleRopeMesh";
        _mesh.MarkDynamic();
        _mf.sharedMesh = _mesh;

        EnsurePathArray();
        EnsureBuffers();
    }

    void OnDisable()
    {
        RestoreLineRendererRenderState();
        SetMeshVisible(false);
    }

    void Update()
    {
        if (!sourceLine) return;

        bool activeNow = sourceLine.enabled;

        // Guardar último start/end válido
        if (activeNow && sourceLine.positionCount >= 2)
        {
            _lastStart = sourceLine.GetPosition(0);
            _lastEnd = sourceLine.GetPosition(1);
        }

        // inicio de cast
        if (activeNow && !_prevActive)
        {
            PickStyleAndSide();
            _retracting = false;
        }

        // se apagó => retract
        if (!activeNow && _prevActive && enableRetract)
        {
            BeginRetract();
        }

        _prevActive = activeNow;

        // succión = Attached + mantener la tecla
        bool suction = false;
        if (grapple != null)
            suction = grapple.IsAttached && Input.GetKey(grapple.grappleKey);

        float target = suction ? 1f : 0f;
        float spd = suction ? tensionIn : tensionOut;
        _tension01 = Mathf.Lerp(_tension01, target, 1f - Mathf.Exp(-spd * Time.deltaTime));

        // 🔥 esto es lo que te saca la “cinta gigante”
        if (hideSourceLine)
            ForceHideLineRenderer();
    }

    void LateUpdate()
    {
        EnsurePathArray();
        EnsureBuffers();

        if (!sourceLine)
        {
            SetMeshVisible(false);
            return;
        }

        if (_retracting)
        {
            DrawRetractMesh();
            return;
        }

        if (!sourceLine.enabled || sourceLine.positionCount < 2)
        {
            SetMeshVisible(false);
            return;
        }

        Vector3 start = sourceLine.GetPosition(0);
        Vector3 end   = sourceLine.GetPosition(1);

        BuildPath(start, end, _tension01);
        BuildTubeMesh(_path);

        SetMeshVisible(true);
    }

    void ForceHideLineRenderer()
    {
        if (!sourceLine) return;

        // cache para restaurar después
        if (!_forceOffWasCached)
        {
            _cachedForceOff = sourceLine.forceRenderingOff;
            _forceOffWasCached = true;
        }

        sourceLine.forceRenderingOff = true; // ✅ sigue funcionando pero no se dibuja
    }

    void RestoreLineRendererRenderState()
    {
        if (!sourceLine) return;
        if (!_forceOffWasCached) return;

        sourceLine.forceRenderingOff = _cachedForceOff;
        _forceOffWasCached = false;
    }

    void SetMeshVisible(bool on)
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr && mr.enabled != on) mr.enabled = on;
    }

    void PickStyleAndSide()
    {
        if (style == RopeStyle.Random3)
        {
            int r = Random.Range(0, 3);
            _activeStyle = (r == 0) ? RopeStyle.Arc : (r == 1) ? RopeStyle.SCurve : RopeStyle.Ripple;
        }
        else _activeStyle = style;

        int s;
        if (forcedSide == 0) s = (Random.value < 0.5f) ? -1 : 1;
        else s = (forcedSide < 0) ? -1 : 1;

        _sideSign = s;
    }

    void BeginRetract()
    {
        _retracting = true;
        _retractT = 0f;
        _retractEndFrom = _lastEnd; // último end real
    }

    void DrawRetractMesh()
    {
        _retractT += Time.deltaTime;
        float p = (retractDuration <= 0.001f) ? 1f : Mathf.Clamp01(_retractT / retractDuration);

        Vector3 start = ComputeOriginLikeGrapple();
        Vector3 end   = Vector3.Lerp(_retractEndFrom, start, Smooth01(p));

        // al soltar: arranca tensa y se va doblando mientras vuelve
        float retractTension = Mathf.Lerp(1f, 0f, Smooth01(p));

        BuildPath(start, end, retractTension);
        BuildTubeMesh(_path);
        SetMeshVisible(true);

        if (p >= 0.999f)
        {
            _retracting = false;
            SetMeshVisible(false);
        }
    }

    Vector3 ComputeOriginLikeGrapple()
    {
        if (grapple != null && grapple.firePoint != null)
        {
            return grapple.firePoint.position
                 + grapple.firePoint.right * grapple.fireRightOffset
                 + grapple.firePoint.forward * grapple.castStartOffset;
        }
        return transform.position;
    }

    void EnsurePathArray()
    {
        int seg = Mathf.Max(2, pathSegments);
        if (_path == null || _path.Length != seg)
            _path = new Vector3[seg];
    }

    void EnsureBuffers()
    {
        int seg = Mathf.Max(2, pathSegments);
        int ring = Mathf.Max(3, radialSegments);

        if (seg == _cachedSeg && ring == _cachedRing && _verts != null) return;

        _cachedSeg = seg;
        _cachedRing = ring;

        int vertCount = seg * ring;
        int triCount = (seg - 1) * ring * 6;

        _verts = new Vector3[vertCount];
        _norms = new Vector3[vertCount];
        _uvs   = new Vector2[vertCount];
        _tris  = new int[triCount];

        // Triángulos (topología fija)
        int ti = 0;
        for (int i = 0; i < seg - 1; i++)
        {
            int row0 = i * ring;
            int row1 = (i + 1) * ring;

            for (int j = 0; j < ring; j++)
            {
                int a = row0 + j;
                int b = row0 + ((j + 1) % ring);
                int c = row1 + j;
                int d = row1 + ((j + 1) % ring);

                _tris[ti++] = a; _tris[ti++] = c; _tris[ti++] = b;
                _tris[ti++] = b; _tris[ti++] = c; _tris[ti++] = d;
            }
        }
    }

    void BuildPath(Vector3 start, Vector3 end, float suctionTension)
    {
        Vector3 dir = end - start;
        float distLine = dir.magnitude;
        Vector3 fwd = (distLine > 1e-6f) ? (dir / distLine) : Vector3.forward;

        // eje diagonal (right+up) y perpendicular a la cuerda
        Transform fp = (grapple != null) ? grapple.firePoint : null;
        Vector3 right = fp ? fp.right : Vector3.right;
        Vector3 up    = fp ? fp.up    : Vector3.up;

        Vector3 axis = Vector3.Lerp(right, up, diagonalBias);
        axis = Vector3.ProjectOnPlane(axis, fwd);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(fwd, Vector3.up);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
        axis.Normalize();

        float targetCurve = Mathf.Min(curveMax, distLine * curvePerMeter);
        _curveCurrent = Mathf.Lerp(_curveCurrent, targetCurve, 1f - Mathf.Exp(-curveSmooth * Time.deltaTime));

        float curveAmount = _curveCurrent * (1f - Mathf.Clamp01(suctionTension));
        float time = Time.time;

        int seg = _path.Length;
        for (int i = 0; i < seg; i++)
        {
            float t = (float)i / (seg - 1);
            Vector3 p = Vector3.Lerp(start, end, t);

            float bell = 4f * t * (1f - t); // 0 extremos, 1 medio

            float shape;
            switch (_activeStyle)
            {
                default:
                case RopeStyle.Arc:
                    shape = _sideSign;
                    break;

                case RopeStyle.SCurve:
                    shape = Mathf.Sin((t - 0.5f) * Mathf.PI) * 1.15f;
                    shape *= _sideSign;
                    break;

                case RopeStyle.Ripple:
                    float w = Mathf.Sin((t * 7.0f) + (time * 2.2f));
                    shape = _sideSign * (0.55f + 0.45f * w);
                    break;
            }

            _path[i] = p + axis * (curveAmount * bell * shape);
        }
    }

    void BuildTubeMesh(Vector3[] centers)
    {
        int seg = centers.Length;
        int ring = Mathf.Max(3, radialSegments);

        // dist acumulada para UV
        float distU = 0f;
        float uScale = (tileWorldUnits > 0.001f) ? (1f / tileWorldUnits) : 1f;

        for (int i = 0; i < seg; i++)
        {
            Vector3 tng;
            if (i == 0) tng = (centers[1] - centers[0]).normalized;
            else if (i == seg - 1) tng = (centers[seg - 1] - centers[seg - 2]).normalized;
            else tng = (centers[i + 1] - centers[i - 1]).normalized;

            if (tng.sqrMagnitude < 1e-6f) tng = Vector3.forward;

            Transform fp = (grapple != null) ? grapple.firePoint : null;
            Vector3 up = fp ? fp.up : Vector3.up;
            up = Vector3.ProjectOnPlane(up, tng);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, tng);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.right;
            up.Normalize();

            Vector3 right = Vector3.Cross(tng, up).normalized;

            if (i > 0) distU += Vector3.Distance(centers[i - 1], centers[i]);
            float u = distU * uScale;

            for (int j = 0; j < ring; j++)
            {
                float a = (j / (float)ring) * Mathf.PI * 2f;
                Vector3 n = Mathf.Cos(a) * right + Mathf.Sin(a) * up;

                int idx = i * ring + j;
                _verts[idx] = centers[i] + n * radius;
                _norms[idx] = n;

                float v = (j / (float)ring) * vTiling;
                _uvs[idx] = new Vector2(u, v);
            }
        }

        _mesh.Clear(false);
        _mesh.vertices = _verts;
        _mesh.normals = _norms;
        _mesh.uv = _uvs;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
