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
[DefaultExecutionOrder(-500)]
public class LaptopMenuRenderTextureRig : MonoBehaviour
{
    // ✅ Agregamos Rosty como panel extra (sin tocar el resto de panels)
    enum PanelId { Main, Options, Logs, Rosty }

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

    // =========================
    //  ✅ ROSTY (nuevo, en este mismo script)
    // =========================
    [Header("ROSTY - Intro/Test (INIT SESSION)")]
    public bool usarRostyIntro = true;
    public bool rostySoloPrimeraVez = true;

    [Tooltip("Para testear en Editor: si está ON, Rosty SIEMPRE se muestra en Play Mode aunque ya esté marcado como completado.")]
    public bool rostyForceAlwaysInEditor = true;

    public string rostyIntroDoneKey = "HR_intro_done";

    [Header("ROSTY - Idioma (Inspector)")]
    public bool rostyEnglishDefault = false;
    [HideInInspector] public string rostyLangKey = "HR_lang_en"; // legacy (sin toggle ingame)

    [Header("ROSTY - Sprites (emociones)")]
    public Sprite rostyIdle;
    public Sprite rostyTalkClosed;
    [Tooltip("Frame intermedio de boca (opcional). Si está vacío, se usa Closed/Open.")]
    public Sprite rostyTalkMid;
    public Sprite rostyTalkOpen;
    public Sprite rostyLaugh;

    [Header("ROSTY - Sprites (emociones extra, opcional)")]
    public Sprite rostySpeech;
    public Sprite rostyAngry;
    public Sprite rostyDoubt;
    public Sprite rostySad;
    public Sprite rostySurprised;
    public Sprite rostySpeechless;


    [Header("ROSTY - Layout (tuneo en vivo)")]
    // Anchors dentro del panel Rosty (0..1)
    public Vector2 rostyPortraitAnchorMin = new Vector2(0.08f, 0.22f);
    public Vector2 rostyPortraitAnchorMax = new Vector2(0.32f, 0.78f);

    public Vector2 rostyBodyAnchorMin = new Vector2(0.36f, 0.22f);
    public Vector2 rostyBodyAnchorMax = new Vector2(0.92f, 0.78f);

    public Vector2 rostyButtonsAnchorMin = new Vector2(0.36f, 0.10f);
    public Vector2 rostyButtonsAnchorMax = new Vector2(0.92f, 0.20f);

    public int rostyTitleFontSize = 48;
    public int rostyBodyFontSize = 30;
    public int rostyButtonFontSize = 34;
    public float rostyButtonHeight = 78f;
    public float rostyButtonsSpacing = 12f;


    [Header("ROSTY - DogBread Layout (Inspector)")]
    [Tooltip("Escala de fuente SOLO para la pregunta del perro y el pan (0.85 = más chico).")]
    [Range(0.75f, 1.10f)] public float rostyDogBreadFontScale = 0.95f;

    [Tooltip("Override de font size SOLO para DogBread. 0 = usa rostyButtonFontSize.")]
    public int rostyDogBreadButtonFontSizeOverride = 30;

    [Tooltip("Gap entre botones en DogBread (pixeles a uiScale=1).")]
    public float rostyDogBreadGap = 54f;

    [Tooltip("Padding interno extra para medir el ancho (incluye flecha), pixeles a uiScale=1.")]
    public float rostyDogBreadPadA = 150f;
    public float rostyDogBreadPadB = 130f;

    [Tooltip("Margen seguro para no tocar el borde/linea blanca (pixeles a uiScale=1).")]
    public float rostyDogBreadSafeLeft = 70f;
    public float rostyDogBreadSafeRight = 28f;

    [Tooltip("Anchos mínimos (pixeles a uiScale=1).")]
    public float rostyDogBreadMinWidthA = 240f;
    public float rostyDogBreadMinWidthB = 190f;


    [Header("ROSTY - UX")]
    [Tooltip("Si está en false, no hay botón VOLVER: volvés con ESC.")]
    public bool rostyUseBackButton = false;

    [Tooltip("Enter completa el texto actual cuando está tipeando.")]
    public bool rostyEnterCompletesText = true;

    [Tooltip("Bloqueo de submit (rebote) cuando termina el tipeo o aparecen botones.")]
    public float rostySubmitGuardSeconds = 0.10f;

    [HideInInspector] [SerializeField] float secondsBeforeSwitch = 1f; // fallback interno (anti CS0103)

    [Tooltip("Guard extra (seg) después de apretar NEXT/A/B para que el Enter no 'salte' el tipeo del siguiente texto.")]
    public float rostyPostAdvanceGuardSeconds = 0.08f;

    [Header("ROSTY - Spoiler / Borrado")]
    [Tooltip("Segundos para que el jugador lea el spoiler antes de borrarlo.")]
    public float rostySpoilerHoldSeconds = 5.0f;
    [Tooltip("Velocidad de borrado (caracteres por segundo) para el spoiler.")]
    public float rostySpoilerEraseCps = 70f;

    [Tooltip("Velocidad de borrado (cps) para el gag de 'Eso era…' (más bajo = más lento).")]
    public float rostyCapEraseCps = 30f;
    [Tooltip("Pausa antes de borrar el gag 'Eso era…'.")]
    public float rostyCapEraseHoldSeconds = 2.5f;



    [Header("ROSTY - Flow")]
    [Tooltip("Si está ON, al elegir A/B se limpia el texto para que el siguiente mensaje reemplace al anterior (sin scroll).")]
    public bool rostyClearTextAfterChoice = true;

    [Tooltip("Mostrar un botón NEXT entre textos para que el jugador marque el ritmo de lectura.")]
    public bool rostyUseNextBetweenTexts = true;

    [Tooltip("Label del botón NEXT (ES).")]
    public string rostyNextLabelES = "NEXT";
    [Tooltip("Label del botón NEXT (EN).")]
    public string rostyNextLabelEN = "NEXT";

    [Tooltip("Label del botón final para comenzar (ES).")]
    public string rostyBeginLabelES = "COMENZAR";
    [Tooltip("Label del botón final para comenzar (EN).")]
    public string rostyBeginLabelEN = "BEGIN";


    [Header("ROSTY - Boca (más lenta)")]
    [Tooltip("Tiempo mínimo entre flips de boca (seg).")]
    public float rostyMouthMinFlipInterval = 0.12f;
    [Header("ROSTY - Typewriter")]
    public float rostyCharsPerSecond = 46f;
    public float rostyPunctuationExtraDelay = 0.04f;
    public bool rostyAllowSkipWithEnter = true;

    [Header("ROSTY - Boca (al ritmo del tipeo)")]
    public bool rostyMouthFlap = true;
    public int rostyCharsPerMouthFlip = 1;
    public bool rostyIgnoreWhitespaceForMouth = true;

    [Header("ROSTY - Typing SFX (loop mientras escribe)")]
    public bool rostyTypingSfxEnabled = true;
    public AudioClip rostyTypingLoopClip;
    public AudioMixerGroup rostyTypingMixerGroup;
    [Range(0f, 1f)] public float rostyTypingVol = 0.65f;
    public float rostyTypingFadeOut = 0.04f;

    [Header("ROSTY - Flags (PlayerPrefs)")]
    public string rostyDuckBananaKey = "HR_q_duckbanana";
    public string rostyColdKey = "HR_q_cold";
    public string rostyMechaKey = "HR_q_mechas";
    public string rostyCyberGrandmaKey = "HR_q_cybergrandma";
    public string rostyDogBreadKey = "HR_q_dogbread";

    [Header("ROSTY - Safety (bloqueo scripts externos)")]
    [Tooltip("Si está ON, INIT SESSION siempre muestra a Rosty (modo test).")]
    public bool rostyAlwaysShowOnInitSession = false;

    [Tooltip("Si está ON, mientras Rosty está activo se deshabilitan scripts externos que podrían cargar escenas por su cuenta.")]
    public bool rostyDisableExternalStartScripts = true;

    [Tooltip("Nombres de clases (MonoBehaviour/Behaviour) a deshabilitar mientras Rosty está activo. Ej: MainMenuFlow, LaptopMenuSystem.")]
    public string[] rostyDisableTypeNames = new string[] { "MainMenuFlow", "LaptopMenuSystem" };

    [Header("ROSTY - Debug")]
    public bool rostyDebugLogs = false;

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

    // ✅ Rosty panel refs
    GameObject _panelRosty;
    Button _btnBackRosty;
    Button _btnRostyA, _btnRostyB, _btnRostyBegin;
    TextMeshProUGUI _rostyBodyTMP;
    Image _rostyPortrait;
    Toggle _rostyLangToggle;
    TextMeshProUGUI _rostyLangLabel;

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
    //  PUBLIC (para FX)  (SciFiButtonFX / SliderPulseFX lo usan)
    // =========================
    internal bool OptionsHoverBlocked => (_panelActual == PanelId.Options) && _optionsHoverBlocked;
    internal bool MainHoverBlocked => (_panelActual == PanelId.Main) && _mainHoverBlocked;

    // =========================
    //  ROSTY internals
    // =========================
    Coroutine _rostyCo;
    bool _rostyEnglish;
    bool _rostyWaitingChoice;
    int _rostyChoice = -1;
    bool _rostyLastChoiceA; // ✅ evita out/ref en coroutine
    bool _rostyWaitingBegin;

    


    float _rostyChoiceRowBaseY;
    bool _rostyChoiceRowBaseYInit;

        bool _rostyWaitingNext;
    bool _rostyNextPressed;
    enum RostyActionMode { None, Next, Begin }
    RostyActionMode _rostyActionMode = RostyActionMode.None;

    enum RostyEmotion { Speech, Angry, Doubt, Laugh, Sad, Surprised, Speechless }
    RostyEmotion _rostyEmotion = RostyEmotion.Speech;
// ✅ Rosty input guards (anti skip + anti submit rebote)
    float _rostySkipGuardUntil = 0f;
    float _rostySubmitGuardUntil = 0f;
    Coroutine _coRostyInputGuard;
AudioSource _rostyTypingSrc;
    Coroutine _rostyTypingFadeCo;

    // ✅ Rosty: behaviours deshabilitados temporalmente (para evitar que otros scripts carguen LoadingScene)
    readonly List<Behaviour> _rostyDisabledBehaviours = new List<Behaviour>();

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

