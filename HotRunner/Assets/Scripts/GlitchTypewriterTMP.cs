using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class GlitchTypewriterTMP : MonoBehaviour
{
    [Header("Texto")]
    [TextArea]
    public string defaultText;
    public bool autoPlayOnEnable = false;

    [Header("Typewriter")]
    public float charsPerSecond = 45f;
    public bool showBlockWhileTyping = true;
    public string blockChar = "█";

    [Header("Ráfagas glitch")]
    [Tooltip("Probabilidad por carácter tipiado de inyectar 'basura' fugaz.")]
    public float glitchBurstChance = 0.14f;

    [Tooltip("Duración de cada ráfaga (segundos).")]
    public float glitchBurstTime = 0.05f;
    public string glitchChars = "@#$%&/\\|<>[]+-=_*";

    [Header("Loop opcional")]
    public bool loopReveal = false;
    public float loopDelay = 0.6f;

    TextMeshProUGUI tmp;
    string cleanText;
    int shown;
    bool running;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (autoPlayOnEnable)
            StartTyping();
    }

    public void StartTyping(string overrideText = null)
    {
        StopAllCoroutines();

        cleanText =
            !string.IsNullOrEmpty(overrideText) ? overrideText
            : !string.IsNullOrEmpty(defaultText) ? defaultText
            : tmp.text;

        shown = 0;
        running = true;
        StartCoroutine(CoType());
    }

    IEnumerator CoType()
    {
        float step = 1f / Mathf.Max(1, charsPerSecond);

        while (running)
        {
            shown = Mathf.Min(cleanText.Length, shown + 1);
            tmp.text = BuildShown();

            if (Random.value < glitchBurstChance)
            {
                string backup = tmp.text;
                tmp.text = InjectGlitch(tmp.text);
                yield return new WaitForSeconds(glitchBurstTime);
                tmp.text = backup;
            }

            if (shown >= cleanText.Length)
            {
                running = false;
                if (showBlockWhileTyping)
                    tmp.text = cleanText;

                if (loopReveal)
                {
                    yield return new WaitForSeconds(loopDelay);
                    shown = 0;
                    running = true;
                    continue;
                }
                break;
            }

            yield return new WaitForSeconds(step);
        }
    }

    string BuildShown()
    {
        string head = cleanText.Substring(0, shown);
        if (showBlockWhileTyping && shown < cleanText.Length)
            return head + blockChar;
        return head;
    }

    string InjectGlitch(string s)
    {
        var arr = s.ToCharArray();
        int swaps = Mathf.Clamp(Mathf.CeilToInt(arr.Length * 0.04f), 1, 12);
        for (int i = 0; i < swaps; i++)
        {
            int idx = Random.Range(0, arr.Length);
            if (char.IsLetterOrDigit(arr[idx]))
                arr[idx] = glitchChars[Random.Range(0, glitchChars.Length)];
        }
        return new string(arr);
    }
}
