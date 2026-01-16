using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFadeUI : MonoBehaviour
{
    [Header("Auto")]
    public bool fadeInOnStart = true;     // LoadingScene: true / MainMenu: false
    public float startBlackHold = 0.00f;  // mantener negro un toque antes de fade-in

    [Header("Duraciones (segundos)")]
    public float fadeInDur = 0.18f;   // negro -> transparente
    public float fadeOutDur = 0.25f;  // transparente -> negro

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
        SetAlpha(0f); // MainMenu NO debe quedar negro
    }

    IEnumerator Start()
    {
        if (!Application.isPlaying) yield break;

        if (fadeInOnStart)
        {
            SetAlpha(1f);
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

            // Preferir Overlay
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) score += 1000;

            // Preferir RootCanvas
            if (c.isRootCanvas) score += 500;

            // Preferir más sorting
            score += c.sortingOrder;

            if (score > bestScore)
            {
                bestScore = score;
                target = c;
            }
        }

        // Si no existe Canvas, creamos uno (raro si tu UI se ve)
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

        // ✅ FIX REAL: hacer que el FadeCanvas sea FULL SCREEN (si no, queda 100x100)
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

        var scaler = fadeGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _cg = fadeGO.GetComponent<CanvasGroup>();
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        // 3) Crear Image negro fullscreen (blindado)
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

        // Fuerza rebuild por si el Canvas/Scaler se actualiza un frame después
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
            SetAlpha(Mathf.Lerp(start, target, t));
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