        // ✅ Rosty
        LoadRostyPrefs();
        EnsureRostyTypingAudio();
    }

    void OnEnable()
    {
        EnsureRT();
        EnsureCamera();
        AplicarRTyST();
        EnsureSessionAudioDefaults();

        // ✅ Typing audio (solo play)
        EnsureLogsTypingAudio();

        // ✅ Rosty
        LoadRostyPrefs();
        EnsureRostyTypingAudio();
    }

    void OnDestroy()
    {
        StopLogsTyping();
        StopUnlockSubmit();
        StopOptionsCaptureLock();
        StopRostyIntro();


        // (FIX) Evitar StartCoroutine en OnDestroy (al salir de escena el GO puede estar inactivo).

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

    // ✅ Llamado externo (por ejemplo desde MainMenuAudio) para iniciar la sesión sin cargar escena directo.
    public void ExternalInitSession()
    {
        OnStartGame();
    }


    void Update()
    {

        // ✅ Debug: F8 fuerza Rosty (opcional)
        if (Application.isPlaying && Input.GetKeyDown(KeyCode.F8) && usarRostyIntro)
        {
            StartRostyIntro();
        }

        // Live rebuild en Play
        if (Application.isPlaying && liveRebuildInPlay && _uiDirty && Time.unscaledTime >= _nextRebuildTime)
        {
            _nextRebuildTime = Time.unscaledTime + rebuildCooldown;
            _uiDirty = false;

            bool wasVisible = _menuVisible;
            PanelId keepPanel = _panelActual;

            // ✅ Si Rosty está escribiendo y reconstruís la UI en vivo, cortamos para evitar MissingReference.
            if (_panelActual == PanelId.Rosty || _rostyCo != null) StopRostyIntro();

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
            else if (_panelRosty != null && _panelRosty.activeSelf) SetPanel(PanelId.Main, true);
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

        // ROSTY: si está esperando botones, forzamos selección segura
        if (_panelActual == PanelId.Rosty)
        {
            // Si estamos esperando BEGIN/Next, mantenemos una selección válida sin clavarla a la fuerza cada frame.
            if (_btnRostyBegin != null && _btnRostyBegin.gameObject.activeSelf)
            {
                var cur = _es.currentSelectedGameObject;

                // Si el foco quedó clavado en algo inválido o inactivo, volver al botón de acción
                if (cur != _btnRostyBegin.gameObject &&
                    (cur == null || !cur.activeInHierarchy || cur == _btnBackRosty?.gameObject))
                {
                    _es.SetSelectedGameObject(_btnRostyBegin.gameObject);
                }

                return;
            }

            if (_rostyWaitingChoice && _btnRostyA != null && _btnRostyB != null)
            {
                var cur = _es.currentSelectedGameObject;
                // Solo corregimos si estás en algo inválido
                if (cur != _btnRostyA.gameObject && cur != _btnRostyB.gameObject)
                    _es.SetSelectedGameObject(_btnRostyA.gameObject);
                return;
            }

            // Si no hay nada seleccionado, caemos en back (si existe)
            if (_btnBackRosty != null && _btnBackRosty.gameObject.activeSelf && _es.currentSelectedGameObject == null)
                _es.SetSelectedGameObject(_btnBackRosty.gameObject);

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

        // ✅ Seguridad: limpiamos listeners por si algún sistema externo engancha onClick y te manda al LoadingScene.
        _btnStart.onClick.RemoveAllListeners();
        _btnOptions.onClick.RemoveAllListeners();
        _btnLogs.onClick.RemoveAllListeners();
        _btnExit.onClick.RemoveAllListeners();

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
        _panelRosty = CrearPanelRosty(canvasGO.transform, layerLaptopUI, sx, sy);

        _panelOptions.SetActive(false);
        _panelLogs.SetActive(false);
        _panelRosty.SetActive(false);
    }

    // ------------------ RESTO DEL SCRIPT ------------------
    // A partir de acá está tu lógica original + agregados de Rosty al final.

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
        title.fontSize = Mathf.RoundToInt(rostyTitleFontSize * uiScale);
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

        // ✅ Esto compila: s_master01/s_music01/s_sfx01 son fields, NO ref params de una lambda.
        _sMaster.onValueChanged.AddListener(v => OnSliderChanged(_sMaster, 0, paramMaster, v, ref s_master01));
        _sMusic.onValueChanged.AddListener(v => OnSliderChanged(_sMusic, 1, paramMusic, v, ref s_music01));
        _sSfx.onValueChanged.AddListener(v => OnSliderChanged(_sSfx, 2, paramSfx, v, ref s_sfx01));

        _btnBackOptions = CrearBotonBack(panel.transform, "BACK", layer);
        var brt = _btnBackOptions.GetComponent<RectTransform>();
        brt.anchorMin = backAnchorMin;
        brt.anchorMax = backAnchorMax;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        _btnBackOptions.onClick.RemoveAllListeners();
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
        title.fontSize = Mathf.RoundToInt(rostyTitleFontSize * uiScale);
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
        _btnBackLogs.onClick.RemoveAllListeners();
        _btnBackLogs.onClick.AddListener(() => SetPanel(PanelId.Main, true));

        LockNavToSelf(_btnBackLogs);

        return panel;
    }

    // =========================
    // ✅ ROSTY PANEL
    // =========================
    GameObject CrearPanelRosty(Transform parent, int layer, float sx, float sy)
    {
        var panel = new GameObject("Panel_Rosty");
        panel.transform.SetParent(parent, false);
        panel.layer = layer;

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(sx + overlayInsetMin.x, sy + overlayInsetMin.y);
        rt.anchorMax = new Vector2(1f - sx - overlayInsetMax.x, 1f - sy - overlayInsetMax.y);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = fondoPanel;
        panelImg.raycastTarget = false;

        var title = UI<TextMeshProUGUI>("Title", panel.transform, layer);
        title.text = "R04-STY // INIT";
        title.font = fontTMP ? fontTMP : title.font;
        title.fontSize = Mathf.RoundToInt(rostyTitleFontSize * uiScale);
        title.color = cianUI;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.raycastTarget = false;
        title.rectTransform.anchorMin = new Vector2(0.08f, 0.80f);
        title.rectTransform.anchorMax = new Vector2(0.78f, 0.95f);
        // (sin toggle ES/EN ingame)

        // Portrait
        _rostyPortrait = UI<Image>("Portrait", panel.transform, layer);
        _rostyPortrait.preserveAspect = true;
        _rostyPortrait.raycastTarget = false;
        _rostyPortrait.rectTransform.anchorMin = rostyPortraitAnchorMin;
        _rostyPortrait.rectTransform.anchorMax = rostyPortraitAnchorMax;
        _rostyPortrait.rectTransform.offsetMin = Vector2.zero;
        _rostyPortrait.rectTransform.offsetMax = Vector2.zero;
        if (_coRostyInputGuard != null)
        {
            StopCoroutine(_coRostyInputGuard);
            _coRostyInputGuard = null;
        }

        SetRostyFaceIdle();

        // Body
        _rostyBodyTMP = UI<TextMeshProUGUI>("Body", panel.transform, layer);
        _rostyBodyTMP.text = "";
        _rostyBodyTMP.font = fontTMP ? fontTMP : _rostyBodyTMP.font;
        _rostyBodyTMP.fontSize = Mathf.RoundToInt(rostyBodyFontSize * uiScale);
        _rostyBodyTMP.color = cianTexto;
        _rostyBodyTMP.alignment = TextAlignmentOptions.TopLeft;
        _rostyBodyTMP.enableWordWrapping = true;
        _rostyBodyTMP.raycastTarget = false;
        _rostyBodyTMP.rectTransform.anchorMin = rostyBodyAnchorMin;
        _rostyBodyTMP.rectTransform.anchorMax = rostyBodyAnchorMax;
        _rostyBodyTMP.rectTransform.offsetMin = Vector2.zero;
        _rostyBodyTMP.rectTransform.offsetMax = Vector2.zero;

        // Buttons root: YES/NO arriba + NEXT centrado abajo
        var buttonsRoot = new GameObject("ButtonsRoot");
        buttonsRoot.transform.SetParent(panel.transform, false);
        buttonsRoot.layer = layer;

        var brtRoot = buttonsRoot.AddComponent<RectTransform>();
        brtRoot.anchorMin = rostyButtonsAnchorMin;
        brtRoot.anchorMax = rostyButtonsAnchorMax;
        brtRoot.offsetMin = Vector2.zero;
        brtRoot.offsetMax = Vector2.zero;

        // Row YES/NO (arriba)
        var rowChoices = new GameObject("ChoicesRow");
        rowChoices.transform.SetParent(buttonsRoot.transform, false);
        rowChoices.layer = layer;

        var crt = rowChoices.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0.50f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = new Vector2(0, -Mathf.RoundToInt(rostyButtonsSpacing * 0.5f * uiScale));

        var hlgChoices = rowChoices.AddComponent<HorizontalLayoutGroup>();
        hlgChoices.childAlignment = TextAnchor.MiddleCenter;
        hlgChoices.childControlWidth = true;
        hlgChoices.childControlHeight = true;
        hlgChoices.childForceExpandWidth = false;  // ✅ NO estira: queda más compacto y centrado
        hlgChoices.childForceExpandHeight = true;
        hlgChoices.spacing = Mathf.RoundToInt(rostyButtonsSpacing * uiScale);

        // Row NEXT (abajo, centrado)
        var rowNext = new GameObject("NextRow");
        rowNext.transform.SetParent(buttonsRoot.transform, false);
        rowNext.layer = layer;

        var nrt = rowNext.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0.50f);
        nrt.offsetMin = new Vector2(0, Mathf.RoundToInt(rostyButtonsSpacing * 0.5f * uiScale));
        nrt.offsetMax = Vector2.zero;

        var hlgNext = rowNext.AddComponent<HorizontalLayoutGroup>();
        hlgNext.childAlignment = TextAnchor.MiddleCenter;
        hlgNext.childControlWidth = true;
        hlgNext.childControlHeight = true;
        hlgNext.childForceExpandWidth = false;     // ✅ no estira: queda centrado
        hlgNext.childForceExpandHeight = true;

        _btnRostyA = CrearBotonSciFi(rowChoices.transform, "A", layer, ambarSelect);
        _btnRostyB = CrearBotonSciFi(rowChoices.transform, "B", layer, ambarSelect);
        _btnRostyBegin = CrearBotonSciFi(rowNext.transform, "NEXT", layer, ambarSelect);

        ApplyRostyChoiceButtonLayout(_btnRostyA);
        ApplyRostyChoiceButtonLayout(_btnRostyB);
        ApplyRostyNextButtonLayout(_btnRostyBegin);

        _btnRostyA.onClick.RemoveAllListeners();
        _btnRostyA.onClick.AddListener(() => RostyChoose(0));
        _btnRostyB.onClick.RemoveAllListeners();
        _btnRostyB.onClick.AddListener(() => RostyChoose(1));
        _btnRostyBegin.onClick.RemoveAllListeners();
        _btnRostyBegin.onClick.AddListener(RostyActionButtonPressed);

        _btnRostyA.gameObject.SetActive(false);
        _btnRostyB.gameObject.SetActive(false);

        // ✅ Guard post-elección: evita que el Enter de elegir A/B saltee el siguiente tipeo
        RostyStartInputGuard(Mathf.Max(0.02f, rostyPostAdvanceGuardSeconds));
        _btnRostyBegin.gameObject.SetActive(false);

        _btnBackRosty = CrearBotonBack(panel.transform, RL("VOLVER", "BACK"), layer);
        var brt = _btnBackRosty.GetComponent<RectTransform>();
        brt.anchorMin = backAnchorMin;
        brt.anchorMax = backAnchorMax;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        _btnBackRosty.onClick.RemoveAllListeners();
        _btnBackRosty.onClick.AddListener(() => SetPanel(PanelId.Main, true));

        if (!rostyUseBackButton)
            _btnBackRosty.gameObject.SetActive(false);


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

        // ✅ ROSTY lock: si Rosty está activo, ignoramos pedidos externos de ocultar el menú.
        // Esto evita que otros sistemas (FocusDetector / Flow) te apaguen el canvas justo al tocar INIT SESSION.
        if (!visible && (_panelActual == PanelId.Rosty || _rostyCo != null || _rostyWaitingChoice || _rostyWaitingBegin))
        {
            if (rostyDebugLogs) Debug.LogWarning("[Rosty] Se intentó ocultar el menú mientras Rosty está activo. Ignorado.");
            return;
        }

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
                StopRostyIntro();

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
        // salir de logs
        if (_panelActual == PanelId.Logs && id != PanelId.Logs)
            StopLogsTyping();

        // salir de rosty
        if (_panelActual == PanelId.Rosty && id != PanelId.Rosty)
            StopRostyIntro();

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
        if (_panelRosty != null) _panelRosty.SetActive(id == PanelId.Rosty);

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
        else if (id == PanelId.Rosty)
        {
            if (_rostyWaitingBegin && _btnRostyBegin != null) _es.SetSelectedGameObject(_btnRostyBegin.gameObject);
            else if (_rostyWaitingChoice && _btnRostyA != null) _es.SetSelectedGameObject(_btnRostyA.gameObject);
            else if (_btnBackRosty != null) _es.SetSelectedGameObject(_btnBackRosty.gameObject);
        }
    }

    // =========================
    // ✅ INIT SESSION: Rosty primero, luego LoadingScene
    // =========================
    void OnStartGame()
    {
        // Si no estás en Play Mode, NO cargamos escenas.
        if (!Application.isPlaying)
        {
            if (rostyDebugLogs) Debug.Log("[LaptopMenu] INIT SESSION ignorado (no está en Play Mode).");
            return;
        }

        bool forceInEditor = (rostyForceAlwaysInEditor && Application.isEditor);
        bool introDone = PlayerPrefs.GetInt(rostyIntroDoneKey, 0) == 1;

        // ✅ Modo test: si rostyAlwaysShowOnInitSession está ON, Rosty se muestra SIEMPRE cuando tocás INIT SESSION.
        // Si está OFF, respeta la lógica de "solo primera vez" + PlayerPrefs.
        bool shouldShowRosty = usarRostyIntro;

        if (rostyDebugLogs)
            Debug.LogWarning($"[LaptopMenu] INIT SESSION -> showRosty={shouldShowRosty} forceInEditor={forceInEditor} introDone={introDone}");

        if (shouldShowRosty)
        {
            RostyDisableExternalScripts();
            StartRostyIntro();
            return;
        }

        LoadGameNow();
    }

    void LoadGameNow()
    {
        EnsureGlobalRunStateForGameplay();
        // ✅ Fade seguro: SceneFadeUI puede estar en un GO inactivo (FadeBlack)
        var f = FindObjectOfType<SceneFadeUI>(true);

        if (f != null)
        {
            // Intentar activarlo si está apagado
            if (!f.gameObject.activeInHierarchy)
                f.gameObject.SetActive(true);

            if (f.isActiveAndEnabled && f.gameObject.activeInHierarchy)
            {
                f.FadeOutThenLoad("LoadingScene");
                return;
            }
        }

        // Fallback
        SceneManager.LoadScene("LoadingScene");
    }

    // (FIX) Asegura que el cambio de escena no herede estados globales del menú (freeze / sin input).
    void EnsureGlobalRunStateForGameplay()
    {
        // TimeScale / physics
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Audio
        AudioListener.pause = false;

        // Cursor (gameplay)
        if (ocultarCursorCuandoNoHayMenu)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }


    void OnExit()
    {
#if UNITY_EDITOR
        Debug.Log("[LaptopMenu] EXIT (en editor no cierra).");
#else
        Application.Quit();
#endif
    }

    // =========================
    //  Helpers UI
    // =========================
    void SetVerticalNav(Selectable s, Selectable up, Selectable down)
    {
        if (s == null) return;
        var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        s.navigation = nav;
    }


    void SetHorizontalNav(Selectable a, Selectable b)
    {
        if (a == null || b == null) return;

        var navA = new Navigation { mode = Navigation.Mode.Explicit };
        navA.selectOnLeft = b;
        navA.selectOnRight = b;
        navA.selectOnUp = a;
        navA.selectOnDown = a;
        a.navigation = navA;

        var navB = new Navigation { mode = Navigation.Mode.Explicit };
        navB.selectOnLeft = a;
        navB.selectOnRight = a;
        navB.selectOnUp = b;
        navB.selectOnDown = b;
        b.navigation = navB;
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

    // =====================================================================================
    //  ✅ ROSTY - LOGICA DE TIPEO (SOLIDA) + PREGUNTAS
    // =====================================================================================

    void LoadRostyPrefs()
    {
        // ✅ Sin toggle ES/EN ingame: solo Inspector
        _rostyEnglish = rostyEnglishDefault;
    }

    void SaveRostyPrefs()
    {
        // legacy (sin toggle ingame)
    }

    string RL(string es, string en)
    {
        if (_rostyEnglish && !string.IsNullOrEmpty(en)) return en;
        return es;
    }

    void EnsureRostyTypingAudio()
    {
        if (!Application.isPlaying) return;
        if (_rostyTypingSrc != null) return;

        Transform t = transform.Find("RostyTypingAudio");
        if (t == null)
        {
            var go = new GameObject("RostyTypingAudio");
            go.transform.SetParent(transform, false);
            t = go.transform;
        }

        _rostyTypingSrc = t.GetComponent<AudioSource>();
        if (_rostyTypingSrc == null) _rostyTypingSrc = t.gameObject.AddComponent<AudioSource>();

        _rostyTypingSrc.playOnAwake = false;
        _rostyTypingSrc.loop = true;
        _rostyTypingSrc.spatialBlend = 0f;
        _rostyTypingSrc.volume = Mathf.Clamp01(rostyTypingVol);

        if (rostyTypingMixerGroup != null)
            _rostyTypingSrc.outputAudioMixerGroup = rostyTypingMixerGroup;
    }

    void RostyTypingSFX_Start()
    {
        if (!Application.isPlaying) return;
        if (!rostyTypingSfxEnabled) return;
        if (rostyTypingLoopClip == null) return;

        EnsureRostyTypingAudio();

        if (_rostyTypingFadeCo != null)
        {
            StopCoroutine(_rostyTypingFadeCo);
            _rostyTypingFadeCo = null;
        }

        if (_rostyTypingSrc.clip != rostyTypingLoopClip)
            _rostyTypingSrc.clip = rostyTypingLoopClip;

        _rostyTypingSrc.volume = Mathf.Clamp01(rostyTypingVol);

        if (!_rostyTypingSrc.isPlaying)
            _rostyTypingSrc.Play();
    }

    void RostyTypingSFX_Stop(bool instant = false)
    {
        if (_rostyTypingSrc == null) return;

        if (instant || !Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy || rostyTypingFadeOut <= 0.001f)
        {
            if (_rostyTypingFadeCo != null)
            {
                StopCoroutine(_rostyTypingFadeCo);
                _rostyTypingFadeCo = null;
            }

            if (_rostyTypingSrc.isPlaying) _rostyTypingSrc.Stop();
            _rostyTypingSrc.volume = Mathf.Clamp01(rostyTypingVol);
            return;
        }

        if (_rostyTypingFadeCo != null)
        {
            StopCoroutine(_rostyTypingFadeCo);
            _rostyTypingFadeCo = null;
        }

        _rostyTypingFadeCo = StartCoroutine(Co_FadeOutStopRostyTyping(rostyTypingFadeOut));
    }

    IEnumerator Co_FadeOutStopRostyTyping(float dur)
    {
        if (_rostyTypingSrc == null) yield break;

        float startVol = _rostyTypingSrc.volume;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            _rostyTypingSrc.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        if (_rostyTypingSrc.isPlaying) _rostyTypingSrc.Stop();
        _rostyTypingSrc.volume = Mathf.Clamp01(rostyTypingVol);
        _rostyTypingFadeCo = null;
    }

    // =========================
    //  ✅ ROSTY - Bloqueo de scripts externos que cargan escena
    // =========================
    void RostyDisableExternalScripts()
    {
        if (!rostyDisableExternalStartScripts) return;
        if (!Application.isPlaying) return;

        _rostyDisabledBehaviours.Clear();

        if (rostyDisableTypeNames == null || rostyDisableTypeNames.Length == 0) return;

        // Buscamos Behaviours (MonoBehaviours incluidos) y deshabilitamos los que matchean por nombre de clase.
        var all = FindObjectsOfType<Behaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var b = all[i];
            if (b == null) continue;
            if (b == this) continue;

            string typeName = b.GetType().Name;

            bool match = false;
            for (int j = 0; j < rostyDisableTypeNames.Length; j++)
            {
                string wanted = rostyDisableTypeNames[j];
                if (string.IsNullOrEmpty(wanted)) continue;
                if (typeName == wanted) { match = true; break; }
            }

            if (!match) continue;

            if (b.enabled)
            {
                b.enabled = false;
                _rostyDisabledBehaviours.Add(b);
            }
        }

        if (rostyDebugLogs && _rostyDisabledBehaviours.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append("[Rosty] Deshabilitados temporales: ").Append(_rostyDisabledBehaviours.Count).Append(" => ");
            for (int i = 0; i < _rostyDisabledBehaviours.Count; i++)
            {
                var b = _rostyDisabledBehaviours[i];
                if (b == null) continue;
                sb.Append(b.GetType().Name);
                if (i < _rostyDisabledBehaviours.Count - 1) sb.Append(", ");
            }
            Debug.LogWarning(sb.ToString());
        }
    }

    void RostyRestoreExternalScripts()
    {
        if (_rostyDisabledBehaviours.Count == 0) return;

        for (int i = 0; i < _rostyDisabledBehaviours.Count; i++)
        {
            var b = _rostyDisabledBehaviours[i];
            if (b != null) b.enabled = true;
        }

        _rostyDisabledBehaviours.Clear();
    }

    void StartRostyIntro()
    {
        if (!Application.isPlaying) return;
        if (_panelRosty == null) return;

        
        StopRostyIntro();

        _rostyWaitingNext = false;
        _rostyNextPressed = false;
        _rostyActionMode = RostyActionMode.None;


        RostyDisableExternalScripts();

        SetPanel(PanelId.Rosty, true);

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";
        RostyHideActionButtons();
        RostySetEmotion(RostyEmotion.Speech);

        // ✅ Guard: evita que el Enter de INIT SESSION skipee/active botones
        RostyStartInputGuard(Mathf.Max(0.10f, rostySubmitGuardSeconds));

        if (_btnRostyA != null) _btnRostyA.gameObject.SetActive(false);
        if (_btnRostyB != null) _btnRostyB.gameObject.SetActive(false);
        if (_btnRostyBegin != null) _btnRostyBegin.gameObject.SetActive(false);

        _rostyCo = StartCoroutine(Co_RostyIntro_Safe());
    }

    void StopRostyIntro()
    {
        if (_rostyCo != null)
        {
            StopCoroutine(_rostyCo);
            _rostyCo = null;
        }

        _rostyWaitingChoice = false;
        _rostyWaitingBegin = false;
        _rostyWaitingNext = false;
        _rostyNextPressed = false;
        _rostyActionMode = RostyActionMode.None;
        if (_btnRostyBegin != null) _btnRostyBegin.gameObject.SetActive(false);

        _rostyChoice = -1;

        SetRostyFaceIdle();
        RostyTypingSFX_Stop(true);

        // ✅ restaurar scripts externos (si se deshabilitaron al iniciar Rosty)
        RostyRestoreExternalScripts();
    }

    void RostyChoose(int c)
    {
        if (!_rostyWaitingChoice) return;
        if (RostySubmitBlocked()) return; // ✅ evita click automático por rebote
        _rostyChoice = c;
    }

    void RostyBegin()
    {
        if (!_rostyWaitingBegin) return;
        if (RostySubmitBlocked()) return; // ✅ evita auto-begin por rebote

        _rostyWaitingBegin = false;

        PlayerPrefs.SetInt(rostyIntroDoneKey, 1);
        PlayerPrefs.Save();

        LoadGameNow();
    }

    void RostyActionButtonPressed()
    {
        // Este botón se usa como NEXT durante el diálogo y como BEGIN al final.
        if (RostySubmitBlocked()) return;

        if (_rostyActionMode == RostyActionMode.Next)
        {
            if (!_rostyWaitingNext) return;
            _rostyNextPressed = true;
        }
        else if (_rostyActionMode == RostyActionMode.Begin)
        {
            // Reutilizamos tu Begin real
            RostyBegin();
        }
    }


    // ✅ Rosty: fuerza "hover/focus" visual del botón de acción (NEXT/COMENZAR)
    void RostyForceActionButtonFocus()
    {
        if (_btnRostyBegin == null) return;
        var go = _btnRostyBegin.gameObject;
        if (go == null || !go.activeInHierarchy) return;

        if (_es != null)
        {
            // Selección real (teclado) + eventos de select/hover para que el FX se vea
            _es.SetSelectedGameObject(go);

            var bed = new BaseEventData(_es);
            ExecuteEvents.Execute<ISelectHandler>(go, bed, ExecuteEvents.selectHandler);

            var ped = new PointerEventData(_es);
            ExecuteEvents.Execute<IPointerEnterHandler>(go, ped, ExecuteEvents.pointerEnterHandler);
        }

        // Por las dudas (Selectable interno)
        _btnRostyBegin.Select();
    }

    void RostyClearAllButtonHoverFX()
    {
        if (_es == null) return;

        var ped = new PointerEventData(_es);

        if (_btnRostyA != null) ExecuteEvents.Execute<IPointerExitHandler>(_btnRostyA.gameObject, ped, ExecuteEvents.pointerExitHandler);
        if (_btnRostyB != null) ExecuteEvents.Execute<IPointerExitHandler>(_btnRostyB.gameObject, ped, ExecuteEvents.pointerExitHandler);
        if (_btnRostyBegin != null) ExecuteEvents.Execute<IPointerExitHandler>(_btnRostyBegin.gameObject, ped, ExecuteEvents.pointerExitHandler);

        // Si quedó seleccionado algo inactivo, limpiarlo
        if (_es.currentSelectedGameObject != null && !_es.currentSelectedGameObject.activeInHierarchy)
            _es.SetSelectedGameObject(null);
    }

    IEnumerator RostyWaitNext(bool keepExpression = false)
    {
        if (!rostyUseNextBetweenTexts) yield break;

        if (_btnRostyBegin == null) yield break;

        _rostyActionMode = RostyActionMode.Next;
        _rostyWaitingNext = true;
        _rostyNextPressed = false;

        SetButtonLabel(_btnRostyBegin, RL(rostyNextLabelES, rostyNextLabelEN));
        _btnRostyBegin.gameObject.SetActive(true);

        // ✅ Forzar focus/hover visual (como en el screenshot "mechas")
        RostyForceActionButtonFocus();

        // Guard anti-rebote (el Enter que completó el tipeo no debería activar NEXT al instante)
        RostyStartInputGuard(rostySubmitGuardSeconds);

        while (!_rostyNextPressed)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible) yield break;
            yield return null;
        }

        _rostyWaitingNext = false;
        _rostyNextPressed = false;
        _rostyActionMode = RostyActionMode.None;

        _btnRostyBegin.gameObject.SetActive(false);

        // ✅ Guard post-NEXT: evita que el Enter que activó NEXT saltee el siguiente tipeo
        RostyStartInputGuard(Mathf.Max(0.02f, rostyPostAdvanceGuardSeconds));


        if (!keepExpression) SetRostyFaceIdle();
    }

    

    IEnumerator RostyWaitNextTimedEmotion(float secondsBeforeSwitch, RostyEmotion afterEmotion)
    {
        if (!rostyUseNextBetweenTexts) yield break;
        if (_btnRostyBegin == null) yield break;

        _rostyActionMode = RostyActionMode.Next;
        _rostyWaitingNext = true;
        _rostyNextPressed = false;

        SetButtonLabel(_btnRostyBegin, RL(rostyNextLabelES, rostyNextLabelEN));
        _btnRostyBegin.gameObject.SetActive(true);

        // ✅ Forzar focus/hover visual
        RostyForceActionButtonFocus();

        RostyStartInputGuard(rostySubmitGuardSeconds);

        float t0 = Time.unscaledTime;
        bool switched = false;

        while (!_rostyNextPressed)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible) yield break;

            if (!switched && secondsBeforeSwitch > 0f && (Time.unscaledTime - t0) >= secondsBeforeSwitch)
            {
                switched = true;
                RostySetEmotion(afterEmotion);
            }

            yield return null;
        }

        _rostyWaitingNext = false;
        _rostyNextPressed = false;
        _rostyActionMode = RostyActionMode.None;

        _btnRostyBegin.gameObject.SetActive(false);

        RostyStartInputGuard(Mathf.Max(0.02f, rostyPostAdvanceGuardSeconds));

        // keepExpression: siempre true acá
    }

    IEnumerator RostyShowTextAndNext_EmotionSwitch(string line, RostyEmotion firstEmotion, float secondsThenSwitch, RostyEmotion secondEmotion)
    {
        if (_rostyBodyTMP == null) yield break;

// ✅ Reset estilo texto (por si venimos de una pregunta larga con autosize/márgenes)
int _baseSize = Mathf.RoundToInt(rostyBodyFontSize * uiScale);
_rostyBodyTMP.enableAutoSizing = false;
_rostyBodyTMP.fontSize = _baseSize;
_rostyBodyTMP.fontSizeMax = _baseSize;
_rostyBodyTMP.fontSizeMin = _baseSize;
_rostyBodyTMP.margin = Vector4.zero;

        RostyHideActionButtons();
        _rostyBodyTMP.text = "";

        // tipeo normal
        RostySetEmotion(RostyEmotion.Speech);
        yield return RostyTypeAppend(line);

        // al final: emoción 1
        RostySetEmotion(firstEmotion);

        // esperar NEXT mientras a los X segundos cambia a emoción 2
        yield return RostyWaitNextTimedEmotion(secondsThenSwitch, secondEmotion);
    }

