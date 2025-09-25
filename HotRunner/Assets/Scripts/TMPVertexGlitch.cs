using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPVertexGlitch : MonoBehaviour
{
    // ===================== TEMBLOR (vértices) =====================
    [Header("Temblor (vértices)")]
    public bool enableShake = true;
    public float posAmplitude = 0.75f; // magnitud del desplazamiento
    public float jaggedKick = 1.5f; // “patadas” discretas
    public int glitchFPS = 30; // veces/seg que actualiza
    public float noiseScale = 0.03f; // escala espacial del ruido
    public float noiseTimeScale = 0.07f; // velocidad temporal del ruido
    public float jaggedChancePerSecond = 6f; // prob. total de pataditas por seg

    // ===================== TEXTO (scramble/resolve) =====================
    [Header("Texto Cyberpunk")]
    public bool enableTextFX = true;

    [Tooltip("Si está vacío, usa el texto actual del TMP.")]
    [TextArea]
    public string defaultText;

    [Header("Scramble (basura)")]
    public float scrambleCharsPerSecond = 45f; // velocidad al “llenar”
    public float scrambleFlickerRate = 18f; // Hz para cambiar basura ya escrita
    public string scrambleAlphabet = "@#%&+*=?/\\-_<>|01IOXZ";

    [Header("Resolve (reescritura correcta)")]
    public float resolveCharsPerSecond = 120f;
    public bool settleFlickerOnResolve = true; // mini parpadeo antes de fijar el real
    public int settleFlickerCycles = 2;

    [Header("Cursor")]
    public string cursorChar = "█";
    public float cursorBlinkHz = 2.5f; // titilado por segundo
    public bool cursorDuringAllPhases = true; // cursor también durante scramble/resolve

    [Header("Visibilidad (señal del fade)")]
    [Range(0f, 1f)]
    public float appearThreshold = 0.6f;

    [Range(0f, 1f)]
    public float disappearThreshold = 0.2f;

    TextMeshProUGUI tmp;
    float nextTick;
    bool wasVisible;
    Coroutine textRoutine;

    struct Token
    {
        public bool isTag;
        public char ch;
        public string tag;
    }

    Token[] tokens;
    int visibleCount;
    string finalTextRaw;
    float cursorT;
    bool cursorOn;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        if (tmp)
            tmp.richText = true;

        if (!FontHasGlyph(tmp, cursorChar))
            cursorChar = "|";
    }

    void OnEnable()
    {
        CheckVisibilityAndMaybeTrigger();
    }

    void Update()
    {
        CheckVisibilityAndMaybeTrigger();

        if (enableShake && tmp && tmp.color.a >= 0.02f)
            TickVertexGlitch();
    }

    void CheckVisibilityAndMaybeTrigger()
    {
        if (tmp == null)
            return;

        float a = tmp.color.a;
        bool nowVisible = wasVisible ? (a > disappearThreshold) : (a > appearThreshold);

        if (nowVisible && !wasVisible)
        {
            if (enableTextFX)
                StartTextFX();
        }
        else if (!nowVisible && wasVisible)
        {
            if (textRoutine != null)
            {
                StopCoroutine(textRoutine);
                textRoutine = null;
            }
        }

        wasVisible = nowVisible;
    }

    void TickVertexGlitch()
    {
        if (Time.unscaledTime < nextTick)
            return;
        nextTick = Time.unscaledTime + 1f / Mathf.Max(1, glitchFPS);

        if (tmp.textInfo.characterCount == 0)
            return;

        tmp.ForceMeshUpdate();

        var info = tmp.textInfo;
        float dt = 1f / Mathf.Max(1, glitchFPS);
        float jaggedProbThisTick = jaggedChancePerSecond * dt;

        for (int mi = 0; mi < info.meshInfo.Length; mi++)
        {
            var meshInfo = info.meshInfo[mi];
            var verts = meshInfo.vertices;

            for (int c = 0; c < info.characterCount; c++)
            {
                var ch = info.characterInfo[c];
                if (!ch.isVisible || ch.materialReferenceIndex != mi)
                    continue;

                int vIdx = ch.vertexIndex;

                for (int j = 0; j < 4; j++)
                {
                    Vector3 v = verts[vIdx + j];

                    float n =
                        Mathf.PerlinNoise(
                            (v.x + c) * noiseScale,
                            (Time.unscaledTime + v.y) * noiseTimeScale
                        ) - 0.5f;

                    v += new Vector3(n * posAmplitude, -n * posAmplitude, 0f);

                    if (Random.value < jaggedProbThisTick)
                        v += new Vector3(
                            Random.Range(-jaggedKick, jaggedKick),
                            Random.Range(-jaggedKick, jaggedKick),
                            0
                        );

                    verts[vIdx + j] = v;
                }
            }

            meshInfo.mesh.vertices = verts;
            tmp.UpdateGeometry(meshInfo.mesh, mi);
        }
    }

    public void StartTextFX(string overrideText = null)
    {
        if (textRoutine != null)
            StopCoroutine(textRoutine);

        finalTextRaw =
            !string.IsNullOrEmpty(overrideText) ? overrideText
            : !string.IsNullOrEmpty(defaultText) ? defaultText
            : tmp.text;

        tokens = Tokenize(finalTextRaw, out visibleCount);
        cursorT = 0f;
        cursorOn = false;
        textRoutine = StartCoroutine(CoRunTextFX());
    }

    IEnumerator CoRunTextFX()
    {
        int writtenVisible = 0;
        float addStep = 1f / Mathf.Max(1f, scrambleCharsPerSecond);
        float nextAdd = Time.unscaledTime;
        float nextFlick = 0f;
        float flickStep = 1f / Mathf.Max(1f, scrambleFlickerRate);

        char[] visBuffer = new char[visibleCount];

        while (writtenVisible < visibleCount)
        {
            float now = Time.unscaledTime;

            if (now >= nextAdd)
            {
                visBuffer[writtenVisible] = RandScramble();
                writtenVisible++;
                nextAdd = now + addStep;
            }

            if (now >= nextFlick)
            {
                for (int i = 0; i < writtenVisible; i++)
                    visBuffer[i] = RandScramble();
                nextFlick = now + flickStep;
            }

            TickCursorBlink();
            tmp.text = ComposeWithBuffer(visBuffer, writtenVisible, cursorDuringAllPhases);
            yield return null;
        }

        float resStep = 1f / Mathf.Max(1f, resolveCharsPerSecond);
        int resolved = 0;
        float nextRes = Time.unscaledTime;

        while (resolved < visibleCount)
        {
            float now = Time.unscaledTime;

            if (now >= nextRes)
            {
                int idx = resolved;

                if (settleFlickerOnResolve && settleFlickerCycles > 0)
                {
                    for (int k = 0; k < settleFlickerCycles; k++)
                    {
                        visBuffer[idx] = RandScramble();
                        TickCursorBlink();
                        tmp.text = ComposeWithBuffer(
                            visBuffer,
                            visibleCount,
                            cursorDuringAllPhases
                        );
                        yield return new WaitForSecondsRealtime(resStep * 0.3f);
                    }
                }

                visBuffer[idx] = GetVisibleAt(idx);
                resolved++;
                nextRes = now + resStep;
            }

            TickCursorBlink();
            tmp.text = ComposeWithBuffer(
                visBuffer,
                Mathf.Max(resolved, visibleCount),
                cursorDuringAllPhases
            );
            yield return null;
        }

        while (true)
        {
            TickCursorBlink();
            tmp.text = finalTextRaw + TaggedCursor(cursorOn);
            yield return null;
        }
    }

    void TickCursorBlink()
    {
        cursorT += Time.unscaledDeltaTime * cursorBlinkHz * Mathf.PI * 2f;
        cursorOn = Mathf.Sin(cursorT) > 0f;
    }

    char RandScramble()
    {
        if (string.IsNullOrEmpty(scrambleAlphabet))
            return '#';
        int i = Random.Range(0, scrambleAlphabet.Length);
        return scrambleAlphabet[i];
    }

    static Token[] Tokenize(string s, out int visible)
    {
        var list = new List<Token>(s.Length);
        visible = 0;
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '<')
            {
                int j = s.IndexOf('>', i + 1);
                if (j >= 0)
                {
                    list.Add(new Token { isTag = true, tag = s.Substring(i, j - i + 1) });
                    i = j + 1;
                    continue;
                }
            }
            list.Add(new Token { isTag = false, ch = s[i] });
            visible++;
            i++;
        }
        return list.ToArray();
    }

    char GetVisibleAt(int vi)
    {
        int count = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!tokens[i].isTag)
            {
                if (count == vi)
                    return tokens[i].ch;
                count++;
            }
        }
        return ' ';
    }

    string ComposeWithBuffer(char[] visBuf, int upToVisible, bool showCursor)
    {
        var sb = new StringBuilder(finalTextRaw.Length + 8);
        int v = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.isTag)
            {
                sb.Append(t.tag);
            }
            else
            {
                if (v < upToVisible)
                {
                    char c = visBuf != null ? visBuf[v] : t.ch;
                    sb.Append(c);
                }
                v++;
            }
        }
        if (showCursor)
            sb.Append(TaggedCursor(cursorOn));
        return sb.ToString();
    }

    string TaggedCursor(bool on)
    {
        Color c = tmp ? tmp.color : Color.white;
        byte r = (byte)Mathf.RoundToInt(c.r * 255f);
        byte g = (byte)Mathf.RoundToInt(c.g * 255f);
        byte b = (byte)Mathf.RoundToInt(c.b * 255f);
        byte a = on ? (byte)255 : (byte)0;
        return $"<color=#{r:X2}{g:X2}{b:X2}{a:X2}>{cursorChar}</color>";
    }

    bool FontHasGlyph(TextMeshProUGUI t, string s)
    {
        if (t == null || string.IsNullOrEmpty(s))
            return false;
        var font = t.font;
        if (font == null)
            return false;
        return font.HasCharacter(s[0], true, true);
    }
}
