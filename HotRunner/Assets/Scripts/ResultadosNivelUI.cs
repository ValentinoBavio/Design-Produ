using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ResultadosNivelUI : MonoBehaviour
{
    public static bool IsResultsVisible { get; private set; } = false;

    [Header("Canvas")]
    public CanvasGroup canvasGroup;

    [Header("TMP refs")]
    public TextMeshProUGUI txtScore;
    public TextMeshProUGUI txtTime;
    public TextMeshProUGUI txtCollected;
    public TextMeshProUGUI txtBonus;
    public TextMeshProUGUI txtStyle;
    public TextMeshProUGUI txtTotal;

    [Header("BOTTOM HINTS (Padre de los 3 textos)")]
    public GameObject bottomHintsRoot;

    [Header("Acciones (escenas)")]
    public string bonusStageSceneName = "BonusStage";
    public string mainMenuSceneName = "MainMenu";

    [Header("Teclas")]
    public KeyCode keyContinue = KeyCode.Return;
    public KeyCode keyContinueAlt = KeyCode.KeypadEnter;
    public KeyCode keyRetry = KeyCode.Space;
    public KeyCode keyMainMenu = KeyCode.Escape;

    [Header("FadeBlack (Image) - por Inspector")]
    [Tooltip("Arrastrá acá tu Image negra (FadeBlack).")]
    public Image fadeBlack;

    [Tooltip("Si está ON, el fade se re-parenta al root del Canvas para tapar TODO (recomendado).")]
    public bool reparentFadeToCanvasRoot = true;

    [Tooltip("Duración del fade a negro al ir a MainMenu.")]
    public float fadeToMenuSeconds = 0.25f;

    [Header("Mostrar hints")]
    public bool showHintsOnlyAfterFinish = true;

    [Header("Pulse leve (Bottom Hints)")]
    public bool hintPulseEnabled = true;
    [Range(0f, 0.12f)] public float hintPulseAmount = 0.03f;
    [Range(0.1f, 4f)] public float hintPulseSpeed = 1.2f;

    [Header("Colores")]
    public Color colorAmarillo = new Color(0.90f, 1.00f, 0.20f);
    public Color colorVerde = new Color(0.00f, 1.00f, 0.30f);
    public Color colorRojo = new Color(1.00f, 0.20f, 0.20f);
    public Color colorBlanco = Color.white;

    [Header("Animación")]
    public float fadeInSeg = 0.25f;
    public float delayEntreLineas = 0.20f;

    [Tooltip("Duración del rolling (unscaled). Si usás rollingClip y syncDurConteoToRollingClip, se pisa.")]
    public float durConteo = 1.345f;

    [Header("Typewriter")]
    public bool typewriterEnabled = true;
    public float cpsFast = 40f;
    public float cpsNormal = 28f;
    public bool cursorEnabled = true;
    public string cursorChar = "█";
    public float cursorBlinkInterval = 0.08f;

    [Header("Skip (durante conteo)")]
    public bool allowSkip = true;

    [Header("Tiempo - formato")]
    public bool timeWithSpaces = true;

    [Header("Rolling pads")]
    public int padCollected = 3;
    public int padBonus = 3;
    public int padStylePoints = 3;
    public int padStyleChips = 3;
    public int padTotal = 3;

    [Header("Fake roll cuando final = 0")]
    public bool fakeRollWhenZero = true;
    [Range(0.10f, 0.95f)] public float fakeStartMin01 = 0.35f;
    [Range(0.10f, 1.00f)] public float fakeStartMax01 = 0.95f;

    [Header("Typing SFX (loop)")]
    public bool typingSfxEnabled = true;
    public AudioClip typingLoopClip;
    public AudioMixerGroup typingMixerGroup;
    [Range(0f, 1f)] public float typingVol = 0.65f;
    public float typingFadeOut = 0.05f;

    [Header("Rolling SFX")]
    public bool rollingSfxEnabled = true;
    public AudioClip rollingClip;
    public AudioMixerGroup rollingMixerGroup;
    [Range(0f, 1f)] public float rollingVol = 0.9f;
    public bool syncDurConteoToRollingClip = true;
    public float rollingFadeOut = 0.04f;

    // runtime
    Coroutine _rutina;
    bool _skipRequested;

    bool _actionsEnabled = false;
    bool _transitioning = false;

    float _blinkAcc;
    bool _cursorOn = true;

    AudioSource _typingSrc;
    Coroutine _typingFadeCo;

    AudioSource _rollingSrc;
    Coroutine _rollingFadeCo;

    Vector3 _hintsBaseScale = Vector3.one;
    float _hintPulseT0;

    // ✅ Fade runtime cache (para re-parent)
    Canvas _rootCanvas;
    Transform _fadeOriginalParent;
    int _fadeOriginalSibling;
    bool _fadeCached;

    void Awake()
    {
        IsResultsVisible = false;

        ForceTMPNoWrap(txtScore);
        ForceTMPNoWrap(txtTime);
        ForceTMPNoWrap(txtCollected);
        ForceTMPNoWrap(txtBonus);
        ForceTMPNoWrap(txtStyle);
        ForceTMPNoWrap(txtTotal);

        if (syncDurConteoToRollingClip && rollingClip)
            durConteo = rollingClip.length;

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        CacheHintsScale();
        SetHintsVisible(false);

        gameObject.SetActive(false);

        EnsureTypingAudio();
        EnsureRollingAudio();

        EnsureFadeRef();
        SetFadeAlpha(0f);
    }

    void OnEnable()
    {
        IsResultsVisible = true;

        _actionsEnabled = false;
        _transitioning = false;

        CacheHintsScale();
        if (showHintsOnlyAfterFinish) SetHintsVisible(false);

        EnsureFadeRef();
        SetFadeAlpha(0f);
    }

    void OnDisable()
    {
        IsResultsVisible = false;
        SetHintsVisible(false);
        _actionsEnabled = false;
        _transitioning = false;
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_transitioning) return;

        bool pressedContinue = Input.GetKeyDown(keyContinue) || Input.GetKeyDown(keyContinueAlt);
        bool pressedRetry = Input.GetKeyDown(keyRetry);
        bool pressedMenu = Input.GetKeyDown(keyMainMenu);

        // Durante rutina: Enter skipea
        if (_rutina != null)
        {
            if (allowSkip && pressedContinue)
                _skipRequested = true;
            return;
        }

        // Ya terminó: acciones
        if (!_actionsEnabled) return;

        if (pressedContinue) GoContinue();
        else if (pressedRetry) GoRetry();
        else if (pressedMenu) GoMainMenu();
    }

    void LateUpdate()
    {
        if (!hintPulseEnabled) return;
        if (!_actionsEnabled) return;
        if (!bottomHintsRoot || !bottomHintsRoot.activeInHierarchy) return;

        float t = (Time.unscaledTime + _hintPulseT0) * Mathf.Max(0.01f, hintPulseSpeed);
        float s = 1f + Mathf.Sin(t * Mathf.PI * 2f) * hintPulseAmount;
        bottomHintsRoot.transform.localScale = _hintsBaseScale * s;
    }

    public void Mostrar(ResultadosNivelData d)
    {
        gameObject.SetActive(true);

        if (syncDurConteoToRollingClip && rollingClip)
            durConteo = rollingClip.length;

        if (_rutina != null) StopCoroutine(_rutina);
        _rutina = StartCoroutine(Rutina(d));
    }

    IEnumerator Rutina(ResultadosNivelData d)
    {
        _skipRequested = false;
        _actionsEnabled = false;
        _transitioning = false;

        if (showHintsOnlyAfterFinish)
            SetHintsVisible(false);

        Limpiar();

        yield return Fade(1f, fadeInSeg);

        if (_skipRequested)
        {
            ShowAllInstant(d);
            FinishAndEnableActions();
            _rutina = null;
            yield break;
        }

        // SCORE
        yield return TypeInto_Normal(txtScore, Colorize(d.calificacionTexto, colorAmarillo), cpsFast);
        if (BreakIfSkip(d)) yield break;
        yield return WaitLineDelay();

        // TIME (espacios no “cuentan”)
        yield return TypeInto_Time_NoSpaceCost(txtTime, Colorize(FormatearTiempo(d.tiempoSeg), colorAmarillo), cpsFast);
        if (BreakIfSkip(d)) yield break;
        yield return WaitLineDelay();

        // COLLECTED
        {
            string pre = Colorize(new string('0', padCollected), colorAmarillo) + Colorize(" x " + d.multiplicador, colorVerde);
            yield return TypeInto_Normal(txtCollected, pre, cpsNormal);
            if (BreakIfSkip(d)) yield break;

            yield return RollCollected(d.chipsRecolectados, d.multiplicador);
            if (BreakIfSkip(d)) yield break;

            yield return WaitLineDelay();
        }

        // BONUS
        {
            int bonus = d.chipsFijos + (d.secretoEncontrado ? d.chipsSecreto : 0);
            string pre = Colorize("+" + new string('0', padBonus), colorVerde);

            yield return TypeInto_Normal(txtBonus, pre, cpsNormal);
            if (BreakIfSkip(d)) yield break;

            yield return RollSimple(txtBonus, "+", bonus, padBonus, colorVerde);
            if (BreakIfSkip(d)) yield break;

            yield return WaitLineDelay();
        }

        // STYLE
        {
            string pre = Colorize("+" + new string('0', padStylePoints), colorVerde) +
                         Colorize("/" + d.divisorEstilo, colorRojo) +
                         Colorize(" = ", colorBlanco) +
                         Colorize(new string('0', padStyleChips), colorAmarillo);

            yield return TypeInto_Normal(txtStyle, pre, cpsNormal);
            if (BreakIfSkip(d)) yield break;

            yield return RollStyle(d.puntosEstilo, d.divisorEstilo, d.chipsPorEstilo);
            if (BreakIfSkip(d)) yield break;

            yield return WaitLineDelay();
        }

        // TOTAL
        {
            string pre = Colorize(new string('0', padTotal), colorAmarillo);

            yield return TypeInto_Normal(txtTotal, pre, cpsNormal);
            if (BreakIfSkip(d)) yield break;

            yield return RollSimple(txtTotal, "", d.totalGanado, padTotal, colorAmarillo);
            if (BreakIfSkip(d)) yield break;
        }

        StopTypingSFX(true);
        StopRollingSFX(true);

        if (canvasGroup)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        _rutina = null;
        FinishAndEnableActions();
    }

    void FinishAndEnableActions()
    {
        if (showHintsOnlyAfterFinish)
            SetHintsVisible(true);

        CacheHintsScale();
        _hintPulseT0 = Random.value * 10f;

        _actionsEnabled = true;
        _skipRequested = false;
    }

    bool BreakIfSkip(ResultadosNivelData d)
    {
        if (!_skipRequested) return false;

        StopTypingSFX(true);
        StopRollingSFX(true);
        ShowAllInstant(d);

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        _rutina = null;
        FinishAndEnableActions();
        return true;
    }

    // ===================== ACCIONES =====================

    void GoContinue()
    {
        if (_transitioning) return;

        if (!string.IsNullOrEmpty(bonusStageSceneName))
            SceneManager.LoadScene(bonusStageSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoRetry()
    {
        if (_transitioning) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoMainMenu()
    {
        if (_transitioning) return;

        StopTypingSFX(true);
        StopRollingSFX(true);

        StartCoroutine(CoToMainMenu_FadeBlackThenLoad());
    }

    IEnumerator CoToMainMenu_FadeBlackThenLoad()
    {
        _transitioning = true;
        _actionsEnabled = false;

        if (canvasGroup)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        EnsureFadeRef();

        if (!fadeBlack)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        PrepareFadeOnTop();

        float dur = Mathf.Max(0.01f, fadeToMenuSeconds);
        float t = 0f;
        float startA = fadeBlack.color.a;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            SetFadeAlpha(Mathf.Lerp(startA, 1f, t));
            yield return null;
        }

        SetFadeAlpha(1f);
        AudioListener.pause = false;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ===================== FADE (Image por Inspector) =====================

    void EnsureFadeRef()
    {
        if (!fadeBlack)
        {
            // fallback opcional si te olvidás de asignarlo: busca "FadeBlack" en hijos
            var t = transform.Find("FadeBlack");
            if (t) fadeBlack = t.GetComponent<Image>();
        }

        if (!fadeBlack) return;

        if (_rootCanvas == null)
            _rootCanvas = fadeBlack.GetComponentInParent<Canvas>(true);

        if (!_fadeCached)
        {
            _fadeOriginalParent = fadeBlack.transform.parent;
            _fadeOriginalSibling = fadeBlack.transform.GetSiblingIndex();
            _fadeCached = true;
        }

        // asegurate que arranque en 0
        SetFadeAlpha(0f);
        fadeBlack.raycastTarget = false;
        fadeBlack.gameObject.SetActive(true);
    }

    void PrepareFadeOnTop()
    {
        if (!fadeBlack) return;

        // ✅ esto hace que tape TODO lo que esté en el mismo canvas
        if (reparentFadeToCanvasRoot && _rootCanvas != null)
        {
            fadeBlack.transform.SetParent(_rootCanvas.transform, worldPositionStays: false);

            // full stretch por las dudas
            var rt = fadeBlack.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ✅ arriba de todo (dentro del canvas)
        fadeBlack.transform.SetAsLastSibling();

        // arrancar desde alpha actual (por si quedó algo)
        fadeBlack.gameObject.SetActive(true);
    }

    void SetFadeAlpha(float a)
    {
        if (!fadeBlack) return;
        var c = fadeBlack.color;
        c.r = 0f; c.g = 0f; c.b = 0f;
        c.a = Mathf.Clamp01(a);
        fadeBlack.color = c;
    }

    // ===================== UI helpers =====================

    IEnumerator WaitLineDelay()
    {
        if (delayEntreLineas > 0f)
            yield return new WaitForSecondsRealtime(delayEntreLineas);
    }

    void CacheHintsScale()
    {
        if (!bottomHintsRoot) return;
        _hintsBaseScale = bottomHintsRoot.transform.localScale;
        if (_hintsBaseScale == Vector3.zero) _hintsBaseScale = Vector3.one;
    }

    void SetHintsVisible(bool on)
    {
        if (!bottomHintsRoot) return;
        bottomHintsRoot.SetActive(on);
        if (!on) bottomHintsRoot.transform.localScale = _hintsBaseScale;
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

    void ShowAllInstant(ResultadosNivelData d)
    {
        SetTxt(txtScore, Colorize(d.calificacionTexto, colorAmarillo));
        SetTxt(txtTime, Colorize(FormatearTiempo(d.tiempoSeg), colorAmarillo));

        SetTxt(txtCollected,
            Colorize(d.chipsRecolectados.ToString(PadFmt(padCollected)), colorAmarillo) +
            Colorize(" x " + d.multiplicador, colorVerde));

        int bonus = d.chipsFijos + (d.secretoEncontrado ? d.chipsSecreto : 0);
        SetTxt(txtBonus, Colorize("+" + bonus.ToString(PadFmt(padBonus)), colorVerde));

        SetTxt(txtStyle,
            Colorize("+" + d.puntosEstilo.ToString(PadFmt(padStylePoints)), colorVerde) +
            Colorize("/" + d.divisorEstilo, colorRojo) +
            Colorize(" = ", colorBlanco) +
            Colorize(d.chipsPorEstilo.ToString(PadFmt(padStyleChips)), colorAmarillo));

        SetTxt(txtTotal, Colorize(d.totalGanado.ToString(PadFmt(padTotal)), colorAmarillo));
    }

    IEnumerator Fade(float alphaFinal, float dur)
    {
        if (!canvasGroup) yield break;

        float a0 = canvasGroup.alpha;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            canvasGroup.alpha = Mathf.Lerp(a0, alphaFinal, t);
            yield return null;
        }

        canvasGroup.alpha = alphaFinal;
    }

    // ===================== TYPEWRITER =====================

    IEnumerator TypeInto_Normal(TextMeshProUGUI txt, string final, float cps)
    {
        if (!txt) yield break;

        if (_skipRequested || !typewriterEnabled || cps <= 0.01f)
        {
            txt.text = final;
            yield break;
        }

        StartTypingSFX();

        _blinkAcc = 0f;
        _cursorOn = true;

        var sb = new StringBuilder(final.Length + 8);

        float last = Time.realtimeSinceStartup;
        float acc = 0f;

        int i = 0;
        while (i < final.Length)
        {
            if (_skipRequested)
            {
                txt.text = final;
                StopTypingSFX(true);
                yield break;
            }

            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - last);
            last = now;

            if (cursorEnabled)
            {
                _blinkAcc += dt;
                if (_blinkAcc >= Mathf.Max(0.02f, cursorBlinkInterval))
                {
                    _blinkAcc = 0f;
                    _cursorOn = !_cursorOn;
                }
            }

            acc += dt * cps;
            int add = Mathf.FloorToInt(acc);

            if (add > 0)
            {
                acc -= add;

                while (add > 0 && i < final.Length)
                {
                    char c = final[i];

                    if (c == '<')
                    {
                        int close = final.IndexOf('>', i);
                        if (close >= 0)
                        {
                            sb.Append(final, i, close - i + 1);
                            i = close + 1;
                            continue;
                        }
                    }

                    sb.Append(c);
                    i++;
                    add--;
                }
            }

            txt.text = cursorEnabled
                ? (_cursorOn ? (sb.ToString() + cursorChar) : sb.ToString())
                : sb.ToString();

            yield return null;
        }

        txt.text = final;
        StopTypingSFX(false);
    }

    IEnumerator TypeInto_Time_NoSpaceCost(TextMeshProUGUI txt, string final, float cps)
    {
        if (!txt) yield break;

        if (_skipRequested || !typewriterEnabled || cps <= 0.01f)
        {
            txt.text = final;
            yield break;
        }

        StartTypingSFX();

        float last = Time.realtimeSinceStartup;
        float accVisible = 0f;

        _blinkAcc = 0f;
        _cursorOn = true;

        var sb = new StringBuilder(final.Length + 8);

        int i = 0;
        while (i < final.Length)
        {
            if (_skipRequested)
            {
                txt.text = final;
                StopTypingSFX(true);
                yield break;
            }

            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - last);
            last = now;

            if (cursorEnabled)
            {
                _blinkAcc += dt;
                if (_blinkAcc >= Mathf.Max(0.02f, cursorBlinkInterval))
                {
                    _blinkAcc = 0f;
                    _cursorOn = !_cursorOn;
                }
            }

            accVisible += dt * cps;
            int addVisible = Mathf.FloorToInt(accVisible);

            if (addVisible > 0)
            {
                accVisible -= addVisible;

                while (addVisible > 0 && i < final.Length)
                {
                    char c = final[i];

                    if (c == '<')
                    {
                        int close = final.IndexOf('>', i);
                        if (close >= 0)
                        {
                            sb.Append(final, i, close - i + 1);
                            i = close + 1;
                            continue;
                        }
                    }

                    // espacios NO consumen presupuesto
                    if (c == ' ')
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }

                    sb.Append(c);
                    i++;
                    addVisible--;
                }
            }

            txt.text = cursorEnabled
                ? (_cursorOn ? (sb.ToString() + cursorChar) : sb.ToString())
                : sb.ToString();

            yield return null;
        }

        txt.text = final;
        StopTypingSFX(false);
    }

    // ===================== ROLLING =====================

    IEnumerator RollCollected(int chips, int mult)
    {
        bool fake = (chips == 0 && fakeRollWhenZero);
        int start = fake ? GetFakeStart(padCollected) : 0;

        bool shouldPlay = rollingSfxEnabled && rollingClip && (chips != 0 || fake);
        if (shouldPlay) StartRollingSFX();

        float t = 0f;
        float dur = Mathf.Max(0.01f, durConteo);

        while (t < 1f)
        {
            if (_skipRequested) break;

            t += Time.unscaledDeltaTime / dur;
            float x = Ease01(t);
            int v = Mathf.FloorToInt(Mathf.Lerp(start, chips, x));

            SetTxt(txtCollected,
                Colorize(v.ToString(PadFmt(padCollected)), colorAmarillo) +
                Colorize(" x " + mult, colorVerde));

            yield return null;
        }

        SetTxt(txtCollected,
            Colorize(chips.ToString(PadFmt(padCollected)), colorAmarillo) +
            Colorize(" x " + mult, colorVerde));

        if (shouldPlay) StopRollingSFX(false);
    }

    IEnumerator RollStyle(int estilo, int divisor, int chipsEstilo)
    {
        bool fakeA = (estilo == 0 && fakeRollWhenZero);
        bool fakeB = (chipsEstilo == 0 && fakeRollWhenZero);

        int startA = fakeA ? GetFakeStart(padStylePoints) : 0;
        int startB = fakeB ? GetFakeStart(padStyleChips) : 0;

        bool shouldPlay = rollingSfxEnabled && rollingClip && ((estilo != 0 || chipsEstilo != 0) || (fakeA || fakeB));
        if (shouldPlay) StartRollingSFX();

        float t = 0f;
        float dur = Mathf.Max(0.01f, durConteo);
        const float lagB = 0.12f;

        while (t < 1f)
        {
            if (_skipRequested) break;

            t += Time.unscaledDeltaTime / dur;

            float xa = Ease01(t);
            float xb = Ease01(Mathf.Clamp01((t - lagB) / (1f - lagB)));

            int a = Mathf.FloorToInt(Mathf.Lerp(startA, estilo, xa));
            int b = Mathf.FloorToInt(Mathf.Lerp(startB, chipsEstilo, xb));

            SetTxt(txtStyle,
                Colorize("+" + a.ToString(PadFmt(padStylePoints)), colorVerde) +
                Colorize("/" + divisor, colorRojo) +
                Colorize(" = ", colorBlanco) +
                Colorize(b.ToString(PadFmt(padStyleChips)), colorAmarillo));

            yield return null;
        }

        SetTxt(txtStyle,
            Colorize("+" + estilo.ToString(PadFmt(padStylePoints)), colorVerde) +
            Colorize("/" + divisor, colorRojo) +
            Colorize(" = ", colorBlanco) +
            Colorize(chipsEstilo.ToString(PadFmt(padStyleChips)), colorAmarillo));

        if (shouldPlay) StopRollingSFX(false);
    }

    IEnumerator RollSimple(TextMeshProUGUI txt, string prefijo, int valorFinal, int pad, Color col)
    {
        if (!txt) yield break;

        bool fake = (valorFinal == 0 && fakeRollWhenZero);
        int start = fake ? GetFakeStart(pad) : 0;

        bool shouldPlay = rollingSfxEnabled && rollingClip && (valorFinal != 0 || fake);
        if (shouldPlay) StartRollingSFX();

        float t = 0f;
        float dur = Mathf.Max(0.01f, durConteo);

        while (t < 1f)
        {
            if (_skipRequested) break;

            t += Time.unscaledDeltaTime / dur;
            float x = Ease01(t);

            int v = Mathf.FloorToInt(Mathf.Lerp(start, valorFinal, x));
            SetTxt(txt, Colorize(prefijo + v.ToString(PadFmt(pad)), col));
            yield return null;
        }

        SetTxt(txt, Colorize(prefijo + valorFinal.ToString(PadFmt(pad)), col));
        if (shouldPlay) StopRollingSFX(false);
    }

    // ===================== AUDIO =====================

    void EnsureTypingAudio()
    {
        if (_typingSrc != null) return;

        _typingSrc = GetComponent<AudioSource>();
        if (!_typingSrc) _typingSrc = gameObject.AddComponent<AudioSource>();

        _typingSrc.playOnAwake = false;
        _typingSrc.loop = true;
        _typingSrc.spatialBlend = 0f;
        _typingSrc.volume = Mathf.Clamp01(typingVol);

        if (typingMixerGroup)
            _typingSrc.outputAudioMixerGroup = typingMixerGroup;
    }

    void StartTypingSFX()
    {
        if (!typingSfxEnabled || !typingLoopClip) return;

        EnsureTypingAudio();

        if (_typingFadeCo != null) { StopCoroutine(_typingFadeCo); _typingFadeCo = null; }

        if (typingMixerGroup && _typingSrc.outputAudioMixerGroup != typingMixerGroup)
            _typingSrc.outputAudioMixerGroup = typingMixerGroup;

        if (_typingSrc.clip != typingLoopClip) _typingSrc.clip = typingLoopClip;

        _typingSrc.volume = Mathf.Clamp01(typingVol);
        if (!_typingSrc.isPlaying) _typingSrc.Play();
    }

    void StopTypingSFX(bool instant)
    {
        if (_typingSrc == null) return;

        if (instant || typingFadeOut <= 0.001f || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            if (_typingFadeCo != null) { StopCoroutine(_typingFadeCo); _typingFadeCo = null; }
            if (_typingSrc.isPlaying) _typingSrc.Stop();
            _typingSrc.volume = Mathf.Clamp01(typingVol);
            return;
        }

        if (_typingFadeCo != null) { StopCoroutine(_typingFadeCo); _typingFadeCo = null; }
        _typingFadeCo = StartCoroutine(Co_FadeOutStop(_typingSrc, typingVol, typingFadeOut, () => _typingFadeCo = null));
    }

    void EnsureRollingAudio()
    {
        if (_rollingSrc != null) return;

        var all = GetComponents<AudioSource>();
        if (all != null && all.Length >= 2) _rollingSrc = all[1];
        else _rollingSrc = gameObject.AddComponent<AudioSource>();

        _rollingSrc.playOnAwake = false;
        _rollingSrc.loop = false;
        _rollingSrc.spatialBlend = 0f;
        _rollingSrc.volume = Mathf.Clamp01(rollingVol);

        if (rollingMixerGroup)
            _rollingSrc.outputAudioMixerGroup = rollingMixerGroup;
    }

    void StartRollingSFX()
    {
        if (!rollingSfxEnabled || !rollingClip) return;

        EnsureRollingAudio();

        if (_rollingFadeCo != null) { StopCoroutine(_rollingFadeCo); _rollingFadeCo = null; }

        if (syncDurConteoToRollingClip && rollingClip)
            durConteo = rollingClip.length;

        if (rollingMixerGroup && _rollingSrc.outputAudioMixerGroup != rollingMixerGroup)
            _rollingSrc.outputAudioMixerGroup = rollingMixerGroup;

        _rollingSrc.Stop();
        _rollingSrc.clip = rollingClip;
        _rollingSrc.volume = Mathf.Clamp01(rollingVol);
        _rollingSrc.pitch = 1f;
        _rollingSrc.Play();
    }

    void StopRollingSFX(bool instant)
    {
        if (_rollingSrc == null) return;

        if (instant || rollingFadeOut <= 0.001f || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            if (_rollingFadeCo != null) { StopCoroutine(_rollingFadeCo); _rollingFadeCo = null; }
            if (_rollingSrc.isPlaying) _rollingSrc.Stop();
            _rollingSrc.volume = Mathf.Clamp01(rollingVol);
            _rollingSrc.pitch = 1f;
            return;
        }

        if (_rollingFadeCo != null) { StopCoroutine(_rollingFadeCo); _rollingFadeCo = null; }
        _rollingFadeCo = StartCoroutine(Co_FadeOutStop(_rollingSrc, rollingVol, rollingFadeOut, () => _rollingFadeCo = null));
    }

    IEnumerator Co_FadeOutStop(AudioSource src, float baseVol, float dur, System.Action onDone)
    {
        if (!src) yield break;

        float startVol = src.volume;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            src.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        if (src.isPlaying) src.Stop();
        src.volume = Mathf.Clamp01(baseVol);
        onDone?.Invoke();
    }

    // ===================== UTILS =====================

    static void ForceTMPNoWrap(TextMeshProUGUI t)
    {
        if (!t) return;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
    }

    int GetFakeStart(int pad)
    {
        int max = Mathf.RoundToInt(Mathf.Pow(10f, Mathf.Clamp(pad, 1, 8))) - 1;
        int min = Mathf.Clamp(Mathf.RoundToInt(max * fakeStartMin01), 1, max);
        int mx = Mathf.Clamp(Mathf.RoundToInt(max * fakeStartMax01), min, max);
        return Random.Range(min, mx + 1);
    }

    static float Ease01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    string PadFmt(int pad) => new string('0', Mathf.Max(1, pad));

    string FormatearTiempo(float s)
    {
        int mm = Mathf.FloorToInt(s / 60f);
        int ss = Mathf.FloorToInt(s % 60f);
        int cc = Mathf.FloorToInt((s - Mathf.Floor(s)) * 100f);

        if (!timeWithSpaces)
            return $"{mm:00}:{ss:00}:{cc:00}";

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