IEnumerator RostyWaitBegin(bool keepExpression = false)
    {
        if (_btnRostyBegin == null) yield break;

        _rostyActionMode = RostyActionMode.Begin;
        _rostyWaitingBegin = true;

        SetButtonLabel(_btnRostyBegin, RL(rostyBeginLabelES, rostyBeginLabelEN));
        _btnRostyBegin.gameObject.SetActive(true);

        // ✅ Forzar focus/hover visual
        RostyForceActionButtonFocus();

        RostyStartInputGuard(rostySubmitGuardSeconds);

        while (_rostyWaitingBegin)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible) yield break;
            yield return null;
        }

        _rostyActionMode = RostyActionMode.None;
        _btnRostyBegin.gameObject.SetActive(false);

        if (!keepExpression) SetRostyFaceIdle();
    }

    IEnumerator RostyShowTextAndNext(string line, RostyEmotion endEmotion = RostyEmotion.Speech, bool laughAtEnd = false)
    {
        if (_rostyBodyTMP == null) yield break;

// ✅ Reset estilo texto (por si venimos de una pregunta larga con autosize/márgenes)
int _baseSize = Mathf.RoundToInt(rostyBodyFontSize * uiScale);
_rostyBodyTMP.enableAutoSizing = false;
_rostyBodyTMP.fontSize = _baseSize;
_rostyBodyTMP.fontSizeMax = _baseSize;
_rostyBodyTMP.fontSizeMin = _baseSize;
_rostyBodyTMP.margin = Vector4.zero;

        RostyHideActionButtons();
        _rostyBodyTMP.text = "";

        // ✅ Durante el tipeo: modo "speech" para permitir mouth flap normal
        RostySetEmotion(RostyEmotion.Speech);

        yield return RostyTypeAppend(line);

        // ✅ Al final: aplicar emoción pedida (se mantiene hasta NEXT)
        RostyEmotion final = laughAtEnd ? RostyEmotion.Laugh : endEmotion;
        if (final != RostyEmotion.Speech)
            RostySetEmotion(final);
        else
            RostySetEmotion(RostyEmotion.Speech);

        bool keep = (final != RostyEmotion.Speech);
        yield return RostyWaitNext(keepExpression: keep);
    }



    
    void RostySetEmotion(RostyEmotion emotion)
    {
        _rostyEmotion = emotion;
        ApplyRostyEmotion();
    }

    void ApplyRostyEmotion()
    {
        if (_rostyPortrait == null) return;

        // Prioridad: sprite específico si existe, si no cae a Idle.
        Sprite s = null;
        switch (_rostyEmotion)
        {
            case RostyEmotion.Angry: s = rostyAngry; break;
            case RostyEmotion.Doubt: s = rostyDoubt; break;
            case RostyEmotion.Sad: s = rostySad; break;
            case RostyEmotion.Surprised: s = rostySurprised; break;
            case RostyEmotion.Speechless: s = rostySpeechless; break;
            case RostyEmotion.Laugh: s = rostyLaugh; break;
            case RostyEmotion.Speech: s = rostySpeech; break;
        }

        if (s != null) _rostyPortrait.sprite = s;
        else SetRostyFaceIdle();
    }

    void SetRostyFaceIdle()
    {
        // ✅ IMPORTANTE: volver a estado neutral también a nivel lógica (evita que una emoción se "pegue" al siguiente texto)
        _rostyEmotion = RostyEmotion.Speech;

        if (_rostyPortrait == null) return;

        // Si hay sprite "Speech", úsalo como idle por defecto.
        if (rostySpeech != null) { _rostyPortrait.sprite = rostySpeech; return; }

        if (rostyIdle != null) _rostyPortrait.sprite = rostyIdle;
        else if (rostyTalkClosed != null) _rostyPortrait.sprite = rostyTalkClosed;
    }

    void SetRostyFaceTalkPhase(int phase)
    {
        if (_rostyPortrait == null) return;

        // phase: 0=closed, 1=mid, 2=open
        Sprite closed = rostyTalkClosed;
        Sprite mid = rostyTalkMid != null ? rostyTalkMid : null;
        Sprite open = rostyTalkOpen;

        if (open == null || closed == null)
        {
            ApplyRostyEmotion();
            return;
        }

        if (phase == 1 && mid != null) _rostyPortrait.sprite = mid;
        else _rostyPortrait.sprite = (phase >= 2) ? open : closed;
    }

    // compat: llamado viejo (open/closed)
    void SetRostyFaceTalk(bool open)
    {
        SetRostyFaceTalkPhase(open ? 2 : 0);
    }

    void SetRostyFaceLaugh()
    {
        if (_rostyPortrait == null) return;
        if (rostyLaugh != null) _rostyPortrait.sprite = rostyLaugh;
        else SetRostyFaceIdle();
    }


    void RostyHideActionButtons()
    {
        // Limpia el FX hover/selected de botones viejos (evita estados pegados)
        RostyClearAllButtonHoverFX();

        if (_btnRostyA != null) _btnRostyA.gameObject.SetActive(false);
        if (_btnRostyB != null) _btnRostyB.gameObject.SetActive(false);
        if (_btnRostyBegin != null) _btnRostyBegin.gameObject.SetActive(false);
    }

