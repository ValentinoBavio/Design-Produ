using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class LaptopMenuRenderTextureRig : MonoBehaviour
{
    enum PanelId { Main, Options, Logs }

    // =========================
    //  AUDIO (solo durante la sesión)
    // =========================
    static bool s_audioInit;
    static float s_master01 = 1f;
    static float s_music01 = 1f;
    static float s_sfx01 = 1f;

    [Header("Pantalla Laptop")]
    public MeshRenderer pantallaRenderer;
    public Collider pantallaCollider;
    public int materialIndex = 0;

    [Header("RenderTexture")]
    public int rtAncho = 1024;
    public int rtAlto = 576;

    [Header("Screen Zoom (BaseMap_ST)")]
    public bool aplicarBaseMapST = true;
    public Vector2 screenTiling = new Vector2(0.7f, 0.7f);
    public bool autoCenterOffset = true;
    public Vector2 screenOffset = new Vector2(0.15f, 0.15f);

    [Header("Layout (tuneo)")]
    [Range(0.8f, 2.0f)] public float uiScale = 1.20f;
    public Vector2 safeInsetExtra = new Vector2(0.05f, 0.06f);
    [Range(0.30f, 0.70f)] public float panelWidth = 0.52f;
    [Range(0.35f, 0.85f)] public float panelHeight = 0.68f;
    public Vector2 panelOffset = new Vector2(0.00f, 0.00f);

    [Header("Menu BG (solo detrás de botones)")]
    public bool usarFondoSoloDetrasDeBotones = true;
    public Vector2 menuBgAnchorMin = new Vector2(0.00f, 0.18f);
    public Vector2 menuBgAnchorMax = new Vector2(1.00f, 0.92f);

    [Header("Panels (SYS CONFIG / LOGS) layout")]
    public Vector2 overlayInsetMin = new Vector2(0.06f, 0.10f);
    public Vector2 overlayInsetMax = new Vector2(0.06f, 0.10f);

    public Vector2 optionsContentAnchorMin = new Vector2(0.08f, 0.26f);
    public Vector2 optionsContentAnchorMax = new Vector2(0.92f, 0.70f);

    public Vector2 logsBodyAnchorMin = new Vector2(0.08f, 0.24f);
    public Vector2 logsBodyAnchorMax = new Vector2(0.92f, 0.76f);

    [Header("SYS CONFIG - Sliders (forma)")]
    [Range(0.28f, 0.55f)] public float sliderLabelWidth01 = 0.42f;
    public float sliderRowHeight = 56f;
    public float sliderBarHeight = 10f;
    public float sliderHandleWidth = 10f;
    public float sliderHandleHeightMult = 1.8f;

    [Header("SYS CONFIG - Hover / Pulse")]
    public bool sliderPulseEnabled = true;
    [Range(0f, 1f)] public float sliderPulseAmount = 0.35f;
    public float sliderPulseSpeed = 5.5f;

    [Header("BACK (SYS CONFIG / LOGS)")]
    public Vector2 backAnchorMin = new Vector2(0.08f, 0.12f);
    public Vector2 backAnchorMax = new Vector2(0.28f, 0.20f);

    [Header("Live Tuning (modificar en vivo)")]
    public bool liveRebuildInPlay = true;
    public bool liveRebuildInEditor = true;
    public float rebuildCooldown = 0.12f;

    [Header("Fondo")]
    public Sprite backgroundSprite;
    [Range(0f, 1f)] public float overlayAlpha = 0.25f;

    [Header("Fuente TMP (opcional)")]
    public TMP_FontAsset fontTMP;

    [Header("Etiqueta Terminal")]
    public bool mostrarEtiquetaTerminal = true;
    public string etiquetaTerminal = ">> SECURE TERMINAL // AUTH REQUIRED";

    [Header("Hint inferior")]
    public bool mostrarHint = true;
    public string hintTexto = "Press [Enter] to Select";
    public bool mostrarVersion = true;
    public string versionTexto = "v0.1 / build 2049";

    [Header("HUD - Layout independiente (0..1 dentro del área segura)")]
    public Vector2 terminalAnchorMin01 = new Vector2(0.06f, 0.92f);
    public Vector2 terminalAnchorMax01 = new Vector2(0.94f, 0.98f);
    public Vector2 terminalOffsetMinPx = Vector2.zero;
    public Vector2 terminalOffsetMaxPx = Vector2.zero;

    public Vector2 hintAnchorMin01 = new Vector2(0.06f, 0.02f);
    public Vector2 hintAnchorMax01 = new Vector2(0.70f, 0.08f);
    public Vector2 hintOffsetMinPx = Vector2.zero;
    public Vector2 hintOffsetMaxPx = Vector2.zero;

    public Vector2 versionAnchorMin01 = new Vector2(0.70f, 0.02f);
    public Vector2 versionAnchorMax01 = new Vector2(0.94f, 0.08f);
    public Vector2 versionOffsetMinPx = Vector2.zero;
    public Vector2 versionOffsetMaxPx = Vector2.zero;

    [Header("Textos de Botones (Main)")]
    public string labelStart = "INIT SESSION";
    public string labelOptions = "SYS CONFIG";
    public string labelLogs = "LOGS";
    public string labelExit = "EXIT TERMINAL";

    [Header("Paleta")]
    public Color fondoPanel = new Color(0.027f, 0.063f, 0.094f, 0.80f);
    public Color cianUI = new Color(0f, 0.898f, 1f, 1f);
    public Color cianTexto = new Color(0.482f, 0.937f, 1f, 1f);
    public Color ambarSelect = new Color(1f, 0.690f, 0f, 1f);
    public Color rojoExit = new Color(1f, 0.231f, 0.420f, 1f);

    [Header("Acciones")]
    public string escenaNuevaPartida = "Level1";

    [Header("AudioMixer (SYS CONFIG)")]
    public AudioMixer audioMixer;
    public string paramMaster = "MasterVol";
    public string paramMusic = "MusicVol";
    public string paramSfx = "SFXVol";
    public float minDb = -80f;

    [Header("LOGS")]
    public string logsTitulo = "LOGS";
    [TextArea(3, 10)]
    public string logsTexto =
        "BOOT: OK\n" +
        "LINK: STABLE\n" +
        "AUTH: REQUIRED\n";

    [Header("LOGS - Typewriter")]
    public bool logsTypewriterEnabled = true;
    public string logsCursorChar = "█";
    public float logsStartDelay = 0.03f;
    public float logsCursorBlinkInterval = 0.08f;
    public float logsCharsPerSecondFirstTwo = 140f;
    public float logsCharsPerSecondRest = 70f;
    public float logsHoldAfterFirstLine = 1f;
    public float logsHoldAfterSecondLine = 1f;
    public bool logsReplayEveryOpen = true;
    public bool logsDoNotRestartWhileOpen = true;
    public bool logsKeepBlinkingAtEnd = true;

    [Header("LOGS - Cursor")]
    [Tooltip("✅ Si está en true, al terminar de escribir se quita el cursor █.")]
    public bool logsCursorDisappearAtEnd = true;

    // =========================
    //  ✅ LOGS - Typing SFX (loop mientras escribe)
    // =========================
    [Header("LOGS - Typing SFX (loop mientras escribe)")]
    public bool logsTypingSfxEnabled = true;
    public AudioClip logsTypingLoopClip;
    public AudioMixerGroup logsTypingMixerGroup;
    [Range(0f, 1f)] public float logsTypingSfxVol = 0.65f;
    [Tooltip("Fade out cortito para evitar click al cortar el loop.")]
    public float logsTypingSfxFadeOut = 0.04f;

    [Header("Comportamiento")]
    public bool ocultarCursorCuandoNoHayMenu = true;

    [Header("Anti-Enter (evita Submit al abrir)")]
    public float delayActivarInteraccion = 0.05f;

    [Header("Fix - Rebote selección (Main)")]
    public bool fixReboteSeleccion = true;
    public float lockSeleccionSegs = 0.20f;

    [Header("Fix - Rebote selección (SYS CONFIG)")]
    public bool fixReboteSeleccionOptions = true;
    public float lockSeleccionOptionsSegs = 0.20f;

    // Interno
    RenderTexture _rt;
    Camera _rtCamera;
    CanvasGroup _cg;

    EventSystem _es;
    StandaloneInputModule _standalone;
    BaseInput _prevInputOverride;
    LaptopRTInput _rtInput;

    readonly List<BaseInputModule> _otrosModulos = new List<BaseInputModule>();
    readonly List<bool> _otrosModulosPrevEnabled = new List<bool>();

    Button _btnStart, _btnOptions, _btnLogs, _btnExit;
    Button _btnBackOptions, _btnBackLogs;

    GameObject _panelMain, _panelOptions, _panelLogs;
    Slider _sMaster, _sMusic, _sSfx;

    bool _menuVisible;
    bool _uiDirty;
    float _nextRebuildTime;

    // lock anti-rebote (Main)
    bool _navPressedThisFrame;
    float _lockSelUntil;
    GameObject _lockSelGO;

    // lock anti-rebote (Options)
    bool _optNavPressedThisFrame;
    float _optLockSelUntil;
    GameObject _optLockSelGO;
    Coroutine _coOptCaptureLock;

    // SYS CONFIG tracking
    Selectable _optionsLast;
    int _optionsLastSlot = 0;

    // bloqueo hover OPTIONS mientras usas teclado (hasta mover mouse)
    bool _optionsHoverBlocked;
    Vector3 _optionsMousePosWhenBlocked;

    // bloqueo hover MAIN mientras usas teclado (hasta mover mouse)
    bool _mainHoverBlocked;
    Vector3 _mainMousePosWhenBlocked;

    PanelId _panelActual = PanelId.Main;

    // LOGS typing
    TextMeshProUGUI _logsBodyTMP;
    Coroutine _logsTypingCo;
    bool _logsTypedOnce;
    bool _logsStartedThisOpen;
    float _logsBlinkAcc;
    bool _logsCursorOn = true;

    // ✅ LOGS typing SFX (interno)
    AudioSource _logsTypingSrc;
    Coroutine _logsTypingFadeCo;

    // coroutine anti-enter separada
    Coroutine _coUnlockSubmit;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

    // =========================
    //  PUBLIC (para FX)
    // =========================
    internal bool OptionsHoverBlocked => (_panelActual == PanelId.Options) && _optionsHoverBlocked;
    internal bool MainHoverBlocked => (_panelActual == PanelId.Main) && _mainHoverBlocked;

    void Awake()
    {
        EnsureRT();
        EnsureCamera();
        AplicarRTyST();

        RebuildUI_Internal();
        ConfigurarEventSystem();

        SetMenuVisible(false);
        EnsureSessionAudioDefaults();

        // ✅ Typing audio (solo play)
        EnsureLogsTypingAudio();
    }

    void OnEnable()
    {
        EnsureRT();
        EnsureCamera();
        AplicarRTyST();
        EnsureSessionAudioDefaults();

        // ✅ Typing audio (solo play)
        EnsureLogsTypingAudio();
    }

    void OnDestroy()
    {
        StopLogsTyping();
        StopUnlockSubmit();
        StopOptionsCaptureLock();

        if (_rtCamera != null && _rtCamera.targetTexture == _rt)
            _rtCamera.targetTexture = null;
        if (pantallaRenderer != null)
        {
            // ✅ No usar SetTexture(..., null). Limpiar el MPB completo.
            var mpb = new MaterialPropertyBlock();
            mpb.Clear();
            pantallaRenderer.SetPropertyBlock(mpb, materialIndex);
        }

if (_rt != null)
        {
            _rt.Release();
            DestroyImmediateSafe(_rt);
            _rt = null;
        }
    }

    public void MostrarMenu(bool animado = true) => SetMenuVisible(true);
    public void OcultarMenu(bool animado = true) => SetMenuVisible(false);

    void Update()
    {
        // Live rebuild en Play
        if (Application.isPlaying && liveRebuildInPlay && _uiDirty && Time.unscaledTime >= _nextRebuildTime)
        {
            _nextRebuildTime = Time.unscaledTime + rebuildCooldown;
            _uiDirty = false;

            bool wasVisible = _menuVisible;
            PanelId keepPanel = _panelActual;

            RebuildUI_Internal();
            AplicarRTyST();
            ConfigurarEventSystem();

            ApplyMenuVisibleAfterRebuild(wasVisible);
            SetPanel(keepPanel, true);
        }

        // Live rebuild en Editor
        if (!Application.isPlaying && liveRebuildInEditor && _uiDirty)
        {
            _uiDirty = false;
            RebuildUI_Internal();
            AplicarRTyST();
            ConfigurarEventSystem();
        }

        if (!Application.isPlaying || !_menuVisible) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_panelOptions != null && _panelOptions.activeSelf) SetPanel(PanelId.Main, true);
            else if (_panelLogs != null && _panelLogs.activeSelf) SetPanel(PanelId.Main, true);
        }

        // MAIN: bloqueo hover mientras navegas con teclado
        if (_panelActual == PanelId.Main)
        {
            bool navKeyDown =
                Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.W);

            if (navKeyDown)
            {
                _mainHoverBlocked = true;
                _mainMousePosWhenBlocked = Input.mousePosition;

                ClearMainFxStates();

                if (_rtInput != null) _rtInput.BlockPointer(lockSeleccionSegs);
            }
            else if (_mainHoverBlocked)
            {
                if (_rtInput != null) _rtInput.BlockPointer(0.05f);

                Vector3 cur = Input.mousePosition;
                if ((cur - _mainMousePosWhenBlocked).sqrMagnitude > 4f)
                {
                    _mainHoverBlocked = false;
                    ClearMainFxStates();
                }
            }
        }

        // OPTIONS: Fix rebote + bloqueo hover hasta mover mouse
        if (_panelActual == PanelId.Options)
        {
            bool navKeyDown =
                Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.W);

            if (navKeyDown)
            {
                _optionsHoverBlocked = true;
                _optionsMousePosWhenBlocked = Input.mousePosition;

                ClearAllOptionsHoverVisuals();

                if (fixReboteSeleccionOptions && _es != null)
                {
                    _optNavPressedThisFrame = true;
                    if (_rtInput != null) _rtInput.BlockPointer(lockSeleccionOptionsSegs);
                }
            }
            else if (_optionsHoverBlocked)
            {
                if (_rtInput != null) _rtInput.BlockPointer(0.05f);

                Vector3 cur = Input.mousePosition;
                if ((cur - _optionsMousePosWhenBlocked).sqrMagnitude > 4f)
                {
                    _optionsHoverBlocked = false;
                    ClearAllOptionsHoverVisuals();
                }
            }
        }

        // MAIN: Fix rebote selección
        if (_panelActual == PanelId.Main && fixReboteSeleccion)
        {
            bool navKey =
                Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.W);

            if (navKey && _es != null)
            {
                _navPressedThisFrame = true;
                if (_rtInput != null) _rtInput.BlockPointer(lockSeleccionSegs);
            }
        }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!_menuVisible) return;
        if (_es == null) return;

        // LOGS: BACK siempre seleccionado
        if (_panelActual == PanelId.Logs)
        {
            if (_btnBackLogs != null && _es.currentSelectedGameObject != _btnBackLogs.gameObject)
                _es.SetSelectedGameObject(_btnBackLogs.gameObject);
            return;
        }

        // OPTIONS lock
        if (_panelActual == PanelId.Options)
        {
            if (fixReboteSeleccionOptions)
            {
                if (_optNavPressedThisFrame)
                {
                    _optNavPressedThisFrame = false;

                    // ✅ FIX: limpiar el lock viejo para que NO rebote a Master cuando querés ir a BACK
                    _optLockSelGO = null;
                    _optLockSelUntil = 0f;

                    StopOptionsCaptureLock();
                    _coOptCaptureLock = StartCoroutine(CO_CaptureOptionsLockNextFrame());
                }

                if (Time.unscaledTime < _optLockSelUntil && _optLockSelGO != null)
                {
                    if (_es.currentSelectedGameObject != _optLockSelGO)
                        _es.SetSelectedGameObject(_optLockSelGO);
                    return;
                }
            }

            EnforceOptionsSelection();
            return;
        }

        // MAIN lock
        if (!fixReboteSeleccion) return;
        if (_panelActual != PanelId.Main) return;

        if (_navPressedThisFrame)
        {
            _navPressedThisFrame = false;

            var cur = _es.currentSelectedGameObject;
            if (cur == null && _btnStart != null) cur = _btnStart.gameObject;

            _lockSelGO = cur;
            _lockSelUntil = Time.unscaledTime + lockSeleccionSegs;
        }

        if (Time.unscaledTime < _lockSelUntil && _lockSelGO != null)
        {
            if (_es.currentSelectedGameObject != _lockSelGO)
                _es.SetSelectedGameObject(_lockSelGO);
        }
    }

    IEnumerator CO_CaptureOptionsLockNextFrame()
    {
        yield return null;

        if (_panelActual != PanelId.Options || _es == null)
        {
            _coOptCaptureLock = null;
            yield break;
        }

        var cur = _es.currentSelectedGameObject;
        if (cur == null && _sMaster != null) cur = _sMaster.gameObject;

        _optLockSelGO = cur;
        _optLockSelUntil = Time.unscaledTime + Mathf.Max(0.01f, lockSeleccionOptionsSegs);

        _coOptCaptureLock = null;
    }

    void StopOptionsCaptureLock()
    {
        if (_coOptCaptureLock != null)
        {
            StopCoroutine(_coOptCaptureLock);
            _coOptCaptureLock = null;
        }
    }

    // =========================
    //  Hover/Select cleanup MAIN
    // =========================
    void ClearMainFxStates()
    {
        if (_panelMain == null || _es == null) return;

        var ped = new PointerEventData(_es);

        var fxs = _panelMain.GetComponentsInChildren<SciFiButtonFX>(true);
        foreach (var fx in fxs)
        {
            ExecuteEvents.Execute<IPointerExitHandler>(fx.gameObject, ped, ExecuteEvents.pointerExitHandler);
        }
    }

    void ClearAllOptionsHoverVisuals()
    {
        if (_panelOptions == null) return;
        var fxs = _panelOptions.GetComponentsInChildren<SliderPulseFX>(true);
        foreach (var fx in fxs) fx.ForceHoverOffOnly();
    }

    void EnforceOptionsSelection()
    {
        var curGO = _es.currentSelectedGameObject;

        if (IsOptionsSelectable(curGO))
        {
            if (_sMaster != null && curGO == _sMaster.gameObject) NotifyOptionsSelected(_sMaster, 0);
            else if (_sMusic != null && curGO == _sMusic.gameObject) NotifyOptionsSelected(_sMusic, 1);
            else if (_sSfx != null && curGO == _sSfx.gameObject) NotifyOptionsSelected(_sSfx, 2);
            else if (_btnBackOptions != null && curGO == _btnBackOptions.gameObject) NotifyOptionsSelected(_btnBackOptions, 3);
            return;
        }

        Selectable desired = _optionsLast != null ? _optionsLast : _sMaster;
        if (desired != null && desired.gameObject != null)
            _es.SetSelectedGameObject(desired.gameObject);
    }

    bool IsOptionsSelectable(GameObject go)
    {
        if (go == null) return false;
        if (_sMaster != null && go == _sMaster.gameObject) return true;
        if (_sMusic != null && go == _sMusic.gameObject) return true;
        if (_sSfx != null && go == _sSfx.gameObject) return true;
        if (_btnBackOptions != null && go == _btnBackOptions.gameObject) return true;
        return false;
    }

    void LockOptionsSelectionNow(GameObject go, float secs)
    {
        if (!fixReboteSeleccionOptions) return;
        if (_es == null || go == null) return;

        _optLockSelGO = go;
        _optLockSelUntil = Time.unscaledTime + Mathf.Max(0.01f, secs);

        if (_es.currentSelectedGameObject != go)
            _es.SetSelectedGameObject(go);
    }

    void MarkUIDirty()
    {
        _uiDirty = true;
        _nextRebuildTime = Application.isPlaying ? Time.unscaledTime + rebuildCooldown : 0f;
    }

    void OnValidate()
    {
        MarkUIDirty();
        if (pantallaRenderer != null) AplicarRTyST();
    }

    // =========================
    //  LOGS typewriter (REALTIME)
    // =========================
    void LockNavToSelf(Selectable s)
    {
        if (s == null) return;
        var nav = new Navigation { mode = Navigation.Mode.Explicit };
        nav.selectOnUp = s;
        nav.selectOnDown = s;
        nav.selectOnLeft = s;
        nav.selectOnRight = s;
        s.navigation = nav;
    }

    // ✅ Ensure audio source for logs typing (solo play)
    void EnsureLogsTypingAudio()
    {
        if (!Application.isPlaying) return;
        if (_logsTypingSrc != null) return;

        Transform t = transform.Find("LogsTypingAudio");
        if (t == null)
        {
            var go = new GameObject("LogsTypingAudio");
            go.transform.SetParent(transform, false);
            t = go.transform;
        }

        _logsTypingSrc = t.GetComponent<AudioSource>();
        if (_logsTypingSrc == null) _logsTypingSrc = t.gameObject.AddComponent<AudioSource>();

        _logsTypingSrc.playOnAwake = false;
        _logsTypingSrc.loop = true;
        _logsTypingSrc.spatialBlend = 0f;
        _logsTypingSrc.volume = Mathf.Clamp01(logsTypingSfxVol);

        if (logsTypingMixerGroup != null)
            _logsTypingSrc.outputAudioMixerGroup = logsTypingMixerGroup;
    }

    void LogsTypingSFX_Start()
    {
        if (!Application.isPlaying) return;
        if (!logsTypingSfxEnabled) return;
        if (logsTypingLoopClip == null) return;

        EnsureLogsTypingAudio();

        if (_logsTypingFadeCo != null)
        {
            StopCoroutine(_logsTypingFadeCo);
            _logsTypingFadeCo = null;
        }

        if (_logsTypingSrc.clip != logsTypingLoopClip)
            _logsTypingSrc.clip = logsTypingLoopClip;

        _logsTypingSrc.volume = Mathf.Clamp01(logsTypingSfxVol);

        if (!_logsTypingSrc.isPlaying)
            _logsTypingSrc.Play();
    }

    void LogsTypingSFX_Stop(bool instant = false)
    {
        if (_logsTypingSrc == null) return;

        // ✅ Si el GO está inactivo o el componente no está enabled, NO intentes coroutine.
        // (Unity tira: "Coroutine couldn't be started because the game object is inactive")
        if (instant || !Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy || logsTypingSfxFadeOut <= 0.001f)
        {
            if (_logsTypingFadeCo != null)
            {
                StopCoroutine(_logsTypingFadeCo);
                _logsTypingFadeCo = null;
            }

            if (_logsTypingSrc.isPlaying) _logsTypingSrc.Stop();
            _logsTypingSrc.volume = Mathf.Clamp01(logsTypingSfxVol);
            return;
        }

        if (_logsTypingFadeCo != null)
        {
            StopCoroutine(_logsTypingFadeCo);
            _logsTypingFadeCo = null;
        }

        _logsTypingFadeCo = StartCoroutine(Co_FadeOutStopLogsTyping(logsTypingSfxFadeOut));
    }

    IEnumerator Co_FadeOutStopLogsTyping(float dur)
    {
        if (_logsTypingSrc == null) yield break;

        float startVol = _logsTypingSrc.volume;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            _logsTypingSrc.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        if (_logsTypingSrc.isPlaying) _logsTypingSrc.Stop();
        _logsTypingSrc.volume = Mathf.Clamp01(logsTypingSfxVol);
        _logsTypingFadeCo = null;
    }

    void StartLogsTypingIfNeeded()
    {
        if (!Application.isPlaying) return;
        if (!logsTypewriterEnabled) return;
        if (_logsBodyTMP == null) return;

        if (_logsTypingCo != null) return;
        if (logsDoNotRestartWhileOpen && _logsStartedThisOpen) return;

        _logsStartedThisOpen = true;

        StopLogsTyping();
        _logsTypingCo = StartCoroutine(Co_TypeLogs_Staged_RT());
    }

    void StopLogsTyping()
    {
        if (_logsTypingCo != null)
        {
            StopCoroutine(_logsTypingCo);
            _logsTypingCo = null;
        }

        // ✅ en destroys/desactivaciones: instant (sin coroutine)
        LogsTypingSFX_Stop(true);
    }

    void LogsStepBlink(float dt, float blinkInterval)
    {
        _logsBlinkAcc += dt;
        if (_logsBlinkAcc >= blinkInterval)
        {
            _logsBlinkAcc -= blinkInterval;
            _logsCursorOn = !_logsCursorOn;
        }
    }

    void LogsRefreshTMP(TextMeshProUGUI tmp, StringBuilder sb, string cursorChar)
    {
        tmp.text = _logsCursorOn ? (sb.ToString() + cursorChar) : sb.ToString();
    }

    IEnumerator LogsTypeSegment_RT(TextMeshProUGUI tmp, StringBuilder sb, string seg, float cps, string cursorChar, float blinkInterval, bool forceFirstCharNow)
    {
        if (string.IsNullOrEmpty(seg)) yield break;

        float last = Time.realtimeSinceStartup;
        int i = 0;
        float acc = 0f;

        if (forceFirstCharNow && seg.Length > 0)
        {
            sb.Append(seg[0]);
            i = 1;
            _logsCursorOn = true;
            LogsRefreshTMP(tmp, sb, cursorChar);
            yield return null;
            last = Time.realtimeSinceStartup;
        }

        while (i < seg.Length)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - last);
            last = now;

            acc += dt * cps;
            int add = Mathf.FloorToInt(acc);

            if (add > 0)
            {
                acc -= add;
                int target = Mathf.Min(seg.Length, i + add);
                sb.Append(seg, i, target - i);
                i = target;
            }

            LogsStepBlink(dt, blinkInterval);
            LogsRefreshTMP(tmp, sb, cursorChar);
            yield return null;
        }
    }

    IEnumerator LogsBlinkPause_RT(TextMeshProUGUI tmp, StringBuilder sb, float secs, string cursorChar, float blinkInterval)
    {
        secs = Mathf.Max(0f, secs);
        float start = Time.realtimeSinceStartup;
        float last = start;

        while (Time.realtimeSinceStartup - start < secs)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - last);
            last = now;

            LogsStepBlink(dt, blinkInterval);
            LogsRefreshTMP(tmp, sb, cursorChar);
            yield return null;
        }

        _logsCursorOn = true;
        LogsRefreshTMP(tmp, sb, cursorChar);
    }

    IEnumerator LogsBlinkForever_RT(TextMeshProUGUI tmp, StringBuilder sb, string cursorChar, float blinkInterval)
    {
        float last = Time.realtimeSinceStartup;

        while (_menuVisible && _panelActual == PanelId.Logs && tmp != null)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - last);
            last = now;

            LogsStepBlink(dt, blinkInterval);
            LogsRefreshTMP(tmp, sb, cursorChar);
            yield return null;
        }
    }

    IEnumerator Co_TypeLogs_Staged_RT()
    {
        var inner = Co_TypeLogs_Staged_RT_Inner();

        while (true)
        {
            object yielded = null;
            bool moved = false;

            try
            {
                moved = inner.MoveNext();
                if (moved) yielded = inner.Current;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                break;
            }

            if (!moved) break;
            yield return yielded;
        }

        // ✅ seguridad: que nunca quede colgado el loop
        LogsTypingSFX_Stop(true);

        _logsTypingCo = null;
    }

    IEnumerator Co_TypeLogs_Staged_RT_Inner()
    {
        _logsBlinkAcc = 0f;
        _logsCursorOn = true;

        string full = (logsTexto ?? "").Replace("\r\n", "\n").TrimEnd('\n', '\r');
        string[] lines = full.Split('\n');

        string l0 = lines.Length > 0 ? lines[0] : "";
        string l1 = lines.Length > 1 ? lines[1] : "";

        float blink = Mathf.Max(0.02f, logsCursorBlinkInterval);
        string cursor = string.IsNullOrEmpty(logsCursorChar) ? "█" : logsCursorChar;

        float cpsFast = Mathf.Max(1f, logsCharsPerSecondFirstTwo);
        float cpsRest = Mathf.Max(1f, logsCharsPerSecondRest);

        var sb = new StringBuilder(full.Length + 8);

        if (!logsReplayEveryOpen && _logsTypedOnce)
        {
            _logsBodyTMP.text = full;

            LogsTypingSFX_Stop(true);

            if (!logsCursorDisappearAtEnd && logsKeepBlinkingAtEnd)
                yield return LogsBlinkForever_RT(_logsBodyTMP, sb, cursor, blink);
            yield break;
        }

        _logsTypedOnce = true;
        _logsBodyTMP.text = "";

        yield return null;
        if (logsStartDelay > 0f)
            yield return new WaitForSecondsRealtime(logsStartDelay);

        if (!string.IsNullOrEmpty(l0))
        {
            LogsTypingSFX_Start();
            yield return LogsTypeSegment_RT(_logsBodyTMP, sb, l0, cpsFast, cursor, blink, false);
            LogsTypingSFX_Stop();

            yield return LogsBlinkPause_RT(_logsBodyTMP, sb, logsHoldAfterFirstLine, cursor, blink);
        }

        if (!string.IsNullOrEmpty(l1))
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');

            LogsTypingSFX_Start();
            yield return LogsTypeSegment_RT(_logsBodyTMP, sb, l1, cpsFast, cursor, blink, true);
            LogsTypingSFX_Stop();

            yield return LogsBlinkPause_RT(_logsBodyTMP, sb, logsHoldAfterSecondLine, cursor, blink);
        }

        if (lines.Length > 2)
        {
            var rest = new StringBuilder();
            for (int i = 2; i < lines.Length; i++)
            {
                rest.Append(lines[i]);
                if (i < lines.Length - 1) rest.Append('\n');
            }

            string restStr = rest.ToString();
            if (!string.IsNullOrEmpty(restStr))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');

                LogsTypingSFX_Start();
                yield return LogsTypeSegment_RT(_logsBodyTMP, sb, restStr, cpsRest, cursor, blink, true);
                LogsTypingSFX_Stop();
            }
        }

        LogsTypingSFX_Stop(true);

        if (logsCursorDisappearAtEnd)
        {
            _logsCursorOn = false;
            _logsBodyTMP.text = sb.ToString();
            yield break;
        }

        if (logsKeepBlinkingAtEnd)
            yield return LogsBlinkForever_RT(_logsBodyTMP, sb, cursor, blink);
        else
            _logsBodyTMP.text = sb.ToString();
    }

    // =========================
    //  RT / CAM
    // =========================
    void EnsureRT()
    {
        if (_rt != null && _rt.width == rtAncho && _rt.height == rtAlto) return;

        if (_rt != null)
        {
            if (_rtCamera != null && _rtCamera.targetTexture == _rt)
                _rtCamera.targetTexture = null;

            _rt.Release();
            DestroyImmediateSafe(_rt);
        }

        _rt = new RenderTexture(rtAncho, rtAlto, 24, RenderTextureFormat.ARGB32);
        _rt.name = "RT_LaptopMenu";
        _rt.filterMode = FilterMode.Bilinear;
        _rt.wrapMode = TextureWrapMode.Clamp;
        _rt.Create();

        if (_rtCamera != null) _rtCamera.targetTexture = _rt;
    }

    void EnsureCamera()
    {
        if (_rtCamera != null) return;

        var existing = transform.Find("LaptopMenu_RTCamera");
        if (existing != null) _rtCamera = existing.GetComponent<Camera>();

        if (_rtCamera == null)
        {
            var camGO = new GameObject("LaptopMenu_RTCamera");
            camGO.transform.SetParent(transform, false);
            _rtCamera = camGO.AddComponent<Camera>();
        }

        _rtCamera.clearFlags = CameraClearFlags.SolidColor;
        _rtCamera.backgroundColor = new Color(0, 0, 0, 0);
        _rtCamera.orthographic = true;
        _rtCamera.orthographicSize = 1f;
        _rtCamera.nearClipPlane = 0.01f;
        _rtCamera.farClipPlane = 100f;
        _rtCamera.targetTexture = _rt;

        int layerLaptopUI = LayerMask.NameToLayer("LaptopUI");
        if (layerLaptopUI < 0)
        {
            Debug.LogError("[LaptopMenu] No existe el layer 'LaptopUI'. Crealo en Tags and Layers.");
            layerLaptopUI = 5;
        }

        _rtCamera.cullingMask = 1 << layerLaptopUI;
        _rtCamera.aspect = rtAncho / (float)rtAlto;
    }

    void AplicarRTyST()
    {
        if (!pantallaRenderer || _rt == null) return;

        var mpb = new MaterialPropertyBlock();
        pantallaRenderer.GetPropertyBlock(mpb, materialIndex);

        mpb.SetTexture(BaseMap, _rt);
        mpb.SetTexture(MainTex, _rt);

        if (aplicarBaseMapST)
        {
            Vector2 til = screenTiling;
            Vector2 off = screenOffset;

            if (autoCenterOffset)
                off = new Vector2((1f - til.x) * 0.5f, (1f - til.y) * 0.5f);

            mpb.SetVector(BaseMapST, new Vector4(til.x, til.y, off.x, off.y));
            mpb.SetVector(MainTexST, new Vector4(til.x, til.y, off.x, off.y));
        }

        pantallaRenderer.SetPropertyBlock(mpb, materialIndex);
    }

    void ConfigurarEventSystem()
    {
        var all = FindObjectsOfType<EventSystem>(true);
        if (all != null && all.Length > 0)
        {
            _es = all[0];
            for (int i = 1; i < all.Length; i++)
                all[i].enabled = false;
        }

        if (_es == null)
        {
            var go = new GameObject("EventSystem");
            _es = go.AddComponent<EventSystem>();
        }

        _es.sendNavigationEvents = true;

        _standalone = _es.GetComponent<StandaloneInputModule>();
        if (_standalone == null) _standalone = _es.gameObject.AddComponent<StandaloneInputModule>();

        _standalone.inputActionsPerSecond = 30f;
        _standalone.repeatDelay = 0.22f;

        _otrosModulos.Clear();
        _otrosModulosPrevEnabled.Clear();
        var mods = _es.GetComponents<BaseInputModule>();
        foreach (var m in mods)
        {
            if (m == _standalone) continue;
            _otrosModulos.Add(m);
            _otrosModulosPrevEnabled.Add(m.enabled);
        }

        _rtInput = _es.GetComponent<LaptopRTInput>();
        if (_rtInput == null) _rtInput = _es.gameObject.AddComponent<LaptopRTInput>();

        _rtInput.camaraRaycast = Camera.main;
        _rtInput.pantallaCollider = pantallaCollider;
        _rtInput.renderTexture = _rt;

        _prevInputOverride = _standalone.inputOverride;
    }

    // =========================
    //  SAFE AREA (por tiling)
    // =========================
    float SafeX()
    {
        float baseSafe = aplicarBaseMapST ? Mathf.Max(0f, (1f - screenTiling.x) * 0.5f) : 0f;
        float extra = Mathf.Max(0f, safeInsetExtra.x);
        return Mathf.Clamp01(baseSafe + extra);
    }

    float SafeY()
    {
        float baseSafe = aplicarBaseMapST ? Mathf.Max(0f, (1f - screenTiling.y) * 0.5f) : 0f;
        float extra = Mathf.Max(0f, safeInsetExtra.y);
        return Mathf.Clamp01(baseSafe + extra);
    }

    Vector2 Safe01ToAnchors(Vector2 a01, float sx, float sy)
    {
        float w = Mathf.Clamp01(1f - 2f * sx);
        float h = Mathf.Clamp01(1f - 2f * sy);

        float x = sx + Mathf.Clamp01(a01.x) * w;
        float y = sy + Mathf.Clamp01(a01.y) * h;

        return new Vector2(x, y);
    }

    void ApplyHudRect(RectTransform rt, Vector2 amin01, Vector2 amax01, Vector2 offMinPx, Vector2 offMaxPx, float sx, float sy)
    {
        rt.anchorMin = Safe01ToAnchors(amin01, sx, sy);
        rt.anchorMax = Safe01ToAnchors(amax01, sx, sy);
        rt.offsetMin = offMinPx * uiScale;
        rt.offsetMax = offMaxPx * uiScale;
    }

    // =========================
    //  UI BUILD
    // =========================
    void RebuildUI_Internal()
    {
        int layerLaptopUI = LayerMask.NameToLayer("LaptopUI");

        var existing = transform.Find("LaptopMenu_Canvas");
        if (existing != null)
            DestroyImmediateSafe(existing.gameObject);

        var canvasGO = new GameObject("LaptopMenu_Canvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.layer = layerLaptopUI;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _rtCamera;
        canvas.planeDistance = 1f;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        canvasGO.AddComponent<GraphicRaycaster>();
        _cg = canvasGO.AddComponent<CanvasGroup>();

        var bg = UI<Image>("BG", canvasGO.transform, layerLaptopUI);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        if (backgroundSprite)
        {
            bg.sprite = backgroundSprite;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }
        else bg.color = new Color(0.02f, 0.03f, 0.05f, 1f);

        var overlay = UI<Image>("Overlay", canvasGO.transform, layerLaptopUI);
        Stretch(overlay.rectTransform);
        overlay.color = new Color(0, 0, 0, Mathf.Clamp01(overlayAlpha));
        overlay.raycastTarget = false;

        float sx = SafeX();
        float sy = SafeY();

        // HUD
        if (mostrarEtiquetaTerminal && !string.IsNullOrEmpty(etiquetaTerminal))
        {
            var term = UI<TextMeshProUGUI>("TerminalLabel", canvasGO.transform, layerLaptopUI);
            term.text = etiquetaTerminal;
            term.font = fontTMP ? fontTMP : term.font;
            term.fontSize = Mathf.RoundToInt(26 * uiScale);
            term.color = new Color(cianUI.r, cianUI.g, cianUI.b, 0.85f);
            term.alignment = TextAlignmentOptions.TopLeft;
            term.raycastTarget = false;

            ApplyHudRect(term.rectTransform, terminalAnchorMin01, terminalAnchorMax01, terminalOffsetMinPx, terminalOffsetMaxPx, sx, sy);
        }

        if (mostrarHint && !string.IsNullOrEmpty(hintTexto))
        {
            var hint = UI<TextMeshProUGUI>("HintLabel", canvasGO.transform, layerLaptopUI);
            hint.text = hintTexto;
            hint.font = fontTMP ? fontTMP : hint.font;
            hint.fontSize = Mathf.RoundToInt(24 * uiScale);
            hint.color = new Color(cianTexto.r, cianTexto.g, cianTexto.b, 0.85f);
            hint.alignment = TextAlignmentOptions.BottomLeft;
            hint.raycastTarget = false;

            ApplyHudRect(hint.rectTransform, hintAnchorMin01, hintAnchorMax01, hintOffsetMinPx, hintOffsetMaxPx, sx, sy);
        }

        if (mostrarVersion && !string.IsNullOrEmpty(versionTexto))
        {
            var ver = UI<TextMeshProUGUI>("VersionLabel", canvasGO.transform, layerLaptopUI);
            ver.text = versionTexto;
            ver.font = fontTMP ? fontTMP : ver.font;
            ver.fontSize = Mathf.RoundToInt(24 * uiScale);
            ver.color = new Color(cianTexto.r, cianTexto.g, cianTexto.b, 0.70f);
            ver.alignment = TextAlignmentOptions.BottomRight;
            ver.raycastTarget = false;

            ApplyHudRect(ver.rectTransform, versionAnchorMin01, versionAnchorMax01, versionOffsetMinPx, versionOffsetMaxPx, sx, sy);
        }

        // Panel Main
        _panelMain = new GameObject("PanelMain");
        _panelMain.transform.SetParent(canvasGO.transform, false);
        _panelMain.layer = layerLaptopUI;

        var prt = _panelMain.AddComponent<RectTransform>();
        float xMin = Mathf.Clamp01(sx + panelOffset.x);
        float yMin = Mathf.Clamp01(sy + panelOffset.y);
        float xMax = Mathf.Min(1f - sx, xMin + panelWidth);
        float yMax = Mathf.Min(1f - sy, yMin + panelHeight);
        prt.anchorMin = new Vector2(xMin, yMin);
        prt.anchorMax = new Vector2(xMax, yMax);

        Transform contenedorBotones = _panelMain.transform;

        if (usarFondoSoloDetrasDeBotones)
        {
            var menuBG = new GameObject("MenuBG");
            menuBG.transform.SetParent(_panelMain.transform, false);
            menuBG.layer = layerLaptopUI;

            var bgrt = menuBG.AddComponent<RectTransform>();
            bgrt.anchorMin = menuBgAnchorMin;
            bgrt.anchorMax = menuBgAnchorMax;

            var bgImg = menuBG.AddComponent<Image>();
            bgImg.color = fondoPanel;
            bgImg.raycastTarget = false;

            contenedorBotones = menuBG.transform;
        }
        else
        {
            var pImg = _panelMain.AddComponent<Image>();
            pImg.color = fondoPanel;
            pImg.raycastTarget = false;
        }

        var botonera = new GameObject("Botonera");
        botonera.transform.SetParent(contenedorBotones, false);
        botonera.layer = layerLaptopUI;

        var brt = botonera.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.08f, 0.10f);
        brt.anchorMax = new Vector2(0.92f, 0.92f);

        var vlg = botonera.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = Mathf.RoundToInt(18 * uiScale);

        _btnStart = CrearBotonSciFi(botonera.transform, labelStart, layerLaptopUI, ambarSelect);
        _btnOptions = CrearBotonSciFi(botonera.transform, labelOptions, layerLaptopUI, ambarSelect);
        _btnLogs = CrearBotonSciFi(botonera.transform, labelLogs, layerLaptopUI, ambarSelect);
        _btnExit = CrearBotonSciFi(botonera.transform, labelExit, layerLaptopUI, rojoExit);

        _btnStart.onClick.AddListener(OnStartGame);
        _btnOptions.onClick.AddListener(() => SetPanel(PanelId.Options, true));
        _btnLogs.onClick.AddListener(() => SetPanel(PanelId.Logs, true));
        _btnExit.onClick.AddListener(OnExit);

        SetVerticalNav(_btnStart, null, _btnOptions);
        SetVerticalNav(_btnOptions, _btnStart, _btnLogs);
        SetVerticalNav(_btnLogs, _btnOptions, _btnExit);
        SetVerticalNav(_btnExit, _btnLogs, null);

        _panelOptions = CrearPanelOptions(canvasGO.transform, layerLaptopUI, sx, sy);
        _panelLogs = CrearPanelLogs(canvasGO.transform, layerLaptopUI, sx, sy);

        _panelOptions.SetActive(false);
        _panelLogs.SetActive(false);
    }

    // ------------------ RESTO DEL SCRIPT ------------------
    // A partir de acá es exactamente lo que pegaste (no modifiqué lógica),
    // solo está incluido para que tengas el archivo completo compilable.

    GameObject CrearPanelOptions(Transform parent, int layer, float sx, float sy)
    {
        var panel = new GameObject("Panel_Options");
        panel.transform.SetParent(parent, false);
        panel.layer = layer;

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(sx + overlayInsetMin.x, sy + overlayInsetMin.y);
        rt.anchorMax = new Vector2(1f - sx - overlayInsetMax.x, 1f - sy - overlayInsetMax.y);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = fondoPanel;
        panelImg.raycastTarget = false;

        var title = UI<TextMeshProUGUI>("Title", panel.transform, layer);
        title.text = "SYS CONFIG";
        title.font = fontTMP ? fontTMP : title.font;
        title.fontSize = Mathf.RoundToInt(48 * uiScale);
        title.color = cianUI;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.raycastTarget = false;
        title.rectTransform.anchorMin = new Vector2(0.08f, 0.80f);
        title.rectTransform.anchorMax = new Vector2(0.92f, 0.95f);

        var cont = new GameObject("Sliders");
        cont.transform.SetParent(panel.transform, false);
        cont.layer = layer;

        var crt = cont.AddComponent<RectTransform>();
        crt.anchorMin = optionsContentAnchorMin;
        crt.anchorMax = optionsContentAnchorMax;

        var vlg = cont.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = Mathf.RoundToInt(14 * uiScale);

        _sMaster = CrearSliderFilaHorizontal(cont.transform, layer, "Master Volume", 0);
        _sMusic = CrearSliderFilaHorizontal(cont.transform, layer, "Music Volume", 1);
        _sSfx = CrearSliderFilaHorizontal(cont.transform, layer, "SFX Volume", 2);

        AplicarAudioASlidersDesdeSesion();

        _sMaster.onValueChanged.AddListener(v => OnSliderChanged(_sMaster, 0, paramMaster, v, ref s_master01));
        _sMusic.onValueChanged.AddListener(v => OnSliderChanged(_sMusic, 1, paramMusic, v, ref s_music01));
        _sSfx.onValueChanged.AddListener(v => OnSliderChanged(_sSfx, 2, paramSfx, v, ref s_sfx01));

        _btnBackOptions = CrearBotonBack(panel.transform, "BACK", layer);
        var brt = _btnBackOptions.GetComponent<RectTransform>();
        brt.anchorMin = backAnchorMin;
        brt.anchorMax = backAnchorMax;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        _btnBackOptions.onClick.AddListener(() => SetPanel(PanelId.Main, true));

        SetVerticalNav(_sMaster, null, _sMusic);
        SetVerticalNav(_sMusic, _sMaster, _sSfx);
        SetVerticalNav(_sSfx, _sMusic, _btnBackOptions);
        SetVerticalNav(_btnBackOptions, _sSfx, null);

        return panel;
    }

    void OnSliderChanged(Selectable sel, int slot, string param, float v, ref float cache)
    {
        SetMixer01(param, v, ref cache);

        if (_panelActual != PanelId.Options) return;
        if (_es == null || sel == null) return;

        NotifyOptionsSelected(sel, slot);

        if (_es.currentSelectedGameObject != sel.gameObject)
            _es.SetSelectedGameObject(sel.gameObject);

        if (fixReboteSeleccionOptions)
        {
            _optLockSelGO = sel.gameObject;
            _optLockSelUntil = Time.unscaledTime + 0.12f;
        }
    }

    GameObject CrearPanelLogs(Transform parent, int layer, float sx, float sy)
    {
        var panel = new GameObject("Panel_Logs");
        panel.transform.SetParent(parent, false);
        panel.layer = layer;

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(sx + overlayInsetMin.x, sy + overlayInsetMin.y);
        rt.anchorMax = new Vector2(1f - sx - overlayInsetMax.x, 1f - sy - overlayInsetMax.y);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = fondoPanel;
        panelImg.raycastTarget = false;

        var title = UI<TextMeshProUGUI>("Title", panel.transform, layer);
        title.text = logsTitulo;
        title.font = fontTMP ? fontTMP : title.font;
        title.fontSize = Mathf.RoundToInt(48 * uiScale);
        title.color = cianUI;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.raycastTarget = false;
        title.rectTransform.anchorMin = new Vector2(0.08f, 0.80f);
        title.rectTransform.anchorMax = new Vector2(0.92f, 0.95f);

        var body = UI<TextMeshProUGUI>("Body", panel.transform, layer);
        body.text = logsTexto;
        body.font = fontTMP ? fontTMP : body.font;
        body.fontSize = Mathf.RoundToInt(30 * uiScale);
        body.color = cianTexto;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.raycastTarget = false;
        body.rectTransform.anchorMin = logsBodyAnchorMin;
        body.rectTransform.anchorMax = logsBodyAnchorMax;

        _logsBodyTMP = body;

        _btnBackLogs = CrearBotonBack(panel.transform, "BACK", layer);
        var brt = _btnBackLogs.GetComponent<RectTransform>();
        brt.anchorMin = backAnchorMin;
        brt.anchorMax = backAnchorMax;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        _btnBackLogs.onClick.AddListener(() => SetPanel(PanelId.Main, true));

        LockNavToSelf(_btnBackLogs);

        return panel;
    }

    Slider CrearSliderFilaHorizontal(Transform parent, int layer, string labelText, int slot)
    {
        var row = new GameObject(labelText.Replace(" ", "_") + "_Row");
        row.transform.SetParent(parent, false);
        row.layer = layer;

        var rt = row.AddComponent<RectTransform>();
        float h = sliderRowHeight * uiScale;
        rt.sizeDelta = new Vector2(0, h);

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;

        var rowBG = row.AddComponent<Image>();
        rowBG.color = new Color(0.01f, 0.05f, 0.08f, 0.06f);
        rowBG.raycastTarget = false;

        var label = UI<TextMeshProUGUI>("Label", row.transform, layer);
        label.text = labelText;
        label.font = fontTMP ? fontTMP : label.font;
        label.fontSize = Mathf.RoundToInt(26 * uiScale);
        label.color = new Color(cianTexto.r, cianTexto.g, cianTexto.b, 0.95f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(sliderLabelWidth01, 1f);
        lrt.offsetMin = new Vector2(10 * uiScale, 0);
        lrt.offsetMax = new Vector2(-10 * uiScale, 0);

        var sGO = new GameObject("Slider");
        sGO.transform.SetParent(row.transform, false);
        sGO.layer = layer;

        var srt = sGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(sliderLabelWidth01, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(10 * uiScale, 0);
        srt.offsetMax = new Vector2(-10 * uiScale, 0);

        var hit = sGO.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, 0.001f);
        hit.raycastTarget = true;

        var bar = UI<Image>("BarBG", sGO.transform, layer);
        bar.color = new Color(0.01f, 0.06f, 0.09f, 0.55f);
        bar.raycastTarget = false;
        var barRT = bar.rectTransform;
        barRT.anchorMin = new Vector2(0f, 0.5f);
        barRT.anchorMax = new Vector2(1f, 0.5f);
        barRT.sizeDelta = new Vector2(0, sliderBarHeight * uiScale);

        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sGO.transform, false);
        fillArea.layer = layer;
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0.02f, 0.5f);
        faRT.anchorMax = new Vector2(0.98f, 0.5f);
        faRT.sizeDelta = new Vector2(0, sliderBarHeight * uiScale);

        var fill = UI<Image>("Fill", fillArea.transform, layer);
        fill.color = new Color(cianUI.r, cianUI.g, cianUI.b, 0.35f);
        fill.raycastTarget = false;

        var frt = fill.rectTransform;
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 1f);

        var handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(sGO.transform, false);
        handleArea.layer = layer;
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0.02f, 0.5f);
        haRT.anchorMax = new Vector2(0.98f, 0.5f);
        haRT.sizeDelta = new Vector2(0, sliderBarHeight * uiScale);

        var handle = UI<Image>("Handle", handleArea.transform, layer);
        handle.color = new Color(ambarSelect.r, ambarSelect.g, ambarSelect.b, 0.95f);
        handle.raycastTarget = false;

        var hrt = handle.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0.5f);
        hrt.anchorMax = new Vector2(0f, 0.5f);
        hrt.sizeDelta = new Vector2(sliderHandleWidth * uiScale, (sliderBarHeight * sliderHandleHeightMult) * uiScale);

        var slider = sGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = frt;
        slider.handleRect = hrt;
        slider.targetGraphic = hit;

        var fx = sGO.AddComponent<SliderPulseFX>();
        fx.owner = this;
        fx.slot = slot;
        fx.eventSystem = _es;
        fx.selectable = slider;

        fx.label = label;
        fx.bar = bar;
        fx.fill = fill;
        fx.handle = handle;

        fx.baseText = cianTexto;
        fx.hiText = ambarSelect;

        fx.baseFill = new Color(cianUI.r, cianUI.g, cianUI.b, 0.35f);
        fx.hiFill = new Color(ambarSelect.r, ambarSelect.g, ambarSelect.b, 0.55f);

        fx.baseBar = new Color(0.01f, 0.06f, 0.09f, 0.55f);
        fx.hiBar = new Color(cianUI.r, cianUI.g, cianUI.b, 0.22f);

        fx.pulseAmount = sliderPulseAmount;
        fx.pulseSpeed = sliderPulseSpeed;
        fx.enabledPulse = sliderPulseEnabled;

        return slider;
    }

    internal void NotifyOptionsSelected(Selectable sel, int slot)
    {
        _optionsLast = sel;
        _optionsLastSlot = slot;
    }

    // =========================
    //  AUDIO session helpers
    // =========================
    void EnsureSessionAudioDefaults()
    {
        if (s_audioInit) return;

        s_audioInit = true;
        s_master01 = 1f;
        s_music01 = 1f;
        s_sfx01 = 1f;

        SetMixer01(paramMaster, s_master01, ref s_master01);
        SetMixer01(paramMusic, s_music01, ref s_music01);
        SetMixer01(paramSfx, s_sfx01, ref s_sfx01);
    }

    void AplicarAudioASlidersDesdeSesion()
    {
        if (_sMaster != null) _sMaster.SetValueWithoutNotify(s_master01);
        if (_sMusic != null) _sMusic.SetValueWithoutNotify(s_music01);
        if (_sSfx != null) _sSfx.SetValueWithoutNotify(s_sfx01);

        SetMixer01(paramMaster, s_master01, ref s_master01);
        SetMixer01(paramMusic, s_music01, ref s_music01);
        SetMixer01(paramSfx, s_sfx01, ref s_sfx01);
    }

    void SetMixer01(string param, float value01, ref float cache)
    {
        cache = Mathf.Clamp01(value01);

        if (audioMixer != null && !string.IsNullOrEmpty(param))
        {
            float db = Value01ToDb(cache);
            audioMixer.SetFloat(param, db);
        }
    }

    float Value01ToDb(float v)
    {
        v = Mathf.Clamp01(v);
        if (v <= 0.0001f) return minDb;
        float db = Mathf.Log10(v) * 20f;
        return Mathf.Clamp(db, minDb, 0f);
    }

    // =========================
    //  MENU visibility / panels
    // =========================
    void ApplyMenuVisibleAfterRebuild(bool visible)
    {
        _menuVisible = visible;

        if (_cg)
        {
            _cg.blocksRaycasts = visible;
            _cg.alpha = visible ? 1f : 0f;
            _cg.interactable = visible;
        }

        if (_standalone != null && visible)
        {
            for (int i = 0; i < _otrosModulos.Count; i++)
                _otrosModulos[i].enabled = false;

            _standalone.enabled = true;
            _standalone.inputOverride = _rtInput;

            if (_es != null) _es.SetSelectedGameObject(null);
        }
    }

    void StopUnlockSubmit()
    {
        if (_coUnlockSubmit != null)
        {
            StopCoroutine(_coUnlockSubmit);
            _coUnlockSubmit = null;
        }
    }

    void SetMenuVisible(bool visible)
    {
        _menuVisible = visible;

        if (_cg)
        {
            _cg.blocksRaycasts = visible;
            _cg.alpha = visible ? 1f : 0f;
            _cg.interactable = false;
        }

        if (_standalone != null)
        {
            if (visible)
            {
                for (int i = 0; i < _otrosModulos.Count; i++)
                    _otrosModulos[i].enabled = false;

                _standalone.enabled = true;
                _standalone.inputOverride = _rtInput;

                if (_es != null) _es.SetSelectedGameObject(null);

                StopUnlockSubmit();
                if (Application.isPlaying)
                    _coUnlockSubmit = StartCoroutine(ActivarInteraccionLuegoDeSoltarSubmit());
                else
                {
                    if (_cg) _cg.interactable = true;
                    SetPanel(_panelActual, true);
                }
            }
            else
            {
                StopUnlockSubmit();
                StopLogsTyping();
                StopOptionsCaptureLock();

                _standalone.inputOverride = _prevInputOverride;

                for (int i = 0; i < _otrosModulos.Count; i++)
                    _otrosModulos[i].enabled = _otrosModulosPrevEnabled[i];

                if (_es != null) _es.SetSelectedGameObject(null);
            }
        }

        if (visible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else if (ocultarCursorCuandoNoHayMenu)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    bool IsSubmitHeldSafe()
    {
        bool held = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        try { held |= Input.GetButton("Submit"); } catch { }
        return held;
    }

    IEnumerator ActivarInteraccionLuegoDeSoltarSubmit()
    {
        yield return null;

        if (delayActivarInteraccion > 0f)
            yield return new WaitForSecondsRealtime(delayActivarInteraccion);

        yield return new WaitUntil(() => !IsSubmitHeldSafe());

        if (_cg) _cg.interactable = true;
        SetPanel(_panelActual, true);

        _coUnlockSubmit = null;
    }

    void SetPanel(PanelId id, bool setSelection)
    {
        if (_panelActual == PanelId.Logs && id != PanelId.Logs)
            StopLogsTyping();

        if (_panelActual == PanelId.Options && id != PanelId.Options)
        {
            if (_btnBackOptions != null && _optionsLast == _btnBackOptions && _sMaster != null)
            {
                _optionsLast = _sMaster;
                _optionsLastSlot = 0;
            }

            StopOptionsCaptureLock();
        }

        if (_panelActual == PanelId.Main && id != PanelId.Main)
            ClearMainFxStates();

        if (id == PanelId.Main)
        {
            _mainHoverBlocked = true;
            _mainMousePosWhenBlocked = Input.mousePosition;
            ClearMainFxStates();
            if (_rtInput != null) _rtInput.BlockPointer(0.35f);
        }

        _panelActual = id;

        if (_panelMain != null) _panelMain.SetActive(id == PanelId.Main);
        if (_panelOptions != null) _panelOptions.SetActive(id == PanelId.Options);
        if (_panelLogs != null) _panelLogs.SetActive(id == PanelId.Logs);

        if (_rtInput != null) _rtInput.BlockPointer(0.15f);

        if (id == PanelId.Options)
        {
            AplicarAudioASlidersDesdeSesion();

            _optionsHoverBlocked = true;
            _optionsMousePosWhenBlocked = Input.mousePosition;
            ClearAllOptionsHoverVisuals();

            _lockSelGO = null;
            _lockSelUntil = 0f;

            if (_rtInput != null)
                _rtInput.BlockPointer(lockSeleccionOptionsSegs);
        }

        if (id == PanelId.Logs)
            _logsStartedThisOpen = false;

        if (!setSelection || _es == null) return;

        _es.SetSelectedGameObject(null);

        if (id == PanelId.Main && _btnStart != null)
        {
            _es.SetSelectedGameObject(_btnStart.gameObject);
        }
        else if (id == PanelId.Options && _sMaster != null)
        {
            Selectable startSel = (_optionsLast != null) ? _optionsLast : _sMaster;
            int startSlot = (_optionsLast != null) ? _optionsLastSlot : 0;

            NotifyOptionsSelected(startSel, startSlot);
            _es.SetSelectedGameObject(startSel.gameObject);

            LockOptionsSelectionNow(startSel.gameObject, lockSeleccionOptionsSegs);
        }
        else if (id == PanelId.Logs && _btnBackLogs != null)
        {
            _es.SetSelectedGameObject(_btnBackLogs.gameObject);
            StartLogsTypingIfNeeded();
        }
    }

    void OnStartGame()
    {
        var f = FindObjectOfType<SceneFadeUI>(true);
        if (f != null) f.FadeOutThenLoad("LoadingScene");
        else SceneManager.LoadScene("LoadingScene");
    }

    void OnExit()
    {
    #if UNITY_EDITOR
            Debug.Log("[LaptopMenu] EXIT (en editor no cierra).");
    #else
            Application.Quit();
    #endif
    }

    void SetVerticalNav(Selectable s, Selectable up, Selectable down)
    {
        if (s == null) return;
        var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        s.navigation = nav;
    }

    Button CrearBotonBack(Transform parent, string texto, int layer)
    {
        int h = Mathf.RoundToInt(56 * uiScale);

        var go = new GameObject(texto.Replace(" ", "_") + "_Back");
        go.transform.SetParent(parent, false);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, h);

        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.10f, 0.14f, 0.55f);
        img.raycastTarget = true;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(cianUI.r, cianUI.g, cianUI.b, 0.22f);
        outline.effectDistance = new Vector2(2, -2);

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.layer = layer;

        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(14 * uiScale, 0);
        lrt.offsetMax = new Vector2(-14 * uiScale, 0);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.font = fontTMP ? fontTMP : tmp.font;
        tmp.fontSize = Mathf.RoundToInt(28 * uiScale);
        tmp.color = cianTexto;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var fx = go.AddComponent<SciFiButtonFX>();
        fx.owner = this;
        fx.target = rt;
        fx.fondo = img;
        fx.label = tmp;
        fx.uiCian = cianUI;
        fx.textoCianSuave = cianTexto;
        fx.seleccion = ambarSelect;
        fx.noForzarSeleccionEnHover = false;

        return btn;
    }

    Button CrearBotonSciFi(Transform parent, string texto, int layer, Color colorSeleccion)
    {
        int h = Mathf.RoundToInt(78 * uiScale);

        var go = new GameObject(texto.Replace(" ", "_"));
        go.transform.SetParent(parent, false);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, h);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.10f, 0.14f, 0.75f);
        img.raycastTarget = true;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(cianUI.r, cianUI.g, cianUI.b, 0.18f);
        outline.effectDistance = new Vector2(2, -2);

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var bar = UI<Image>("AccentBar", go.transform, layer);
        bar.color = new Color(cianUI.r, cianUI.g, cianUI.b, 0.45f);
        bar.raycastTarget = false;
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(0, 1);
        brt.sizeDelta = new Vector2(Mathf.RoundToInt(6 * uiScale), 0);
        brt.anchoredPosition = new Vector2(Mathf.RoundToInt(3 * uiScale), 0);

        var under = UI<Image>("Underline", go.transform, layer);
        under.color = new Color(cianUI.r, cianUI.g, cianUI.b, 0.08f);
        under.raycastTarget = false;
        var urt = under.rectTransform;
        urt.anchorMin = new Vector2(0, 0);
        urt.anchorMax = new Vector2(1, 0);
        urt.sizeDelta = new Vector2(0, Mathf.RoundToInt(3 * uiScale));
        urt.anchoredPosition = new Vector2(0, Mathf.RoundToInt(2 * uiScale));

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.layer = layer;

        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(Mathf.RoundToInt(18 * uiScale), Mathf.RoundToInt(10 * uiScale));
        lrt.offsetMax = new Vector2(-Mathf.RoundToInt(48 * uiScale), -Mathf.RoundToInt(10 * uiScale));

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.font = fontTMP ? fontTMP : tmp.font;
        tmp.fontSize = Mathf.RoundToInt(36 * uiScale);
        tmp.color = cianTexto;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        var chevGO = new GameObject("Chevron");
        chevGO.transform.SetParent(go.transform, false);
        chevGO.layer = layer;

        var crt = chevGO.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(1, 0);
        crt.anchorMax = new Vector2(1, 1);
        crt.sizeDelta = new Vector2(Mathf.RoundToInt(42 * uiScale), 0);
        crt.anchoredPosition = new Vector2(-Mathf.RoundToInt(10 * uiScale), 0);

        var chev = chevGO.AddComponent<TextMeshProUGUI>();
        chev.text = ">";
        chev.font = fontTMP ? fontTMP : chev.font;
        chev.fontSize = Mathf.RoundToInt(40 * uiScale);
        chev.color = new Color(cianUI.r, cianUI.g, cianUI.b, 0.35f);
        chev.alignment = TextAlignmentOptions.Center;
        chev.raycastTarget = false;

        var fx = go.AddComponent<SciFiButtonFX>();
        fx.owner = this;
        fx.target = rt;
        fx.fondo = img;
        fx.label = tmp;
        fx.barraAcentoIzq = bar;
        fx.underline = under;
        fx.chevron = chev;
        fx.uiCian = cianUI;
        fx.textoCianSuave = cianTexto;
        fx.seleccion = colorSeleccion;
        fx.noForzarSeleccionEnHover = true;

        return btn;
    }

    T UI<T>(string name, Transform parent, int layer) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.AddComponent<RectTransform>();
        return go.AddComponent<T>();
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
