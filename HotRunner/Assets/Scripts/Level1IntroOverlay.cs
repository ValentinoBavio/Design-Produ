using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TMPro.EditorUtilities;

[DefaultExecutionOrder(-10000)]
public class Level1IntroOverlay : MonoBehaviour
{
    // ✅ Flag global para crosshair
    public static bool IsIntroActive { get; private set; } = false;

    [Header("Texto")]
    public TMP_FontAsset fontTMP;
    public string mainTitle = "LEVEL 1";
    public string subTitle  = "TRAINEE PROGRAM";
    public int titleFontSize = 72;
    public int subFontSize = 26;

    [Header("Estilo (ambos)")]
    public Color textColor = new Color(1f, 0.84f, 0.15f, 1f);
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.22f;

    [Header("Layout (centrado)")]
    public Vector2 blockAnchorMin = new Vector2(0.1f, 0.40f);
    public Vector2 blockAnchorMax = new Vector2(0.9f, 0.62f);
    public float subtitleSpacingPx = 10f;

    [Header("Timings (unscaled)")]
    public float holdSeconds = 5.0f;
    public float textFadeOutDur = 0.35f;

    [Header("Bloqueo")]
    public bool freezeTimeScale = true;
    public Behaviour[] disableDuringIntro;
    public Rigidbody[] rigidbodiesToZero;

    CanvasGroup _cg;
    float _oldTimeScale;
    bool _locked;

    void Awake()
    {
        if (!Application.isPlaying) return;

        // ✅ IMPORTANTE: al iniciar nivel, aseguramos que NO quede el flag global de prompts prendido
        // (por si reinicias escena o venís de otra escena).
        ChipUIBridge.SetGlobalHidePrompts(false);

        IsIntroActive = true;
        LockNow();
    }

    void OnDisable() { IsIntroActive = false; }
    void OnDestroy() { IsIntroActive = false; }

    void LockNow()
    {
        if (_locked) return;
        _locked = true;

        if (freezeTimeScale)
        {
            _oldTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        SetEnabled(disableDuringIntro, false);

        if (rigidbodiesToZero != null)
        {
            for (int i = 0; i < rigidbodiesToZero.Length; i++)
            {
                var rb = rigidbodiesToZero[i];
                if (!rb) continue;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    IEnumerator Start()
    {
        BuildTextOverlay();

        yield return WaitUnscaled(holdSeconds);
        yield return FadeTextAlpha(0f, textFadeOutDur);

        if (_cg) Destroy(_cg.gameObject);

        SetEnabled(disableDuringIntro, true);

        if (freezeTimeScale)
            Time.timeScale = _oldTimeScale <= 0f ? 1f : _oldTimeScale;

        _locked = false;
        IsIntroActive = false;
    }

    void BuildTextOverlay()
    {
        Canvas target = FindBestCanvas();
        if (target == null)
        {
            var cgo = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            target = cgo.GetComponent<Canvas>();
            target.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        var root = new GameObject("LevelIntroOverlay", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(target.transform, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _cg = root.GetComponent<CanvasGroup>();
        _cg.alpha = 1f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        var block = new GameObject("Block", typeof(RectTransform));
        block.transform.SetParent(root.transform, false);

        var brt = (RectTransform)block.transform;
        brt.anchorMin = blockAnchorMin;
        brt.anchorMax = blockAnchorMax;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;

        var vlg = block.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = subtitleSpacingPx;

        var title = CreateTMP(block.transform, "Title", mainTitle, titleFontSize, TextAlignmentOptions.Center);
        ApplyStyle_FORCE_MATERIAL(title);

        var sub = CreateTMP(block.transform, "Subtitle", subTitle, subFontSize, TextAlignmentOptions.Center);
        ApplyStyle_FORCE_MATERIAL(sub);

        root.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(brt);
    }

    TextMeshProUGUI CreateTMP(Transform parent, string name, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (fontTMP) tmp.font = fontTMP;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.color = Color.white;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = fontSize * 1.1f;
        le.preferredHeight = fontSize * 1.25f;

        return tmp;
    }

    void ApplyStyle_FORCE_MATERIAL(TextMeshProUGUI tmp)
    {
        if (!tmp) return;

        tmp.color = textColor;

        var baseMat = tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial : tmp.fontMaterial;
        if (baseMat == null) return;

        var mat = new Material(baseMat);
        mat.name = baseMat.name + "_IntroInstance";

        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);

        Color oc = outlineColor; oc.a = 1f;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, oc);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, 0));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

        tmp.fontMaterial = mat;
    }

    Canvas FindBestCanvas()
    {
        Canvas best = null;
        int bestScore = int.MinValue;

        var canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (!c || !c.gameObject.activeInHierarchy) continue;

            int score = 0;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) score += 1000;
            if (c.isRootCanvas) score += 200;
            score += c.sortingOrder;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best;
    }

    IEnumerator FadeTextAlpha(float target, float dur)
    {
        if (!_cg) yield break;

        float start = _cg.alpha;
        dur = Mathf.Max(0.01f, dur);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            _cg.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        _cg.alpha = target;
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static void SetEnabled(Behaviour[] arr, bool on)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) arr[i].enabled = on;
    }
}