bool RostySkipPressed()
    {
        if (!rostyAllowSkipWithEnter) return false;
        if (!rostyEnterCompletesText) return false;

        // ✅ no permitir skip en el primer instante (Enter de INIT SESSION)
        if (Time.unscaledTime < _rostySkipGuardUntil) return false;

        // ✅ usar KeyDown (siempre funciona para "completar texto")
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    IEnumerator RostyTypeAppend(string text)
    {
        if (_rostyBodyTMP == null) yield break;
        if (string.IsNullOrEmpty(text)) yield break;

        float cps = Mathf.Max(1f, rostyCharsPerSecond);
        float secPerChar = 1f / cps;

        int[] mouthSeq = new int[] { 0, 1, 2, 1 };
        int mouthSeqIdx = 1; // arranca en talkMid
        int mouthFlipCounter = 0;
        float lastMouthFlipT = 0f;

        // ✅ mientras tipea: nada de botones
        RostyHideActionButtons();

        // ✅ si la línea es 'speechless', no flap
        bool allowFlap = rostyMouthFlap && (_rostyEmotion == RostyEmotion.Speech);

        // arrancar con talkMid si existe (solo en speech). Si hay emoción, respetarla.
        if (allowFlap) SetRostyFaceTalkPhase(mouthSeq[mouthSeqIdx]);
        else ApplyRostyEmotion();

        RostyTypingSFX_Start();

        string baseText = _rostyBodyTMP.text ?? "";

        for (int i = 0; i < text.Length; i++)
        {
            // ✅ guards: si saliste del panel o se reconstruyó la UI, cortamos
            if (_panelActual != PanelId.Rosty || !_menuVisible || _rostyBodyTMP == null) break;

            if (RostySkipPressed())
            {
                _rostyBodyTMP.text = baseText + text;
                break;
            }

            char ch = text[i];
            _rostyBodyTMP.text = baseText + text.Substring(0, i + 1);

            if (allowFlap)
            {
                bool isWs = char.IsWhiteSpace(ch);
                if (!rostyIgnoreWhitespaceForMouth || !isWs)
                {
                    mouthFlipCounter++;
                    if (mouthFlipCounter >= Mathf.Max(1, rostyCharsPerMouthFlip))
                    {
                        mouthFlipCounter = 0;
                        if (Time.unscaledTime - lastMouthFlipT >= Mathf.Max(0f, rostyMouthMinFlipInterval))
                        {
                            lastMouthFlipT = Time.unscaledTime;
                            mouthSeqIdx = (mouthSeqIdx + 1) % mouthSeq.Length;
                            SetRostyFaceTalkPhase(mouthSeq[mouthSeqIdx]);
                        }
                    }
                }
            }

            if (ch == '.' || ch == ',' || ch == '!' || ch == '?' || ch == ':')
            {
                float extra = Mathf.Max(0f, rostyPunctuationExtraDelay);
                if (extra > 0f) yield return new WaitForSecondsRealtime(extra);
            }

            yield return new WaitForSecondsRealtime(secPerChar);
        }

        RostyTypingSFX_Stop(false);
        ApplyRostyEmotion();
    }

    IEnumerator RostySayLine(string line, bool blankLineBefore = true)
    {
        if (_rostyBodyTMP == null) yield break;

        if (!string.IsNullOrEmpty(_rostyBodyTMP.text))
            _rostyBodyTMP.text += blankLineBefore ? "\n\n" : "\n";

        yield return RostyTypeAppend(line);
    }

    IEnumerator RostyEraseChars(int count, float cpsErase = 170f)
    {
        if (_rostyBodyTMP == null) yield break;

        string t = _rostyBodyTMP.text ?? "";
        count = Mathf.Clamp(count, 0, t.Length);

        float cps = Mathf.Max(1f, cpsErase);
        float sec = 1f / cps;

        RostyTypingSFX_Start();

        for (int i = 0; i < count; i++)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible || _rostyBodyTMP == null) break;
            if (RostySkipPressed()) break;

            string cur = _rostyBodyTMP.text ?? "";
            if (cur.Length <= 0) break;

            _rostyBodyTMP.text = cur.Substring(0, cur.Length - 1);
            yield return new WaitForSecondsRealtime(sec);
        }

        RostyTypingSFX_Stop(false);
        ApplyRostyEmotion();
    }

    IEnumerator RostyTypeThenWaitEraseReplaceNewParagraph(string first, float waitSeconds, string replace, bool laughOnReplace)
    {
        if (_rostyBodyTMP == null) yield break;

        if (!string.IsNullOrEmpty(_rostyBodyTMP.text))
            _rostyBodyTMP.text += "\n\n";

        yield return RostyTypeAppend(first);

        if (waitSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitSeconds);

        yield return RostyEraseChars(first.Length, 220f);

        if (laughOnReplace) SetRostyFaceLaugh();
        yield return RostyTypeAppend(replace);
        SetRostyFaceIdle();
    }

    
    void ApplyRostyButtonSizing(Button b)
    {
        if (b == null) return;
        var rt = b.GetComponent<RectTransform>();
        if (rt == null) return;

        // Ajuste de alto (y mantiene el ancho por layout)
        float h = Mathf.RoundToInt(rostyButtonHeight * uiScale);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);

        // Ajustar font del label
        var t = b.transform.Find("Label");
        if (t != null)
        {
            var tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = Mathf.RoundToInt(rostyButtonFontSize * uiScale);
        }
    }

