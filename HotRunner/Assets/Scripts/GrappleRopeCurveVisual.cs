using UnityEngine;

[DisallowMultipleComponent]
public class GrappleRopeCurveVisual : MonoBehaviour
{
    public enum RopeStyle { Arc, SCurve, Ripple, Random3 }

    [Header("Refs")]
    public GrappleGun grapple;
    public LineRenderer lineRenderer;

    [Header("Segments")]
    [Range(6, 80)] public int segments = 28;

    [Header("Curve (Diagonal like reference)")]
    [Tooltip("Cuánta curva por metro (en mundo).")]
    public float curvePerMeter = 0.06f;
    [Tooltip("Límite máximo de curva (metros).")]
    public float curveMax = 0.55f;
    [Tooltip("Suavizado de la curva (más = responde más rápido).")]
    public float curveSmooth = 12f;

    [Header("Diagonal bias (0 = horizontal puro, 1 = vertical puro)")]
    [Range(0f, 1f)] public float diagonalBias = 0.35f;

    [Header("3 estilos (se elige por cast)")]
    public RopeStyle style = RopeStyle.Random3;
    [Tooltip("Si querés que siempre curve a la misma mano, dejalo en 0. Si no, randomiza izquierda/derecha.")]
    public int forcedSide = 0; // -1 = izquierda, +1 = derecha, 0 = random

    [Header("Tension (cuando succiona queda recta)")]
    [Tooltip("Velocidad para volverse recta al succionar.")]
    public float tensionIn = 18f;
    [Tooltip("Velocidad para volver a doblarse cuando NO succiona.")]
    public float tensionOut = 7f;

    [Header("Stability (anti gelatina)")]
    [Tooltip("Suaviza el start/end para matar el micro jitter (más alto = más estable).")]
    public float anchorSmooth = 22f;

    [Tooltip("Si está Attached, baja la ondulación para que no parezca gelatina.")]
    public bool dampRippleWhenAttached = true;

    [Tooltip("Multiplicador de ripple cuando está Attached (0 = sin ripple).")]
    [Range(0f, 1f)] public float attachedRippleMultiplier = 0.15f;

    [Tooltip("Si está Attached, suaviza aún más start/end (extra estabilidad).")]
    public float attachedExtraSmooth = 10f;

    [Header("Retract al soltar (visual)")]
    public bool enableRetract = true;
    public float retractDuration = 0.25f;

    [Header("Texture tiling (no stretch)")]
    public bool fixTextureTiling = true;
    [Tooltip("Cuántos metros de cuerda por 1 repetición de textura.")]
    public float tileWorldUnits = 0.35f;

    [Header("LineRenderer quality")]
    public int cornerVertices = 8;
    public int endCapVertices = 8;
    public LineAlignment alignment = LineAlignment.View;
    public bool forceTileMode = true;

    // ---------- runtime ----------
    RopeStyle _activeStyle;
    float _sideSign = 1f;

    float _curveCurrent;
    float _tension01;

    bool _prevActive;
    Vector3 _lastStart, _lastEnd;

    // start/end suavizados
    Vector3 _startSmoothed, _endSmoothed;
    bool _smoothInit = false;

    // ripple time suavizado (para que se “apague”)
    float _rippleTime;

    // retract
    LineRenderer _retractLR;
    bool _retracting;
    float _retractT;
    Vector3 _retractEndFrom;

    void Reset()
    {
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        if (!grapple) grapple = GetComponent<GrappleGun>();
    }

    void Awake()
    {
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        if (!grapple) grapple = GetComponent<GrappleGun>();

        SetupLine(lineRenderer);

        if (enableRetract)
            CreateRetractRenderer();
    }

    void SetupLine(LineRenderer lr)
    {
        if (!lr) return;

        lr.useWorldSpace = true;
        lr.numCornerVertices = Mathf.Max(0, cornerVertices);
        lr.numCapVertices = Mathf.Max(0, endCapVertices);
        lr.alignment = alignment;

        if (forceTileMode)
            lr.textureMode = LineTextureMode.Tile;
    }

