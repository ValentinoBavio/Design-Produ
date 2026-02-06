using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFadeUI : MonoBehaviour
{
    [Header("Auto")]
    [Tooltip("✅ Para MainMenu ponelo en TRUE (negro -> transparente al iniciar).")]
    public bool fadeInOnStart = true;

    [Tooltip("Mantener negro un toque antes del fade (opcional).")]
    public float startBlackHold = 0.00f;

    [Header("Duraciones (segundos)")]
    [Tooltip("Negro -> transparente (MainMenu). Recomendado 0.6 a 1.2 para suave.")]
    public float fadeInDur = 0.80f;

    [Tooltip("Transparente -> negro (al cambiar de escena).")]
    public float fadeOutDur = 0.25f;

    [Header("Suavidad")]
    [Tooltip("Si querés evitar cualquier flash por layouts, dejalo en 1.")]
    public int waitFramesBeforeFadeIn = 1;

    [Header("Opciones")]
    public bool blockRaycastsWhileFading = true;
    public int sortingOrder = 50000; // arriba de tu UI

    Canvas _fadeCanvas;
    Image _fadeImg;
    CanvasGroup _cg;
    Coroutine _co;

    void Awake()
    {
        if (!Application.isPlaying) return;

        BuildUnderVisibleCanvas();

        // ✅ CLAVE: arrancar en negro si vamos a hacer fade-in, para que NO haya golpe.
        if (fadeInOnStart) SetAlpha(1f);
        else SetAlpha(0f);
    }

    IEnumerator Start()
    {
        if (!Application.isPlaying) yield break;

        if (fadeInOnStart)
        {
            // ✅ Esperar frames para evitar flash de UI reacomodándose
            for (int i = 0; i < Mathf.Max(0, waitFramesBeforeFadeIn); i++)
                yield return null;

            if (startBlackHold > 0f)
                yield return new WaitForSecondsRealtime(startBlackHold);

            yield return FadeTo(0f, fadeInDur);
        }
    }

    void BuildUnderVisibleCanvas()
    {
        // 1) Encontrar un Canvas visible (preferimos Overlay y el de mayor sorting)
        Canvas target = null;
        var canvases = FindObjectsOfType<Canvas>(true);

        int bestScore = int.MinValue;

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (!c || !c.gameObject.activeInHierarchy) continue;

            int score = 0;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay) score += 1000;
            if (c.isRootCanvas) score += 500;
            score += c.sortingOrder;

            if (score > bestScore)
            {
                bestScore = score;
                target = c;
            }
        }

        // Si no existe Canvas, creamos uno
        if (target == null)
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            target = cgo.GetComponent<Canvas>();
            target.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 2) Crear un Canvas hijo SOLO para el fade
        var fadeGO = new GameObject("FadeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        fadeGO.transform.SetParent(target.transform, false);

        var fadeRT = fadeGO.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.pivot = new Vector2(0.5f, 0.5f);
        fadeRT.anchoredPosition = Vector2.zero;
        fadeRT.sizeDelta = Vector2.zero;
        fadeRT.offsetMin = Vector2.zero;
        fadeRT.offsetMax = Vector2.zero;

        _fadeCanvas = fadeGO.GetComponent<Canvas>();
        _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fadeCanvas.overrideSorting = true;
        _fadeCanvas.sortingOrder = sortingOrder;

        // (extra) copiar sortingLayer del canvas target para que no quede atrás por layer
        _fadeCanvas.sortingLayerID = target.sortingLayerID;

        var scaler = fadeGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _cg = fadeGO.GetComponent<CanvasGroup>();
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        // 3) Image negro fullscreen
        var imgGO = new GameObject("FadeBlack", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(fadeGO.transform, false);

        _fadeImg = imgGO.GetComponent<Image>();
        _fadeImg.color = Color.black;
        _fadeImg.raycastTarget = false;

        var rt = (RectTransform)imgGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        imgGO.transform.SetAsLastSibling();
        fadeGO.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(fadeRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    void SetAlpha(float a)
    {
        if (_fadeImg == null) return;
        var c = _fadeImg.color;
        c.a = Mathf.Clamp01(a);
        _fadeImg.color = c;
    }

    // ✅ EASING suave (S-curve)
    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    IEnumerator FadeTo(float target, float dur)
    {
        if (_fadeImg == null) yield break;

        float start = _fadeImg.color.a;
        dur = Mathf.Max(0.01f, dur);
        float t = 0f;

        if (blockRaycastsWhileFading && _cg != null)
            _cg.blocksRaycasts = true;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = Smooth01(t); // ✅ suaviza mucho
            SetAlpha(Mathf.Lerp(start, target, s));
            yield return null;
        }

        SetAlpha(target);

        if (blockRaycastsWhileFading && _cg != null)
            _cg.blocksRaycasts = false;
    }

    public void FadeOutThenLoad(string sceneName)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeOutThenLoad(sceneName));
    }

    IEnumerator CoFadeOutThenLoad(string sceneName)
    {
        yield return FadeTo(1f, fadeOutDur);
        SceneManager.LoadScene(sceneName);
    }
}