// ===================== Rosty: layout + input guard =====================
void ApplyRostyChoiceButtonLayout(Button b)
{
    if (b == null) return;

    ApplyRostyButtonSizing(b);

    // ✅ Opciones más compactas (no llenan todo el ancho)
    var le = b.GetComponent<LayoutElement>();
    if (le == null) le = b.gameObject.AddComponent<LayoutElement>();

    le.minHeight = Mathf.RoundToInt(rostyButtonHeight * uiScale);
    le.preferredHeight = le.minHeight;

    // Base: ancho "cómodo" (luego RostyApplyChoiceWidths lo ajusta para casos especiales)
    float baseW = Mathf.RoundToInt(220f * uiScale);
    le.flexibleWidth = 0f;
    le.preferredWidth = baseW;
    le.minWidth = Mathf.RoundToInt(140f * uiScale);
}

void ApplyRostyNextButtonLayout(Button b)
{
    if (b == null) return;

    ApplyRostyButtonSizing(b);

    // ✅ NEXT centrado: sin expand horizontal, ancho "natural" del botón
    var le = b.GetComponent<LayoutElement>();
    if (le == null) le = b.gameObject.AddComponent<LayoutElement>();

    le.minHeight = Mathf.RoundToInt(rostyButtonHeight * uiScale);
    le.preferredHeight = le.minHeight;

    le.flexibleWidth = 0f;

    // Dejar un ancho razonable para que se vea centrado y legible
    float w = Mathf.Max(180f, rostyButtonHeight * 2.4f) * uiScale;
    le.preferredWidth = Mathf.RoundToInt(w);
    le.minWidth = Mathf.RoundToInt(w * 0.85f);
}

bool RostySubmitBlocked()
{
    // ✅ Bloqueo por tiempo (anti-rebote): evita que un Enter/Submit anterior active botones.
    return Time.unscaledTime < _rostySubmitGuardUntil;
}

void RostyStartInputGuard(float seconds)
{
    if (!Application.isPlaying) return;

    seconds = Mathf.Max(0f, seconds);

    _rostySkipGuardUntil = Time.unscaledTime + seconds;
    _rostySubmitGuardUntil = Time.unscaledTime + seconds;

    // (FIX) Si el GO/componente está inactivo, no tocar coroutines (Unity no permite StartCoroutine en inactivos).
    if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
    {
        _coRostyInputGuard = null;
        return;
    }

    if (_coRostyInputGuard != null)
    {
        StopCoroutine(_coRostyInputGuard);
        _coRostyInputGuard = null;
    }

    _coRostyInputGuard = StartCoroutine(Co_RostyInputGuard());
}

IEnumerator Co_RostyInputGuard()
{
    // Esperar a que sueltes Enter/Submit
    while (IsSubmitHeldSafe())
        yield return null;

    // Esperar a que pase el tiempo del guard
    while (Time.unscaledTime < _rostySubmitGuardUntil)
        yield return null;

    _coRostyInputGuard = null;
}

