using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultadosNivelUI : MonoBehaviour
{
    [Header("Canvas")]
    public CanvasGroup canvasGroup;

    [Header("VALORES (solo estos los cambia el script)")]
    public TextMeshProUGUI txtScore;     // "S+"
    public TextMeshProUGUI txtTime;      // "00 : 00 : 00"
    public TextMeshProUGUI txtCollected; // "000 x 0"
    public TextMeshProUGUI txtBonus;     // "+000"
    public TextMeshProUGUI txtStyle;     // "+0000/5 = 000"
    public TextMeshProUGUI txtTotal;     // "000"

    [Header("Colores (igual estilo captura)")]
    public Color colorAmarillo = new Color(0.90f, 1.00f, 0.20f); // score/tiempo/total
    public Color colorVerde   = new Color(0.00f, 1.00f, 0.30f);  // bonus/estilo
    public Color colorRojo    = new Color(1.00f, 0.20f, 0.20f);  // divisor estilo
    public Color colorBlanco  = Color.white;

    [Header("Animación")]
    public float fadeInSeg = 0.25f;
    public float delayEntreLineas = 0.12f;
    public float durConteo = 0.7f;

    Coroutine _rutina;

    private void Awake()
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    public void Mostrar(ResultadosNivelData d)
    {
        gameObject.SetActive(true);

        if (_rutina != null) StopCoroutine(_rutina);
        _rutina = StartCoroutine(Rutina(d));
    }

    IEnumerator Rutina(ResultadosNivelData d)
    {
        Limpiar();

        yield return Fade(1f, fadeInSeg);

        // SCORE
        SetTxt(txtScore, Colorize(d.calificacionTexto, colorAmarillo));
        yield return new WaitForSeconds(delayEntreLineas);

        // TIEMPO (formato 00 : 00 : 00)
        SetTxt(txtTime, Colorize(FormatearTiempoCaptura(d.tiempoSeg), colorAmarillo));
        yield return new WaitForSeconds(delayEntreLineas);

        // COLLECTED: 000 x 0 (000 amarillo, x 0 verde)
        yield return ConteoCollected(d.chipsRecolectados, d.multiplicador);
        yield return new WaitForSeconds(delayEntreLineas);

        // BONUS: +000 (verde)  -> acá pongo chipsFijos + secreto
        int bonus = d.chipsFijos + (d.secretoEncontrado ? d.chipsSecreto : 0);
        yield return ConteoSimple(txtBonus, "+", bonus, 3, colorVerde);
        yield return new WaitForSeconds(delayEntreLineas);

        // STYLE: +0000/5 = 000 (verde / rojo / amarillo)
        yield return ConteoStyle(d.puntosEstilo, d.divisorEstilo, d.chipsPorEstilo);
        yield return new WaitForSeconds(delayEntreLineas);

        // TOTAL EARN: 000 (amarillo)
        yield return ConteoSimple(txtTotal, "", d.totalGanado, 3, colorAmarillo);

        if (canvasGroup)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    void Limpiar()
    {
        SetTxt(txtScore, "");
        SetTxt(txtTime, "");
        SetTxt(txtCollected, "");
        SetTxt(txtBonus, "");
        SetTxt(txtStyle, "");
        SetTxt(txtTotal, "");

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    IEnumerator Fade(float alphaFinal, float dur)
    {
        if (!canvasGroup) yield break;

        float a0 = canvasGroup.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dur);
            canvasGroup.alpha = Mathf.Lerp(a0, alphaFinal, t);
            yield return null;
        }
        canvasGroup.alpha = alphaFinal;
    }

    IEnumerator ConteoCollected(int chips, int mult)
    {
        float t = 0f;
        int actual = 0;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, durConteo);
            actual = Mathf.FloorToInt(Mathf.Lerp(0, chips, t));

            string left = Colorize(actual.ToString("000"), colorAmarillo);
            string right = Colorize($" x {mult}", colorVerde);
            SetTxt(txtCollected, left + right);

            yield return null;
        }

        string finLeft = Colorize(chips.ToString("000"), colorAmarillo);
        string finRight = Colorize($" x {mult}", colorVerde);
        SetTxt(txtCollected, finLeft + finRight);
    }

    IEnumerator ConteoStyle(int estilo, int divisor, int chipsEstilo)
    {
        float t = 0f;
        int actualEstilo = 0;
        int actualChips = 0;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, durConteo);
            actualEstilo = Mathf.FloorToInt(Mathf.Lerp(0, estilo, t));
            actualChips = Mathf.FloorToInt(Mathf.Lerp(0, chipsEstilo, t));

            string a = Colorize("+" + actualEstilo.ToString("000"), colorVerde);
            string b = Colorize("/" + divisor.ToString(), colorRojo);
            string c = Colorize(" = ", colorBlanco);
            string d = Colorize(actualChips.ToString("000"), colorAmarillo);

            SetTxt(txtStyle, a + b + c + d);
            yield return null;
        }

        string fa = Colorize("+" + estilo.ToString("000"), colorVerde);
        string fb = Colorize("/" + divisor.ToString(), colorRojo);
        string fc = Colorize(" = ", colorBlanco);
        string fd = Colorize(chipsEstilo.ToString("000"), colorAmarillo);

        SetTxt(txtStyle, fa + fb + fc + fd);
    }

    IEnumerator ConteoSimple(TextMeshProUGUI txt, string prefijo, int valorFinal, int pad, Color col)
    {
        float t = 0f;
        int actual = 0;

        string formato = new string('0', Mathf.Max(1, pad));

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, durConteo);
            actual = Mathf.FloorToInt(Mathf.Lerp(0, valorFinal, t));
            SetTxt(txt, Colorize(prefijo + actual.ToString(formato), col));
            yield return null;
        }

        SetTxt(txt, Colorize(prefijo + valorFinal.ToString(formato), col));
    }

    string FormatearTiempoCaptura(float s)
    {
        // 00 : 00 : 00  => MM : SS : CC (centésimas)
        int mm = Mathf.FloorToInt(s / 60f);
        int ss = Mathf.FloorToInt(s % 60f);
        int cc = Mathf.FloorToInt((s - Mathf.Floor(s)) * 100f);
        return $"{mm:00} : {ss:00} : {cc:00}";
    }

    void SetTxt(TextMeshProUGUI txt, string v)
    {
        if (txt) txt.text = v;
    }

    string Colorize(string text, Color c)
    {
        string hex = ColorUtility.ToHtmlStringRGB(c);
        return $"<color=#{hex}>{text}</color>";
    }
}