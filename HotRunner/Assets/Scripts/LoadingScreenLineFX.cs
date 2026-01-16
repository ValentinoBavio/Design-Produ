using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenLineFX : MonoBehaviour
{
    [Header("Dónde se dibuja (si queda null usa este mismo RectTransform)")]
    public RectTransform area;

    [Header("Timing")]
    public bool usarUnscaledTime = true;

    [Header("Live tweak (modificar en vivo)")]
    public bool rebuildEnVivo = true;
    [Tooltip("Rebuild cada X segundos como fallback (0 = solo cuando detecta cambios)")]
    public float rebuildInterval = 0f;

    [Header("Generar")]
    public bool generarArriba = true;
    public bool generarAbajo = true;

    [Header("Estilo general")]
    public Color colorLinea = new Color(0f, 0.9f, 1f, 1f);
    [Range(0f, 1f)] public float alphaBase = 0.22f;
    [Range(0f, 1f)] public float alphaPulse = 0.16f;
    public Vector2 pulseSpeedRange = new Vector2(0.7f, 1.3f);
    public float grosorPx = 3.0f;

    [Header("Runner (chispita)")]
    public bool usarRunner = true;
    public float runnerSizePx = 10f;

    [Header("Runner (forma achatada/estirada)")]
    [Tooltip("Activá esto para definir el runner con tamaño X/Y (ancho/alto) y poder 'achatarlo'.")]
    public bool runnerUsarSizeXY = false;
    public Vector2 runnerSizeXYPx = new Vector2(24f, 8f);

    [Range(0f, 1f)] public float runnerAlpha = 0.70f;
    public Vector2 runnerSpeedRange01 = new Vector2(0.10f, 0.22f);

    [Header("Auto (como tus amarillas)")]
    [Range(0f, 1f)] public float topY01 = 0.92f;
    [Range(0f, 1f)] public float bottomY01 = 0.08f;
    [Range(0f, 0.15f)] public float margenX01 = 0.02f;

    [Tooltip("Top: quiebre cerca izquierda. Bottom: quiebre cerca derecha.")]
    [Range(0.02f, 0.35f)] public float kinkFromEdge01 = 0.12f;

    public float slopeWidthPx = 180f;
    public float stepHeightPx = 24f;

    [Header("Manual (4 puntos 0..1: inicio, antes pendiente, despues pendiente, final)")]
    public bool usarPuntosManuales = false;
    public List<Vector2> puntosArriba01 = new List<Vector2>(); // 4 puntos
    public List<Vector2> puntosAbajo01 = new List<Vector2>();  // 4 puntos

    [Header("Puntos estáticos con pulse (opcional)")]
    public bool usarPuntosPulse = false;

    public enum PuntoForma
    {
        Cuadrado,
        CirculoSuave,
        CirculoDuro
    }

    [System.Serializable]
    public class PuntoPulseDef
    {
        [Tooltip("Posición en anchors 0..1 dentro del área")]
        public Vector2 anchor01 = new Vector2(0.5f, 0.5f);

        [Tooltip("Tamaño del punto (px). Si querés 'aplastarlo', poné distinto X/Y.")]
        public Vector2 sizePx = new Vector2(10f, 10f);

        [Header("Forma")]
        public PuntoForma forma = PuntoForma.Cuadrado;

        [Header("Pulse")]
        [Range(0f, 1f)] public float alphaBase = 0.10f;
        [Range(0f, 1f)] public float alphaPulse = 0.35f;
        [Range(0f, 1f)] public float scalePulseFrac = 0.15f;

        [Tooltip("Velocidad de pulse (si querés fijo, poné X=Y).")]
        public Vector2 speedRange = new Vector2(1.4f, 2.6f);

        [Tooltip("Si es true, la fase es random. Si no, usa 'phase'.")]
        public bool randomPhase = true;
        public float phase = 0f;
    }

    public List<PuntoPulseDef> puntosPulse = new List<PuntoPulseDef>();

    // ===== Interno =====
    private static Sprite s_whiteSprite;
    private static Sprite s_softCircle;
    private static Sprite s_hardCircle;

    private static Texture2D s_whiteTex;
    private static Texture2D s_softTex;
    private static Texture2D s_hardTex;

    private RectTransform _root;
    private bool _built;

    private readonly List<MesaLine> _lines = new List<MesaLine>();
    private readonly List<PuntoPulse> _pulsePoints = new List<PuntoPulse>();

    private float _nextRebuild;
    private int _lastHash;

    void Awake()
    {
        if (!Application.isPlaying) return;

        _root = area ? area : GetComponent<RectTransform>();
        if (_root == null) _root = gameObject.AddComponent<RectTransform>();

        // Stretch full
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0.5f);

        EnsureSprites();
        StartCoroutine(CoBuildNextFrame());
    }

    void OnValidate()
    {
        // Para que al tocar valores en Play Mode se vea instantáneo (sin esperar al Update)
        if (!Application.isPlaying) return;
        if (!_built) return;
        if (!rebuildEnVivo) return;

        if (_root == null)
            _root = area ? area : GetComponent<RectTransform>();

        int h = CalcHash();
        if (h != _lastHash)
        {
            _lastHash = h;
            Rebuild();
        }
    }

    private IEnumerator CoBuildNextFrame()
    {
        yield return null;
        Rebuild();
        _built = true;

        _lastHash = CalcHash();
        _nextRebuild = Now() + Mathf.Max(0f, rebuildInterval);

        transform.SetAsLastSibling();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!_built) return;

        // Rebuild en vivo si cambian parámetros (o por interval fallback)
        if (rebuildEnVivo)
        {
            int h = CalcHash();
            bool changed = (h != _lastHash);

            bool timeHit = false;
            if (rebuildInterval > 0f)
                timeHit = Now() >= _nextRebuild;

            if (changed || timeHit)
            {
                _lastHash = h;
                if (rebuildInterval > 0f)
                    _nextRebuild = Now() + rebuildInterval;

                Rebuild();
            }
        }

        float t = usarUnscaledTime ? Time.unscaledTime : Time.time;
        float dt = usarUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        for (int i = 0; i < _lines.Count; i++)
            _lines[i].Tick(t, dt);

        for (int i = 0; i < _pulsePoints.Count; i++)
            _pulsePoints[i].Tick(t);
    }

    float Now() => usarUnscaledTime ? Time.unscaledTime : Time.time;

    public void ForceRebuildNow()
    {
        if (!Application.isPlaying) return;
        if (_root == null) _root = area ? area : GetComponent<RectTransform>();
        _lastHash = CalcHash();
        Rebuild();
    }

    void OnDestroy()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _lines.Clear();
        _pulsePoints.Clear();
    }

    public void Rebuild()
    {
        if (_root == null) _root = area ? area : GetComponent<RectTransform>();
        if (_root == null) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _lines.Clear();
        _pulsePoints.Clear();

        if (generarArriba)
        {
            var pts = GetTopPointsLocal();
            _lines.Add(CreateMesaLine("Top", pts[0], pts[1], pts[2], pts[3]));
        }

        if (generarAbajo)
        {
            var pts = GetBottomPointsLocal();
            _lines.Add(CreateMesaLine("Bottom", pts[0], pts[1], pts[2], pts[3]));
        }

        // Puntos estáticos con pulse (los ponés donde quieras)
        if (usarPuntosPulse && puntosPulse != null && puntosPulse.Count > 0)
        {
            for (int i = 0; i < puntosPulse.Count; i++)
            {
                if (puntosPulse[i] == null) continue;
                _pulsePoints.Add(CreatePulsePoint("PulsePoint_" + i, puntosPulse[i]));
            }
        }
    }

    // ---------- HASH (detecta cambios del inspector) ----------
    int CalcHash()
    {
        unchecked
        {
            int h = 17;

            h = h * 31 + rebuildEnVivo.GetHashCode();
            h = h * 31 + rebuildInterval.GetHashCode();

            h = h * 31 + generarArriba.GetHashCode();
            h = h * 31 + generarAbajo.GetHashCode();

            h = h * 31 + usarUnscaledTime.GetHashCode();

            h = h * 31 + usarRunner.GetHashCode();
            h = h * 31 + runnerSizePx.GetHashCode();

            h = h * 31 + runnerUsarSizeXY.GetHashCode();
            h = h * 31 + runnerSizeXYPx.x.GetHashCode();
            h = h * 31 + runnerSizeXYPx.y.GetHashCode();

            h = h * 31 + runnerAlpha.GetHashCode();
            h = h * 31 + runnerSpeedRange01.x.GetHashCode();
            h = h * 31 + runnerSpeedRange01.y.GetHashCode();

            h = h * 31 + grosorPx.GetHashCode();
            h = h * 31 + alphaBase.GetHashCode();
            h = h * 31 + alphaPulse.GetHashCode();
            h = h * 31 + pulseSpeedRange.x.GetHashCode();
            h = h * 31 + pulseSpeedRange.y.GetHashCode();

            h = h * 31 + colorLinea.r.GetHashCode();
            h = h * 31 + colorLinea.g.GetHashCode();
            h = h * 31 + colorLinea.b.GetHashCode();
            h = h * 31 + colorLinea.a.GetHashCode();

            h = h * 31 + usarPuntosManuales.GetHashCode();

            h = h * 31 + topY01.GetHashCode();
            h = h * 31 + bottomY01.GetHashCode();
            h = h * 31 + margenX01.GetHashCode();
            h = h * 31 + kinkFromEdge01.GetHashCode();
            h = h * 31 + slopeWidthPx.GetHashCode();
            h = h * 31 + stepHeightPx.GetHashCode();

            if (usarPuntosManuales)
            {
                if (puntosArriba01 != null)
                {
                    h = h * 31 + puntosArriba01.Count.GetHashCode();
                    for (int i = 0; i < puntosArriba01.Count; i++) h = h * 31 + puntosArriba01[i].GetHashCode();
                }
                if (puntosAbajo01 != null)
                {
                    h = h * 31 + puntosAbajo01.Count.GetHashCode();
                    for (int i = 0; i < puntosAbajo01.Count; i++) h = h * 31 + puntosAbajo01[i].GetHashCode();
                }
            }

            // puntos pulse
            h = h * 31 + usarPuntosPulse.GetHashCode();
            if (usarPuntosPulse && puntosPulse != null)
            {
                h = h * 31 + puntosPulse.Count.GetHashCode();
                for (int i = 0; i < puntosPulse.Count; i++)
                {
                    var p = puntosPulse[i];
                    if (p == null) { h = h * 31 + 0; continue; }
                    h = h * 31 + p.anchor01.GetHashCode();
                    h = h * 31 + p.sizePx.GetHashCode();
                    h = h * 31 + p.forma.GetHashCode();
                    h = h * 31 + p.alphaBase.GetHashCode();
                    h = h * 31 + p.alphaPulse.GetHashCode();
                    h = h * 31 + p.scalePulseFrac.GetHashCode();
                    h = h * 31 + p.speedRange.GetHashCode();
                    h = h * 31 + p.randomPhase.GetHashCode();
                    h = h * 31 + p.phase.GetHashCode();
                }
            }

            return h;
        }
    }

    // ---------- PUNTOS ----------
    Vector2[] GetTopPointsLocal()
    {
        if (usarPuntosManuales && puntosArriba01 != null && puntosArriba01.Count >= 4)
        {
            return new Vector2[]
            {
                LocalFrom01(puntosArriba01[0]),
                LocalFrom01(puntosArriba01[1]),
                LocalFrom01(puntosArriba01[2]),
                LocalFrom01(puntosArriba01[3]),
            };
        }

        // AUTO: meseta (y base) -> pendiente (sube) -> meseta (y alta)
        float x0 = margenX01;
        float x3 = 1f - margenX01;

        float yBase = topY01;
        Vector2 p0 = LocalFrom01(new Vector2(x0, yBase));
        Vector2 p3 = LocalFrom01(new Vector2(x3, yBase));

        float totalLen = Mathf.Abs(p3.x - p0.x);
        totalLen = Mathf.Max(10f, totalLen);

        float slopeW = Mathf.Clamp(slopeWidthPx, 20f, totalLen * 0.45f);
        float xSlopeStart = p0.x + totalLen * kinkFromEdge01;

        Vector2 p1 = new Vector2(xSlopeStart, p0.y);
        Vector2 p2 = new Vector2(xSlopeStart + slopeW, p0.y + Mathf.Abs(stepHeightPx));
        Vector2 p3b = new Vector2(p3.x, p0.y + Mathf.Abs(stepHeightPx));

        return new Vector2[] { p0, p1, p2, p3b };
    }

    Vector2[] GetBottomPointsLocal()
    {
        if (usarPuntosManuales && puntosAbajo01 != null && puntosAbajo01.Count >= 4)
        {
            return new Vector2[]
            {
                LocalFrom01(puntosAbajo01[0]),
                LocalFrom01(puntosAbajo01[1]),
                LocalFrom01(puntosAbajo01[2]),
                LocalFrom01(puntosAbajo01[3]),
            };
        }

        // AUTO: meseta (y base) -> pendiente (BAJA) cerca derecha -> meseta (y baja)
        float x0 = margenX01;
        float x3 = 1f - margenX01;

        float yBase = bottomY01;
        Vector2 p0 = LocalFrom01(new Vector2(x0, yBase));
        Vector2 p3 = LocalFrom01(new Vector2(x3, yBase));

        float totalLen = Mathf.Abs(p3.x - p0.x);
        totalLen = Mathf.Max(10f, totalLen);

        float slopeW = Mathf.Clamp(slopeWidthPx, 20f, totalLen * 0.45f);

        float xSlopeStart = p3.x - totalLen * kinkFromEdge01 - slopeW;

        Vector2 p1 = new Vector2(xSlopeStart, p0.y);
        Vector2 p2 = new Vector2(xSlopeStart + slopeW, p0.y - Mathf.Abs(stepHeightPx)); // ✅ baja
        Vector2 p3b = new Vector2(p3.x, p0.y - Mathf.Abs(stepHeightPx));

        return new Vector2[] { p0, p1, p2, p3b };
    }

    // ---------- LINEA (3 segmentos) ----------
    MesaLine CreateMesaLine(string prefix, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        Segment segA = CreateSegment(prefix + "_A", p0, p1);
        Segment segB = CreateSegment(prefix + "_B", p1, p2);
        Segment segC = CreateSegment(prefix + "_C", p2, p3);

        Image runnerImg = null;
        RectTransform runnerRT = null;

        if (usarRunner)
        {
            GameObject go = NewUI(prefix + "_Runner");
            runnerRT = go.GetComponent<RectTransform>();
            runnerImg = go.GetComponent<Image>();
            runnerImg.sprite = s_softCircle;
            runnerImg.raycastTarget = false;

            Vector2 baseSize = runnerUsarSizeXY ? runnerSizeXYPx : (Vector2.one * runnerSizePx);
            runnerRT.sizeDelta = baseSize;
        }

        float speed = Random.Range(pulseSpeedRange.x, pulseSpeedRange.y);
        float phase = Random.value * 10f;
        float runnerSpeed = Random.Range(runnerSpeedRange01.x, runnerSpeedRange01.y);

        return new MesaLine(
            segA, segB, segC,
            p0, p1, p2, p3,
            Mathf.Max(0.5f, grosorPx),
            colorLinea,
            Mathf.Clamp01(alphaBase),
            Mathf.Clamp01(alphaPulse),
            speed, phase,
            runnerImg, runnerRT,
            Mathf.Clamp01(runnerAlpha),
            Mathf.Clamp01(runnerSpeed)
        );
    }

    // ---------- PUNTO PULSE ----------
    PuntoPulse CreatePulsePoint(string name, PuntoPulseDef def)
    {
        GameObject go = NewUI(name);
        RectTransform rt = go.GetComponent<RectTransform>();
        Image img = go.GetComponent<Image>();

        switch (def.forma)
        {
            case PuntoForma.CirculoDuro:  img.sprite = s_hardCircle; break;
            case PuntoForma.CirculoSuave: img.sprite = s_softCircle; break;
            default:                      img.sprite = s_whiteSprite; break;
        }

        img.raycastTarget = false;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        rt.anchoredPosition = LocalFrom01(def.anchor01);
        rt.sizeDelta = def.sizePx;

        Color c0 = colorLinea;
        c0.a = Mathf.Clamp01(def.alphaBase);
        img.color = c0;

        go.transform.SetAsLastSibling();

        float sp = Random.Range(def.speedRange.x, def.speedRange.y);
        float ph = def.randomPhase ? (Random.value * 10f) : def.phase;

        return new PuntoPulse(rt, img, colorLinea,
            Mathf.Clamp01(def.alphaBase),
            Mathf.Clamp01(def.alphaPulse),
            Mathf.Clamp01(def.scalePulseFrac),
            sp, ph);
    }

    Segment CreateSegment(string name, Vector2 a, Vector2 b)
    {
        GameObject go = NewUI(name);
        RectTransform rt = go.GetComponent<RectTransform>();
        Image img = go.GetComponent<Image>();

        img.sprite = s_whiteSprite;
        img.raycastTarget = false;

        Vector2 dir = (b - a);
        float len = dir.magnitude;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(len, grosorPx);
        rt.localRotation = Quaternion.Euler(0, 0, ang);

        return new Segment(rt, img);
    }

    GameObject NewUI(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        return go;
    }

    Vector2 LocalFrom01(Vector2 p01)
    {
        Rect r = _root.rect;
        float x = (p01.x - 0.5f) * r.width;
        float y = (p01.y - 0.5f) * r.height;
        return new Vector2(x, y);
    }

    static void EnsureSprites()
    {
        if (s_whiteSprite == null)
        {
            s_whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            s_whiteTex.SetPixel(0, 0, Color.white);
            s_whiteTex.Apply(false, true);
            s_whiteSprite = Sprite.Create(s_whiteTex, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 100f);
        }

        if (s_softCircle == null)
        {
            const int W = 64;
            const int H = 64;
            s_softTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            s_softTex.wrapMode = TextureWrapMode.Clamp;

            Vector2 c = new Vector2((W - 1) * 0.5f, (H - 1) * 0.5f);
            float maxR = Mathf.Min(W, H) * 0.5f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 2.2f);
                    s_softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            s_softTex.Apply(false, true);
            s_softCircle = Sprite.Create(s_softTex, new Rect(0, 0, W, H),
                new Vector2(0.5f, 0.5f), 100f);
        }

        if (s_hardCircle == null)
        {
            const int W = 64;
            const int H = 64;
            s_hardTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            s_hardTex.wrapMode = TextureWrapMode.Clamp;

            Vector2 c = new Vector2((W - 1) * 0.5f, (H - 1) * 0.5f);
            float radius = Mathf.Min(W, H) * 0.5f - 1f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = (d <= radius) ? 1f : 0f;
                    s_hardTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            s_hardTex.Apply(false, true);
            s_hardCircle = Sprite.Create(s_hardTex, new Rect(0, 0, W, H),
                new Vector2(0.5f, 0.5f), 100f);
        }
    }

    class Segment
    {
        public RectTransform rt;
        public Image img;

        public Segment(RectTransform rt, Image img) { this.rt = rt; this.img = img; }

        public void SetThickness(float px)
        {
            if (!rt) return;
            Vector2 s = rt.sizeDelta;
            s.y = px;
            rt.sizeDelta = s;
        }

        public void SetColor(Color c)
        {
            if (!img) return;
            img.color = c;
        }
    }

    class MesaLine
    {
        Segment a, b, c;
        Vector2 p0, p1, p2, p3;

        float thickBase;
        Color col;
        float alphaBase;
        float alphaPulse;
        float speed;
        float phase;

        Image runnerImg;
        RectTransform runnerRT;
        Vector2 runnerBaseSize;
        float runnerAlpha;
        float runnerSpeed01;
        float runnerT;

        float len01, len12, len23, totalLen;

        public MesaLine(
            Segment a, Segment b, Segment c,
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float thickBase,
            Color col, float alphaBase, float alphaPulse,
            float speed, float phase,
            Image runnerImg, RectTransform runnerRT,
            float runnerAlpha, float runnerSpeed01)
        {
            this.a = a; this.b = b; this.c = c;
            this.p0 = p0; this.p1 = p1; this.p2 = p2; this.p3 = p3;

            this.thickBase = thickBase;
            this.col = col;
            this.alphaBase = alphaBase;
            this.alphaPulse = alphaPulse;
            this.speed = speed;
            this.phase = phase;

            this.runnerImg = runnerImg;
            this.runnerRT = runnerRT;
            this.runnerAlpha = runnerAlpha;
            this.runnerSpeed01 = runnerSpeed01;

            if (runnerRT) runnerBaseSize = runnerRT.sizeDelta;

            len01 = (p1 - p0).magnitude;
            len12 = (p2 - p1).magnitude;
            len23 = (p3 - p2).magnitude;
            totalLen = Mathf.Max(0.0001f, len01 + len12 + len23);

            runnerT = Random.value;
        }

        public void Tick(float t, float dt)
        {
            float s = Mathf.Sin((t * speed) + phase) * 0.5f + 0.5f;

            float aA = Mathf.Clamp01(alphaBase + (alphaPulse * s));
            float thick = Mathf.Max(0.5f, thickBase * (0.85f + 0.35f * s));

            Color cc = col; cc.a = aA;

            a.SetColor(cc); b.SetColor(cc); c.SetColor(cc);
            a.SetThickness(thick); b.SetThickness(thick); c.SetThickness(thick);

            if (runnerImg && runnerRT)
            {
                runnerT = Mathf.Repeat(runnerT + dt * runnerSpeed01, 1f);
                runnerRT.anchoredPosition = PointOnPolyline01(runnerT);

                float rr = Mathf.Sin(t * (speed * 2.2f) + phase) * 0.5f + 0.5f;
                Color rc = col; rc.a = Mathf.Clamp01(runnerAlpha * (0.55f + rr * 0.45f));
                runnerImg.color = rc;

                float k = 0.9f + rr * 0.35f;
                runnerRT.sizeDelta = runnerBaseSize * k;
            }
        }

        Vector2 PointOnPolyline01(float u01)
        {
            float d = u01 * totalLen;

            if (d <= len01) return Vector2.Lerp(p0, p1, d / Mathf.Max(0.0001f, len01));
            d -= len01;

            if (d <= len12) return Vector2.Lerp(p1, p2, d / Mathf.Max(0.0001f, len12));
            d -= len12;

            return Vector2.Lerp(p2, p3, d / Mathf.Max(0.0001f, len23));
        }
    }

    class PuntoPulse
    {
        RectTransform rt;
        Image img;

        Color col;
        float aBase, aPulse;
        float scalePulseFrac;
        float speed, phase;

        Vector2 baseSize;

        public PuntoPulse(RectTransform rt, Image img, Color col, float aBase, float aPulse, float scalePulseFrac, float speed, float phase)
        {
            this.rt = rt;
            this.img = img;
            this.col = col;
            this.aBase = aBase;
            this.aPulse = aPulse;
            this.scalePulseFrac = scalePulseFrac;
            this.speed = speed;
            this.phase = phase;

            if (rt) baseSize = rt.sizeDelta;
        }

        public void Tick(float t)
        {
            if (!img || !rt) return;

            float s = Mathf.Sin(t * speed + phase) * 0.5f + 0.5f;
            float pulse = Mathf.SmoothStep(0f, 1f, s);

            float a = Mathf.Clamp01(aBase + aPulse * pulse);
            Color c = col; c.a = a;
            img.color = c;

            float k = 1f + scalePulseFrac * (pulse - 0.5f) * 2f;
            rt.sizeDelta = baseSize * k;
        }
    }
}