void SetButtonLabel(Button b, string text)
    {
        if (b == null) return;
        var t = b.transform.Find("Label");
        if (t == null) return;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    // ===================== Rosty: botones de elección (casos especiales) =====================
    
    // ===================== Rosty: botones de elección (casos especiales) =====================
    
    // ===================== Rosty: botones de elección (casos especiales) =====================
    // ===================== Rosty: botones de elección (casos especiales) =====================
    void RostyApplyChoiceWidths(string question, string optA, string optB)
    {
        if (_btnRostyA == null || _btnRostyB == null) return;

        // ---------- Detectar casos ----------
        string a = (optA ?? "").Trim().ToUpperInvariant();
        string b = (optB ?? "").Trim().ToUpperInvariant();

        bool isYesNoPair = (b == "NO") && (a.StartsWith("SI") || a.StartsWith("SÍ") || a.StartsWith("YES"));
        bool isLongYes = isYesNoPair && (
            (a.Contains("LLEVO") && a.Contains("GORRA")) ||
            (a.Contains("WEAR") && a.Contains("CAP")) ||
            (a.Contains("CAP") && a.StartsWith("YES"))
        );

        string q = (question ?? "").Trim().ToUpperInvariant();
        bool isDogBread = (q.Contains("PERRO") && q.Contains("PAN")) || (q.Contains("DOG") && q.Contains("BREAD"));

        // ---------- Parent / layout ----------
        var row = _btnRostyA.transform.parent as RectTransform;

        // ✅ Evitar que un LayoutGroup existente te pise el layout manual (esto es lo que causaba superposición).
        if (row != null)
        {
            var lg = row.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (lg != null) lg.enabled = false;
        }

        var rtA = _btnRostyA.GetComponent<RectTransform>();
        var rtB = _btnRostyB.GetComponent<RectTransform>();
        if (rtA == null || rtB == null) return;

        // ---------- Reset robusto ----------
        RostyManualRectReset(rtA);
        RostyManualRectReset(rtB);

        float h = Mathf.RoundToInt(rostyButtonHeight * uiScale);

        // ---------- Medidas (ajustadas a tus capturas) ----------
        float gap;
        float wA;
        float wB;

        if (isDogBread)
        {
            RostyLayoutDogBread(row, rtA, rtB, h);
            return;
        }

        if (isYesNoPair && isLongYes)
{
            // Caso “SI, ES QUE LLEVO GORRA” / “YES (I wear a cap)”:
            // - Dejar un “vacío” visible entre botones (tu zona roja)
            // - NO más chico
            // - SÍ con letra más grande (evitar que TMP la baje demasiado)
            bool longYesEnglish = a.StartsWith("YES");

            gap = Mathf.RoundToInt(44f * uiScale);   // más juntos (sin superponer)
            wB  = Mathf.RoundToInt(112f * uiScale);  // NO chico (mantiene estilo)
            wA  = Mathf.RoundToInt((longYesEnglish ? 336f : 308f) * uiScale);

            RostyApplyChoiceLabelStyle(_btnRostyA, autoSize: true);
            RostyApplyChoiceLabelStyle(_btnRostyB, autoSize: false);

            // Mantener la letra grande (si no, TMP la baja de más)
            RostyOverrideChoiceLabelMin(_btnRostyA, 0.95f);
        }
        else if (isYesNoPair)
        {
            // Caso normal SÍ/NO (pato-bananos, frío/calor, etc.)
            // - Más angostos
            // - Más centrados
            // - Con espacio entre ambos (sin exagerar)
            gap = Mathf.RoundToInt(22f * uiScale);
            wA  = Mathf.RoundToInt(118f * uiScale);
            wB  = Mathf.RoundToInt(118f * uiScale);

            RostyApplyChoiceLabelStyle(_btnRostyA, autoSize: false);
            RostyApplyChoiceLabelStyle(_btnRostyB, autoSize: false);
        }
        else
        {
            // Fallback para otros pares
            gap = Mathf.RoundToInt(rostyButtonsSpacing * uiScale);
            wA = Mathf.RoundToInt(210f * uiScale);
            wB = Mathf.RoundToInt(210f * uiScale);

            RostyApplyChoiceLabelStyle(_btnRostyA, autoSize: true);
            RostyApplyChoiceLabelStyle(_btnRostyB, autoSize: true);
        }

        // ---------- Aplicar sizes ----------
        rtA.sizeDelta = new Vector2(wA, h);
        rtB.sizeDelta = new Vector2(wB, h);

        // ---------- Centrar el “grupo” (A + gap + B) ----------
        float total = wA + gap + wB;
        float startX = -total * 0.5f;

        float shiftX = 0f;
        // Dog+Bread: correr el grupo a la derecha para que NO pise la linea/borde izquierdo.
        if (isDogBread && row != null)
        {
            float rowW = row.rect.width;
            float safeLeft = Mathf.RoundToInt(54f * uiScale);
            float safeRight = Mathf.RoundToInt(34f * uiScale);

            float minEdge = -rowW * 0.5f + safeLeft;
            float maxEdge =  rowW * 0.5f - safeRight;

            float leftEdge = startX;          // borde izquierdo del botón A sin shift
            float rightEdge = startX + total; // borde derecho del botón B sin shift

            if (leftEdge < minEdge) shiftX += (minEdge - leftEdge);
            // si al correr a la derecha nos pasamos del borde derecho, corregimos
            if (rightEdge + shiftX > maxEdge) shiftX -= (rightEdge + shiftX - maxEdge);
        }

        rtA.anchoredPosition = new Vector2(startX + wA * 0.5f + shiftX, 0f);
        rtB.anchoredPosition = new Vector2(startX + wA + gap + wB * 0.5f + shiftX, 0f);

        // ---------- Forzar rebuild ----------
        Canvas.ForceUpdateCanvases();

        // ✅ Dog+Bread: que el botón derecho use el MISMO tamaño real de fuente que el izquierdo
        if (isDogBread) RostyCopyChoiceFontSizeAToB(_btnRostyA, _btnRostyB);

        if (row != null) LayoutRebuilder.ForceRebuildLayoutImmediate(row);
    }

    
// Dog+Bread: layout manual (como los recuadros rojos) + clamp a bordes para que NO pise la línea blanca
void RostyLayoutDogBread(RectTransform row, RectTransform rtA, RectTransform rtB, float h)
{
    if (row == null || rtA == null || rtB == null) return;

    // Evitar que LayoutGroups te pisen
    var lg = row.GetComponent<UnityEngine.UI.LayoutGroup>();
    if (lg != null) lg.enabled = false;

    // Asegurar tamaños/rect actualizados antes de medir
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(row);

    // Labels (TextMeshProUGUI)
    TextMeshProUGUI tmpA = null;
    TextMeshProUGUI tmpB = null;

    var la = _btnRostyA != null ? _btnRostyA.transform.Find("Label") : null;
    var lb = _btnRostyB != null ? _btnRostyB.transform.Find("Label") : null;

    if (la != null) tmpA = la.GetComponent<TextMeshProUGUI>();
    if (lb != null) tmpB = lb.GetComponent<TextMeshProUGUI>();

    if (tmpA == null && _btnRostyA != null) tmpA = _btnRostyA.GetComponentInChildren<TextMeshProUGUI>(true);
    if (tmpB == null && _btnRostyB != null) tmpB = _btnRostyB.GetComponentInChildren<TextMeshProUGUI>(true);

    
    // Normalizar rect del Label para que el texto no quede "tapado" por el chevron y no pierda la última letra.
    float labelPadL = Mathf.RoundToInt(14f * uiScale);
    float labelPadR = Mathf.RoundToInt(26f * uiScale);

    RectTransform labelRtA = (la != null) ? la.GetComponent<RectTransform>() : null;
    RectTransform labelRtB = (lb != null) ? lb.GetComponent<RectTransform>() : null;

    if (labelRtA != null)
    {
        labelRtA.anchorMin = Vector2.zero;
        labelRtA.anchorMax = Vector2.one;
        labelRtA.pivot = new Vector2(0.5f, 0.5f);
        labelRtA.offsetMin = new Vector2(labelPadL, 0f);
        labelRtA.offsetMax = new Vector2(-labelPadR, 0f); // reserva chica para el chevron
    }

    if (labelRtB != null)
    {
        labelRtB.anchorMin = Vector2.zero;
        labelRtB.anchorMax = Vector2.one;
        labelRtB.pivot = new Vector2(0.5f, 0.5f);
        labelRtB.offsetMin = new Vector2(labelPadL, 0f);
        labelRtB.offsetMax = new Vector2(-labelPadR, 0f);
    }

    if (tmpA != null) tmpA.margin = Vector4.zero;
    if (tmpB != null) tmpB.margin = Vector4.zero;

// Estilo estable: sin autosize respirando, sin wrap
    if (tmpA != null)
    {
        tmpA.enableAutoSizing = false;
        tmpA.enableWordWrapping = false;
        tmpA.overflowMode = TextOverflowModes.Truncate; // si no entra, trunca (preferible a superponer)
    }
    if (tmpB != null)
    {
        tmpB.enableAutoSizing = false;
        tmpB.enableWordWrapping = false;
        tmpB.overflowMode = TextOverflowModes.Truncate;
    }

    // Rects: ancla a la izquierda para poder respetar safeLeft/safeRight
    rtA.anchorMin = rtA.anchorMax = new Vector2(0f, 0.5f);
    rtB.anchorMin = rtB.anchorMax = new Vector2(0f, 0.5f);
    rtA.pivot = rtB.pivot = new Vector2(0f, 0.5f);

    var leA = rtA.GetComponent<UnityEngine.UI.LayoutElement>();
    var leB = rtB.GetComponent<UnityEngine.UI.LayoutElement>();
    if (leA != null) leA.ignoreLayout = true;
    if (leB != null) leB.ignoreLayout = true;

    // ======= Inspector knobs (en pixeles a uiScale=1) =======
    float rowW = row.rect.width;

    float safeLeft  = Mathf.RoundToInt(rostyDogBreadSafeLeft * uiScale);
    float safeRight = Mathf.RoundToInt(rostyDogBreadSafeRight * uiScale);

    float gap = Mathf.RoundToInt(rostyDogBreadGap * uiScale);
    float minGap = Mathf.RoundToInt(24f * uiScale);

    float padA = Mathf.RoundToInt(rostyDogBreadPadA * uiScale);
    float padB = Mathf.RoundToInt(rostyDogBreadPadB * uiScale);

    float minA = Mathf.RoundToInt(rostyDogBreadMinWidthA * uiScale);
    float minB = Mathf.RoundToInt(rostyDogBreadMinWidthB * uiScale);

    // Font size fijo (no iterar / no “respirar”)
    int baseFs = (rostyDogBreadButtonFontSizeOverride > 0) ? rostyDogBreadButtonFontSizeOverride : rostyButtonFontSize;
    int fs = Mathf.RoundToInt(baseFs * uiScale * rostyDogBreadFontScale);
    fs = Mathf.Max(fs, Mathf.RoundToInt(14f * uiScale));

    if (tmpA != null) { tmpA.fontSizeMin = fs; tmpA.fontSizeMax = fs; tmpA.fontSize = fs; tmpA.ForceMeshUpdate(); }
    if (tmpB != null) { tmpB.fontSizeMin = fs; tmpB.fontSizeMax = fs; tmpB.fontSize = fs; tmpB.ForceMeshUpdate(); }

    float available = Mathf.Max(0f, rowW - safeLeft - safeRight);

    // Medir anchos requeridos (texto completo + padding)
    float needA = (tmpA != null) ? (tmpA.preferredWidth + padA) : minA;
    float needB = (tmpB != null) ? (tmpB.preferredWidth + padB) : minB;

    // Darle prioridad al izquierdo (tu rojo: A más grande que B)
    float wA = Mathf.RoundToInt(Mathf.Max(minA, needA));
    float wB = Mathf.RoundToInt(Mathf.Max(minB, needB));

    // Clamp suave a available
    if (available > 0f)
    {
        // Reducir gap primero si no entra
        float total = wA + gap + wB;
        if (total > available)
        {
            gap = Mathf.Max(minGap, gap - Mathf.RoundToInt(total - available));
            total = wA + gap + wB;
        }

        // Si sigue sin entrar, repartir anchos manteniendo proporción (sin tocar fuente)
        if (wA + gap + wB > available)
        {
            float usable = Mathf.Max(0f, available - gap);
            float sum = Mathf.Max(1f, wA + wB);

            float ratioA = wA / sum; // A mantiene mayor prioridad
            float targetA = usable * ratioA;

            wA = Mathf.RoundToInt(Mathf.Clamp(targetA, minA, usable - minB));
            wB = Mathf.RoundToInt(Mathf.Clamp(usable - wA, minB, usable - minA));
        }
    }

    // Aplicar tamaños (mismo alto)
    rtA.sizeDelta = new Vector2(wA, h);
    rtB.sizeDelta = new Vector2(wB, h);

    // Posicionar dentro del área segura, centrados como grupo
    float totalW = wA + gap + wB;
    float startX = safeLeft + Mathf.Max(0f, (available - totalW) * 0.5f);

    rtA.anchoredPosition = new Vector2(startX, 0f);
    rtB.anchoredPosition = new Vector2(startX + wA + gap, 0f);

    // Rebuild final
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(row);
}


void RostyManualRectReset(RectTransform rt)
    {
        if (rt == null) return;

        // Centro, sin stretch (para que anchoredPosition sea confiable)
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Limpiar offsets / escala para evitar “arrastres” raros
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void RostyResetChoiceRect(Button b)
    {
        if (b == null) return;
        var rt = b.GetComponent<RectTransform>();
        if (rt == null) return;

        // Volver a valores "neutros" compatibles con LayoutGroups
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // ancho lo maneja el layout; alto lo re-aplicamos
        rt.sizeDelta = new Vector2(0f, Mathf.RoundToInt(rostyButtonHeight * uiScale));
    }

    void RostyApplyChoiceLabelStyle(Button b, bool autoSize)
    {
        if (b == null) return;
        var t = b.transform.Find("Label");
        if (t == null) return;

        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        // Evitar que el texto invada el botón vecino (adiós superposición)
        tmp.overflowMode = autoSize ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;

        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = autoSize;

        float max = Mathf.RoundToInt(rostyButtonFontSize * uiScale);
        tmp.fontSizeMax = max;
        tmp.fontSize = max;

        // si autosize, permitir bajar un poco
        tmp.fontSizeMin = autoSize ? Mathf.RoundToInt(Mathf.Max(7f, 9f * uiScale)) : max;
    }

void RostyOverrideChoiceLabelMin(Button b, float minMul01)
{
    if (b == null) return;
    var t = b.transform.Find("Label");
    if (t == null) return;

    var tmp = t.GetComponent<TextMeshProUGUI>();
    if (tmp == null) return;

    float mul = Mathf.Clamp01(minMul01);
    tmp.fontSizeMin = Mathf.RoundToInt(tmp.fontSizeMax * mul);
}


    void RostyCopyChoiceFontSizeAToB(Button btnA, Button btnB)
    {
        if (btnA == null || btnB == null) return;
        var ta = btnA.transform.Find("Label");
        var tb = btnB.transform.Find("Label");
        if (ta == null || tb == null) return;

        var tmpA = ta.GetComponent<TextMeshProUGUI>();
        var tmpB = tb.GetComponent<TextMeshProUGUI>();
        if (tmpA == null || tmpB == null) return;

        float fs = tmpA.fontSize;
        tmpB.enableAutoSizing = false;
        tmpB.fontSizeMax = fs;
        tmpB.fontSizeMin = fs;
        tmpB.fontSize = fs;
    }



    // ✅ NO out/ref (CS1623 fix)
    IEnumerator RostyAsk2(string question, string optA, string optB, RostyEmotion questionEmotion = RostyEmotion.Speech)
    {
        if (_rostyBodyTMP == null) yield break;

        // ✅ Reset de fuente / autosize / márgenes por si el paso anterior los tocó
        int _rostyBodyBaseSize = Mathf.RoundToInt(rostyBodyFontSize * uiScale);
        _rostyBodyTMP.enableAutoSizing = false;
        _rostyBodyTMP.fontSize = _rostyBodyBaseSize;
        _rostyBodyTMP.fontSizeMax = _rostyBodyBaseSize;
        _rostyBodyTMP.fontSizeMin = _rostyBodyBaseSize;
        _rostyBodyTMP.margin = Vector4.zero;

        string _qUpper = (question ?? "").ToUpperInvariant();
        bool _isCyberGrandma = _qUpper.Contains("CIBERABUELA") || _qUpper.Contains("CYBER-GRANDMA");

        // ✅ Para la ciberabuela:
        // - Reservamos lugar para los botones (así no pisan el texto)
        // - Dejamos que TMP calcule el tamaño UNA sola vez con el texto completo
        // - Luego congelamos ese tamaño para que NO "respire" mientras tipea
        if (_isCyberGrandma)
        {
            float bottomPad = Mathf.RoundToInt((rostyButtonHeight + 28f) * uiScale);
            _rostyBodyTMP.margin = new Vector4(0f, 0f, 0f, bottomPad);

            // Pre-cálculo de tamaño (1 sola vez)
            _rostyBodyTMP.enableAutoSizing = true;
            _rostyBodyTMP.fontSizeMax = _rostyBodyBaseSize;
            _rostyBodyTMP.fontSizeMin = Mathf.RoundToInt(_rostyBodyBaseSize * 0.72f);
            _rostyBodyTMP.fontSize = _rostyBodyBaseSize;

            _rostyBodyTMP.text = (question ?? "");
            _rostyBodyTMP.ForceMeshUpdate();

            float fixedSize = _rostyBodyTMP.fontSize;

            // Congelar tamaño
            _rostyBodyTMP.enableAutoSizing = false;
            _rostyBodyTMP.fontSizeMax = fixedSize;
            _rostyBodyTMP.fontSizeMin = fixedSize;
            _rostyBodyTMP.fontSize = fixedSize;

            // Dejamos limpio para el typewriter
            _rostyBodyTMP.text = "";
        }
        // Pantalla única: escribimos la pregunta en limpio
        _rostyBodyTMP.text = "";
        RostySetEmotion(RostyEmotion.Speech);

        if (!string.IsNullOrEmpty(question))
            yield return RostyTypeAppend(question);

        // ✅ Al final de la pregunta: emoción por línea (se mantiene mientras el jugador elige)
        if (questionEmotion != RostyEmotion.Speech) RostySetEmotion(questionEmotion);

        // Esperar a que se suelte el Enter usado para completar texto (y evitar auto-submit)
        RostyStartInputGuard(rostySubmitGuardSeconds);

        _rostyChoice = -1;
        _rostyWaitingChoice = true;

        SetButtonLabel(_btnRostyA, optA);
        SetButtonLabel(_btnRostyB, optB);
        RostyApplyChoiceWidths(question, optA, optB);

        _btnRostyA.gameObject.SetActive(true);
        _btnRostyB.gameObject.SetActive(true);

        // ✅ FIX: forzar layout para evitar superposición de botones
        Canvas.ForceUpdateCanvases();
        var _row = _btnRostyA != null ? _btnRostyA.transform.parent as RectTransform : null;
        if (_row != null)
        {
            // Guardar Y base una sola vez (para no acumular offsets)
            if (!_rostyChoiceRowBaseYInit)
            {
                _rostyChoiceRowBaseY = _row.anchoredPosition.y;
                _rostyChoiceRowBaseYInit = true;
            }

            // ✅ Ciberabuela: bajar un poco la fila de botones para que conviva con el texto
            float yOffset = _isCyberGrandma ? Mathf.RoundToInt(-42f * uiScale) : 0f;
            _row.anchoredPosition = new Vector2(_row.anchoredPosition.x, _rostyChoiceRowBaseY + yOffset);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_row);
        }

        _btnRostyBegin.gameObject.SetActive(false);

        // Navegación explícita A <-> B (horizontal)
        if (_btnRostyA != null && _btnRostyB != null)
        {
            var navA = new Navigation { mode = Navigation.Mode.Explicit };
            navA.selectOnLeft = _btnRostyA;
            navA.selectOnRight = _btnRostyB;
            navA.selectOnUp = _btnRostyA;
            navA.selectOnDown = _btnRostyA;
            _btnRostyA.navigation = navA;

            var navB = new Navigation { mode = Navigation.Mode.Explicit };
            navB.selectOnLeft = _btnRostyA;
            navB.selectOnRight = _btnRostyB;
            navB.selectOnUp = _btnRostyB;
            navB.selectOnDown = _btnRostyB;
            _btnRostyB.navigation = navB;
        }

        if (_es != null) _es.SetSelectedGameObject(_btnRostyA.gameObject);

        while (_rostyChoice < 0)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible) yield break;
            yield return null;
        }

        _rostyLastChoiceA = (_rostyChoice == 0);
        _rostyWaitingChoice = false;

        _btnRostyA.gameObject.SetActive(false);
        _btnRostyB.gameObject.SetActive(false);

        // Bloqueo cortito para evitar que el mismo Enter dispare NEXT/BEGIN
        RostyStartInputGuard(rostySubmitGuardSeconds);
    }


    
    // ✅ Mostrar opciones A/B sin borrar el texto actual (para el primer saludo)
    IEnumerator RostyChooseAfterCurrentText(string optA, string optB)
    {
        if (_btnRostyA == null || _btnRostyB == null) yield break;

        // Guard anti-rebote (Enter que llegó desde el INIT SESSION / NEXT anterior)
        RostyStartInputGuard(Mathf.Max(0.02f, rostySubmitGuardSeconds));

        _rostyChoice = -1;
        _rostyWaitingChoice = true;

        SetButtonLabel(_btnRostyA, optA);
        SetButtonLabel(_btnRostyB, optB);

        _btnRostyA.gameObject.SetActive(true);
        _btnRostyB.gameObject.SetActive(true);
        _btnRostyBegin.gameObject.SetActive(false);

        // Navegación horizontal explícita (A <-> B)
        SetHorizontalNavAB();

        if (_es != null) _es.SetSelectedGameObject(_btnRostyA.gameObject);

        while (_rostyChoice < 0)
        {
            if (_panelActual != PanelId.Rosty || !_menuVisible) yield break;
            yield return null;
        }

        _rostyLastChoiceA = (_rostyChoice == 0);
        _rostyWaitingChoice = false;

        _btnRostyA.gameObject.SetActive(false);
        _btnRostyB.gameObject.SetActive(false);

        // Guard post-elección
        RostyStartInputGuard(Mathf.Max(0.02f, rostyPostAdvanceGuardSeconds));
    }

    void SetHorizontalNavAB()
    {
        if (_btnRostyA == null || _btnRostyB == null) return;

        var navA = new Navigation { mode = Navigation.Mode.Explicit };
        navA.selectOnLeft = _btnRostyA;
        navA.selectOnRight = _btnRostyB;
        navA.selectOnUp = _btnRostyA;
        navA.selectOnDown = _btnRostyA;
        _btnRostyA.navigation = navA;

        var navB = new Navigation { mode = Navigation.Mode.Explicit };
        navB.selectOnLeft = _btnRostyA;
        navB.selectOnRight = _btnRostyB;
        navB.selectOnUp = _btnRostyB;
        navB.selectOnDown = _btnRostyB;
        _btnRostyB.navigation = navB;
    }

