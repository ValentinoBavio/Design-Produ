using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LoadingGlitchTMP : MonoBehaviour
{
    [Header("Mensajes")]
    [Tooltip("Se muestra SOLO una vez al iniciar.")]
    public string textoInicial = "ALLOCATING MEMORY... OK";

    [Tooltip("Este se repite para siempre (type + hold + repetir).")]
    public string textoBase = "LOADING ...";

    public bool usarUnscaledTime = true;

    [Header("Typewriter + Glitch")]
    public float delayPorLetra = 0.035f;

    [Range(0, 6)] public int pasosGlitchPorLetra = 2;
    public float delayGlitchPaso = 0.018f;

    [Tooltip("Caracteres para glitch. Evitá '<' '>' si usás RichText en tu TMP.")]
    public string caracteresGlitch = "▓▒░█/\\|=+*#@$%&?";

    [Header("Cursor █ (titila SIEMPRE, no mueve el texto)")]
    public char cursorChar = '█';
    public float blinkInterval = 0.12f;
    public float holdCursorSeg = 2.0f;

    [Header("Temblor tipo Inscryption")]
    public bool temblarDuranteEscritura = true;
    public bool temblarDuranteHold = true;

    public float temblorAmpPx = 1.4f;
    [Range(0f, 1f)] public float temblorExtraNervio = 0.35f;

    TextMeshProUGUI _tmp;
    Coroutine _co;

    bool _jitterOn = false;

    string _lastText = "";
    bool _cached = false;

    TMP_TextInfo _textInfo;
    Vector3[][] _originalVertices;
    Color32[][] _originalColors;

    bool _lastBlinkOn = true;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        // No dependemos de richtext para nada, así evitamos que el glitch rompa tags
        // (si vos necesitás richtext, lo podés volver a true).
        // _tmp.richText = false;
    }

    void OnEnable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = null;

        if (_tmp != null)
        {
            _tmp.ForceMeshUpdate();
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    IEnumerator Loop()
    {
        // 1) Mensaje inicial una vez
        if (!string.IsNullOrEmpty(textoInicial))
        {
            _jitterOn = temblarDuranteEscritura;
            yield return TypewriterGlitch(textoInicial);

            _jitterOn = temblarDuranteHold;
            yield return Hold(textoInicial, holdCursorSeg);

            yield return Wait(0.06f);
        }

        // 2) LOADING infinito
        while (true)
        {
            _jitterOn = temblarDuranteEscritura;
            yield return TypewriterGlitch(textoBase);

            _jitterOn = temblarDuranteHold;
            yield return Hold(textoBase, holdCursorSeg);

            _tmp.text = "";
            _lastText = "";
            _cached = false;
            yield return Wait(0.02f);
        }
    }

    IEnumerator TypewriterGlitch(string target)
    {
        target ??= "";
        string fijo = "";

        for (int i = 0; i < target.Length; i++)
        {
            char real = target[i];

            // flashes glitch antes de fijar la letra
            for (int g = 0; g < pasosGlitchPorLetra; g++)
            {
                char rand = PickGlitchChar();
                SetBaseText(fijo + rand);
                yield return Wait(delayGlitchPaso);
            }

            // fijar letra real
            fijo += real;
            SetBaseText(fijo);
            yield return Wait(delayPorLetra);
        }

        SetBaseText(target);
        yield return null;
    }

    IEnumerator Hold(string baseMsg, float seconds)
    {
        SetBaseText(baseMsg ?? "");
        yield return Wait(seconds);
    }

    void SetBaseText(string baseText)
    {
        // ✅ El cursor SIEMPRE está al final (layout fijo)
        _tmp.text = baseText + cursorChar;
    }

    float Now()
    {
        return usarUnscaledTime ? Time.unscaledTime : Time.time;
    }

    bool BlinkOnNow()
    {
        float period = Mathf.Max(0.02f, blinkInterval) * 2f;
        return Mathf.Repeat(Now(), period) < (period * 0.5f);
    }

    char PickGlitchChar()
    {
        if (string.IsNullOrEmpty(caracteresGlitch)) return '#';

        // Evitamos '<' '>' por si el TMP tiene richText activo
        for (int tries = 0; tries < 12; tries++)
        {
            int idx = Random.Range(0, caracteresGlitch.Length);
            char c = caracteresGlitch[idx];
            if (c == '<' || c == '>') continue;
            return c;
        }

        return '#';
    }

    IEnumerator Wait(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (usarUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }

    void LateUpdate()
    {
        if (_tmp == null) return;

        bool blinkOn = BlinkOnNow();

        // Cachear mesh base si cambió el texto
        if (_tmp.havePropertiesChanged || _lastText != _tmp.text || !_cached)
        {
            _tmp.ForceMeshUpdate();
            _textInfo = _tmp.textInfo;

            _originalVertices = new Vector3[_textInfo.meshInfo.Length][];
            _originalColors = new Color32[_textInfo.meshInfo.Length][];

            for (int i = 0; i < _textInfo.meshInfo.Length; i++)
            {
                var vSrc = _textInfo.meshInfo[i].vertices;
                _originalVertices[i] = new Vector3[vSrc.Length];
                System.Array.Copy(vSrc, _originalVertices[i], vSrc.Length);

                var cSrc = _textInfo.meshInfo[i].colors32;
                _originalColors[i] = new Color32[cSrc.Length];
                System.Array.Copy(cSrc, _originalColors[i], cSrc.Length);
            }

            _lastText = _tmp.text;
            _tmp.havePropertiesChanged = false;
            _cached = true;
        }

        if (!_cached) return;

        // Si no hay jitter y el blink no cambió, no hacemos nada
        if (!_jitterOn && blinkOn == _lastBlinkOn)
            return;

        int charCount = _textInfo.characterCount;
        if (charCount <= 0) return;

        // Restaurar vertices y colors originales
        for (int i = 0; i < _textInfo.meshInfo.Length; i++)
        {
            var vDst = _textInfo.meshInfo[i].vertices;
            var vSrc = _originalVertices[i];
            if (vSrc != null && vDst != null) System.Array.Copy(vSrc, vDst, vSrc.Length);

            var cDst = _textInfo.meshInfo[i].colors32;
            var cSrc = _originalColors[i];
            if (cSrc != null && cDst != null) System.Array.Copy(cSrc, cDst, cSrc.Length);
        }

        // Aplicar jitter tipo Inscryption (pero NO al cursor, que es el último char)
        if (_jitterOn && temblorAmpPx > 0.0001f)
        {
            float amp = temblorAmpPx;
            float nervio = Mathf.Lerp(1f, 1.6f, temblorExtraNervio);

            int cursorIndex = charCount - 1;

            for (int i = 0; i < charCount; i++)
            {
                if (i == cursorIndex) continue; // ✅ cursor fijo

                var ch = _textInfo.characterInfo[i];
                if (!ch.isVisible) continue;

                int mi = ch.materialReferenceIndex;
                int vi = ch.vertexIndex;

                float ox = Random.Range(-amp, amp) * nervio;
                float oy = Random.Range(-amp, amp) * nervio;
                Vector3 offset = new Vector3(ox, oy, 0);

                var verts = _textInfo.meshInfo[mi].vertices;
                verts[vi + 0] += offset;
                verts[vi + 1] += offset;
                verts[vi + 2] += offset;
                verts[vi + 3] += offset;
            }
        }

        // Cursor blink SIEMPRE (alpha del último carácter)
        {
            int cursorIndex = charCount - 1;
            var ch = _textInfo.characterInfo[cursorIndex];

            if (ch.isVisible)
            {
                int mi = ch.materialReferenceIndex;
                int vi = ch.vertexIndex;

                byte a = blinkOn ? (byte)255 : (byte)0;
                var cols = _textInfo.meshInfo[mi].colors32;

                Color32 c0 = cols[vi + 0]; c0.a = a; cols[vi + 0] = c0;
                Color32 c1 = cols[vi + 1]; c1.a = a; cols[vi + 1] = c1;
                Color32 c2 = cols[vi + 2]; c2.a = a; cols[vi + 2] = c2;
                Color32 c3 = cols[vi + 3]; c3.a = a; cols[vi + 3] = c3;
            }
        }

        // Subir a meshes (vertices + colors)
        for (int i = 0; i < _textInfo.meshInfo.Length; i++)
        {
            var mesh = _textInfo.meshInfo[i].mesh;
            mesh.vertices = _textInfo.meshInfo[i].vertices;
            mesh.colors32 = _textInfo.meshInfo[i].colors32;
            _tmp.UpdateGeometry(mesh, i);
        }

        _lastBlinkOn = blinkOn;
    }
}