    void CreateRetractRenderer()
    {
        if (_retractLR) return;

        var go = new GameObject("RopeRetractVisual");
        go.transform.SetParent(transform, false);

        _retractLR = go.AddComponent<LineRenderer>();

        _retractLR.sharedMaterial = lineRenderer ? lineRenderer.sharedMaterial : null;
        _retractLR.widthMultiplier = lineRenderer ? lineRenderer.widthMultiplier : 0.02f;
        _retractLR.widthCurve = lineRenderer ? lineRenderer.widthCurve : AnimationCurve.Linear(0, 0.02f, 1, 0.02f);
        _retractLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _retractLR.receiveShadows = false;

        SetupLine(_retractLR);
        _retractLR.enabled = false;
        _retractLR.positionCount = 2;
    }

    void Update()
    {
        if (!lineRenderer) return;

        bool activeNow = lineRenderer.enabled;

        // Guardar último start/end válido mientras estuvo activo
        if (activeNow && lineRenderer.positionCount >= 2)
        {
            _lastStart = lineRenderer.GetPosition(0);
            _lastEnd = lineRenderer.GetPosition(1);

            if (!_smoothInit)
            {
                _startSmoothed = _lastStart;
                _endSmoothed = _lastEnd;
                _smoothInit = true;
            }
        }
        else
        {
            _smoothInit = false;
        }

        // Arranque: elegir estilo/side
        if (activeNow && !_prevActive)
        {
            PickStyleAndSide();
            _retracting = false;
            if (_retractLR) _retractLR.enabled = false;

            _rippleTime = 0f; // reset para que no “salte”
        }

        // Si se apagó este frame => retract visual
        if (!activeNow && _prevActive && enableRetract)
        {
            BeginRetract();
        }

        _prevActive = activeNow;

        // Tensión: Attached + mantener tecla (succión)
        bool suction = false;
        bool attached = false;
        if (grapple != null)
        {
            attached = grapple.IsAttached;
            suction = attached && Input.GetKey(grapple.grappleKey);
        }

        float targetTension = suction ? 1f : 0f;
        float speed = suction ? tensionIn : tensionOut;
        _tension01 = Mathf.Lerp(_tension01, targetTension, 1f - Mathf.Exp(-speed * Time.deltaTime));

        // RippleTime: si está attached, lo “congelamos” lentamente para que deje de jigglear
        if (_activeStyle == RopeStyle.Ripple && dampRippleWhenAttached && attached)
        {
            // avanza re lento
            _rippleTime += Time.deltaTime * 0.25f;
        }
        else
        {
            _rippleTime += Time.deltaTime;
        }
    }

    void LateUpdate()
    {
        // Retract visual
        if (_retracting && _retractLR)
        {
            DrawRetract();
            return;
        }

        // Rope normal mientras GrappleGun la tenga activa
        if (!lineRenderer || !lineRenderer.enabled || lineRenderer.positionCount < 2)
            return;

        bool attached = (grapple != null && grapple.IsAttached);

        // Suavizar start/end para matar vibración
        Vector3 startRaw = lineRenderer.GetPosition(0);
        Vector3 endRaw = lineRenderer.GetPosition(1);

        float s = anchorSmooth + (attached ? attachedExtraSmooth : 0f);
        float a = 1f - Mathf.Exp(-Mathf.Max(0f, s) * Time.deltaTime);

        if (!_smoothInit)
        {
            _startSmoothed = startRaw;
            _endSmoothed = endRaw;
            _smoothInit = true;
        }
        else
        {
            _startSmoothed = Vector3.Lerp(_startSmoothed, startRaw, a);
            _endSmoothed = Vector3.Lerp(_endSmoothed, endRaw, a);
        }

        DrawCurved(lineRenderer, _startSmoothed, _endSmoothed, suctionTension: _tension01, attached: attached);
    }