IEnumerator Co_RostyIntro_Safe()
    {
        var inner = Co_RostyIntro_Inner();

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

        RostyTypingSFX_Stop(true);
        SetRostyFaceIdle();

        _rostyWaitingChoice = false;
        _rostyWaitingBegin = false;
        _rostyChoice = -1;

        _rostyCo = null;
    }

    IEnumerator Co_RostyIntro_Inner()
    {
        if (_rostyBodyTMP == null) yield break;

        // refrescar textos por idioma
        if (_btnBackRosty != null) SetButtonLabel(_btnBackRosty, RL("VOLVER", "BACK"));

        // ==========================================================
        //  TEXTO 1 (pregunta con opciones) - SIN NEXT antes
        // ==========================================================
        yield return RostyAsk2(
            RL(
                "Hola! Soy R04-STY. Rosty para los amigos, te recuerdo de algún lado... ¿Ya has estado por aquí antes no?",
                "Hey! I'm R04-STY. Rosty for friends. Do I remember you from somewhere... you've been here before, right?"
            ),
            RL("SI, ES QUE LLEVO GORRA", "YES (I wear a cap)"),
            RL("NO", "NO")
        );

        bool capYes = _rostyLastChoiceA;

        // Borrar pregunta antes de respuesta
        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (capYes)
        {
            // "Eso era… y la cara de—" (tipea) -> pausa -> borra lento con sfx -> tipea remate, y termina riéndose hasta NEXT
            string gag = RL("Eso era… y la cara de—", "That was it… and the face of—");
            string punch = RL(
                "Eeh.. quiero decir... No me mientas. No me tomes el pelo… ¿O debería decir: no me tomes las MECHAS?",
                "Two caps detected: one on your head, one on my patience. Keep lying and I cap your permissions. Don’t pull my wires xd."
            );

            // ✅ Durante el gag: ENOJO mientras se escribe
            RostySetEmotion(RostyEmotion.Angry);
            yield return RostyTypeAppend(gag);

            // ✅ 4s mínimo para que se pueda leer (aunque el Inspector tenga menos)
            yield return new WaitForSecondsRealtime(2.5f); // fijo: 2.5s para leer el gag

            yield return RostyEraseChars(gag.Length, Mathf.Max(10f, rostyCapEraseCps));

            // ✅ Luego: RIENDO mientras escribe el remate
            RostySetEmotion(RostyEmotion.Laugh);
            yield return RostyTypeAppend(punch);

            // Mantener risa hasta NEXT
            yield return RostyWaitNext(keepExpression: true);
            SetRostyFaceIdle();
        }
        else
        {
            yield return RostyShowTextAndNext(RL(
                "Bueno, te perdiste un chiste buenísimo.",
                "Don’t play with me, xXShadowSniper14Xx. My biometrics are damaged, not my ability to recognize bad usernames."
            ), RostyEmotion.Laugh);
        }

        // ==========================================================
        //  TEXTO 2
        // ==========================================================
        yield return RostyShowTextAndNext(RL(
            "¡VAYA! te dieron el exoesqueleto más viejito, el que tira humo como 504 gasolero.",
            "Wow. They gave you the oldest exoskeleton — the one that smokes like an old school bus. Cool!."
        ), RostyEmotion.Doubt);

        // ==========================================================
        //  TEXTO 3
        // ==========================================================
        yield return RostyShowTextAndNext(RL(
            "Bueno mi tigre de bengala, te toca practicar con ese. Junta microchips y vemos: upgrade… o jubilación anticipada.",
            "Alright my Bengal tiger, you train with that one. Collect microchips and we'll see: upgrade… or early retirement."
        ), RostyEmotion.Laugh);

        // ==========================================================
        //  TEXTO 4
        // ==========================================================
        yield return RostyShowTextAndNext(RL(
            "Si te quedas sin energía tu exo ejecuta el protocolo 'besar el piso' asi que recuerda tomar nuevos núcleos antes de quedarte sin, hacen tic-tac!",
            "If you run out of energy your exo triggers the 'kiss-the-floor' protocol. Grab new cores before you hit zero. Tick-tock!"
        ), RostyEmotion.Sad);

        // ==========================================================
        //  TEXTO 5 (test + spoiler que se borra lento)
        // ==========================================================
        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";
        SetRostyFaceIdle();

        yield return RostyTypeAppend(RL(
            "Perfecto. Pasemos al test de personalidad.",
            "Perfect. Time for the personality test."
        ));

        string spoiler = RL(
            " Spoiler: voy a ignorar tus respuestas con profesionalismo.",
            " Spoiler: I'll ignore your answers professionally."
        );

        yield return RostyTypeAppend(spoiler);

        // ✅ dar tiempo a leer antes de borrar
        if (rostySpoilerHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(rostySpoilerHoldSeconds);

        // ✅ borrar más lento y con sfx
        yield return RostyEraseChars(spoiler.Length, Mathf.Max(10f, rostySpoilerEraseCps));

        // ✅ al terminar el borrado: RIENDO
        RostySetEmotion(RostyEmotion.Laugh);
        yield return RostyWaitNext(keepExpression: true);

        // ==========================================================
        //  TEXTO 6 (pato-bananos) - pregunta sin "Pregunta:"
        // ==========================================================
        yield return RostyAsk2(
            RL("No puedo creer que esto esté en el script. Otra vez. ¿Te gustan los pato-bananos?", "I can’t believe this is in the script. Again. Do you like banana-ducks?"),
            RL("ME ENCANTAN", "LOVE THEM"),
            RL("ODIO", "HATE THEM"),
            RostyEmotion.Surprised
        );
bool likesDuck = _rostyLastChoiceA;

        PlayerPrefs.SetInt(rostyDuckBananaKey, likesDuck ? 1 : 0);
        PlayerPrefs.Save();

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (likesDuck)
        {
            yield return RostyShowTextAndNext(RL(
                "Te gustan los pato-bananos. Excelente… no voy a hacer preguntas al respecto.",
                "You like banana-ducks. Excellent… I won't ask questions about that."
            ), RostyEmotion.Speechless);
        }
        else
        {
            yield return RostyShowTextAndNext(RL(
                "Perfecto. Tu respuesta fue registrada… y descartada con éxito.",
                "Perfect. Your answer has been recorded… and successfully discarded."
            ), RostyEmotion.Laugh);
        }

        // ==========================================================
        //  TEXTO 7 (frío/calor)
        // ==========================================================
        yield return RostyAsk2(
            RL("A ver, humano… ¿Prefieres el frío o el calor?", "Alright human… simple question: cold or hot?"),
            RL("FRÍO", "COLD"),
            RL("CALOR", "HOT"),
            RostyEmotion.Surprised
        );
        bool choseCold = _rostyLastChoiceA;

        PlayerPrefs.SetInt(rostyColdKey, choseCold ? 1 : 0);
        PlayerPrefs.Save();

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (choseCold)
        {
            yield return RostyShowTextAndNext(RL(
                "¿Frío? Excelente. Elegiste la opción incorrecta para un juego que se llama HotRunner. Igual tranqui: te van a calentar a golpes.",
                "Cold? Excellent. Incorrect choice for a game called HotRunner. Relax — they’ll warm you up the hard way."
            ), RostyEmotion.Speechless);
        }
        else
        {
            yield return RostyShowTextAndNext(RL(
                "¿Calor? Excelente. HotRunner aprobado. No mejora tu caso… pero al menos entiendes el título.",
                "Hot? Excellent. HotRunner approved. It doesn’t help your case… but at least you understood the title."
            ), RostyEmotion.Laugh);
        }


        // ==========================================================
        //  TEXTO 8B (perdón rufián) - va después de frío/calor
        // ==========================================================
        yield return RostyShowTextAndNext(RL(
            "Perdón rufián… a veces me tomo estos tests demasiado personales.",
            "Sorry ruffian… sometimes I take these tests way too personally."
        ), RostyEmotion.Sad);

        // ==========================================================
        //  TEXTO 8 (ciberabuela)
        // ==========================================================
        yield return RostyAsk2(
            RL(
                "Una ciberabuela sube a una unidad de transporte colectiva, te golpea en la rodilla con su bastón-scanner (como queriendo que notes su presencia).\nDespués sentís en tu nuca su respiración y un “bip bip” constante.\n¿Le cedés el asiento o mirás por la ventana AFK?",
                "A cyber-grandma gets on a mass-transit unit. She smacks your knee with her cane-scanner (like she REALLY wants you to notice her).\nThen you feel her breathing on your neck and a constant “beep beep”.\nDo you give her your seat, or stare out the window AFK?"
            ),
            RL("SE LO CEDO", "GIVE HER THE SEAT"),
            RL("AFK", "AFK")
        );
        bool gaveSeat = _rostyLastChoiceA;

        PlayerPrefs.SetInt(rostyCyberGrandmaKey, gaveSeat ? 1 : 0);
        PlayerPrefs.Save();

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (gaveSeat)
        {
            yield return RostyShowTextAndNext(RL(
                "Ah, elegiste ceder el asiento… y cuando el transporte frene, manotearle el bastón-scanner y salir corriendo.\nExcelente ser humano.",
                "Ah, you gave up your seat… and when the bus stops, you snatch the cane-scanner and run.\nExcellent human being."
            ), RostyEmotion.Doubt);
        }
        else
        {
            yield return RostyShowTextAndNext(RL(
                "Ah, elegiste AFK. Excelente ser humano.\nAhora vas a viajar todo el trayecto con la culpa y el “bip bip” en la nuca. Te lo merecés.",
                "Ah, you chose AFK. Excellent human being.\nNow you'll ride the whole way with guilt and that “beep beep” at the back of your neck. You deserve it."
            ), RostyEmotion.Laugh);
        }

        // ==========================================================
        //  TEXTO 9 (perro + pan)
        // ==========================================================
        yield return RostyAsk2(
            RL("¿Por qué el perro lleva el pan en la boca?", "Why is the dog carrying a loaf of bread in its mouth?"),
            RL("PORQUE NO TIENE MANOS", "BECAUSE IT HAS NO HANDS"),
            RL("SE LO ROBÓ", "IT STOLE IT")
        );
        bool dogNoHands = _rostyLastChoiceA;

        PlayerPrefs.SetInt(rostyDogBreadKey, dogNoHands ? 1 : 0);
        PlayerPrefs.Save();

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (dogNoHands)
        {
            yield return RostyShowTextAndNext(RL(
                "Eres todo un comediante… ¿algo más? ¿un “toc-toc”? ¿un monólogo completo?\nUPDATE: Capacidad máxima de payasos alcanzada.",
                "You're quite the comedian… anything else? A knock-knock? A full monologue?\nUPDATE: Maximum clown capacity reached."
            ), RostyEmotion.Doubt);
        }
        else
        {
            yield return RostyShowTextAndNext(RL(
                "Sin sentido del humor.\n¿Quién es el no-humano ahora?",
                "No sense of humor.\nWho's the non-human now?"
            ), RostyEmotion.Doubt);
        }
        // ==========================================================
        //  TEXTO 11 (mechas)
        // ==========================================================
        yield return RostyAsk2(
            RL("¿Te gustan los mechas? Advertencia: tu respuesta puede tener consecuencias.", "Do you like mechas? Warning: consequences may apply."),
            RL("SÍ", "YES"),
            RL("NO", "NO"),
            RostyEmotion.Angry
        );
        bool likesMechas = _rostyLastChoiceA;

        PlayerPrefs.SetInt(rostyMechaKey, likesMechas ? 1 : 0);
        PlayerPrefs.Save();

        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";

        if (likesMechas)
        {
            yield return RostyShowTextAndNext(RL(
                "Perfecto. Respuesta registrada… y usada en tu contra cuando me resulte conveniente.\nUPDATE: nivel de confianza = 0.01%.",
                "Perfect. Answer recorded… and weaponized against you at my discretion.\nUPDATE: trust level = 0.01%"
            ), RostyEmotion.Laugh);
        }
        else
        {
            yield return RostyShowTextAndNext_EmotionSwitch(RL(
                "Perfecto. Amistad finalizada. (Duró más de lo esperado.)\nUPDATE: nivel de confianza = 0.00%.",
                "Perfect. Friendship terminated. (Lasted longer than expected.)\nUPDATE: trust level = 0.00%."
            ), RostyEmotion.Angry, 1.0f, RostyEmotion.Sad);
        }

        // ==========================================================
        //  TEXTO 13 (final) -> BEGIN
        // ==========================================================
        if (_rostyBodyTMP != null) _rostyBodyTMP.text = "";
        RostySetEmotion(RostyEmotion.Speech);

        yield return RostyTypeAppend(RL(
            "Bueno, maquinola del mal… el Nivel 1 de entrenamiento comienza ahora. A la vuelta te hago más preguntas... Broma. ¡Riete, es un chiste!",
            "Alright champ… Level 1 training starts now. When you get back, I’ll ask more questions… Joke. Laugh. It’s a joke."
        ));

        // al final: RIENDO
        RostySetEmotion(RostyEmotion.Laugh);

        // BEGIN
        yield return RostyWaitBegin(keepExpression: true);
    }



}