    void PickStyleAndSide()
    {
        if (style == RopeStyle.Random3)
        {
            int r = Random.Range(0, 3);
            _activeStyle = (r == 0) ? RopeStyle.Arc : (r == 1) ? RopeStyle.SCurve : RopeStyle.Ripple;
        }
        else
        {
            _activeStyle = style;
        }

        int s;
        if (forcedSide == 0) s = (Random.value < 0.5f) ? -1 : 1;
        else s = (forcedSide < 0) ? -1 : 1;

        _sideSign = (float)s;
    }

    void BeginRetract()
    {
        if (!_retractLR) return;

        _retracting = true;
        _retractT = 0f;

        _retractEndFrom = _lastEnd;
        _retractLR.enabled = true;
    }

    void DrawRetract()
    {
        _retractT += Time.deltaTime;
        float p = (retractDuration <= 0.001f) ? 1f : Mathf.Clamp01(_retractT / retractDuration);

        Vector3 start = ComputeOriginLikeGrapple();
        Vector3 end = Vector3.Lerp(_retractEndFrom, start, Smooth01(p));

        float retractTension = Mathf.Lerp(1f, 0f, Smooth01(p));

        DrawCurved(_retractLR, start, end, suctionTension: retractTension, attached: false);

        if (p >= 0.999f)
        {
            _retracting = false;
            _retractLR.enabled = false;
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

    void DrawCurved(LineRenderer lr, Vector3 start, Vector3 end, float suctionTension, bool attached)
    {
        if (!lr) return;

        if (fixTextureTiling && tileWorldUnits > 0.001f)
        {
            float dist = Vector3.Distance(start, end);
            float tiles = Mathf.Max(0.01f, dist / tileWorldUnits);

            if (forceTileMode) lr.textureMode = LineTextureMode.Tile;
            lr.textureScale = new Vector2(tiles, 1f);
        }

        int seg = Mathf.Max(2, segments);
        if (lr.positionCount != seg) lr.positionCount = seg;

        Vector3 dir = end - start;
        float distLine = dir.magnitude;
        Vector3 fwd = (distLine > 1e-6f) ? (dir / distLine) : Vector3.forward;

        Transform fp = (grapple != null) ? grapple.firePoint : null;

        Vector3 right = fp ? fp.right : Vector3.right;
        Vector3 up = fp ? fp.up : Vector3.up;

        Vector3 axis = Vector3.Lerp(right, up, diagonalBias);
        axis = Vector3.ProjectOnPlane(axis, fwd);

        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(fwd, Vector3.up);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
        axis.Normalize();

        float targetCurve = Mathf.Min(curveMax, distLine * curvePerMeter);
        _curveCurrent = Mathf.Lerp(_curveCurrent, targetCurve, 1f - Mathf.Exp(-curveSmooth * Time.deltaTime));

        float curveAmount = _curveCurrent * (1f - Mathf.Clamp01(suctionTension));

        // si está attached, apagamos la “onda” para que no sea gelatina
        float rippleMul = 1f;
        if (dampRippleWhenAttached && attached && _activeStyle == RopeStyle.Ripple)
            rippleMul = attachedRippleMultiplier;

        for (int i = 0; i < seg; i++)
        {
            float t = (float)i / (seg - 1);
            Vector3 p = Vector3.Lerp(start, end, t);

            float bell = 4f * t * (1f - t);

            float shape = 0f;
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
                    // ahora usa tiempo filtrado y multiplicador
                    float w = Mathf.Sin((t * 7.0f) + (_rippleTime * 2.2f));
                    shape = _sideSign * (0.55f + 0.45f * w) * rippleMul;
                    break;
            }

            p += axis * (curveAmount * bell * shape);
            lr.SetPosition(i, p);
        }
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
