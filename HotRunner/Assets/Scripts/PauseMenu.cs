using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(1000)]
public class PauseMenu : MonoBehaviour
{
    // =========================
    // ✅ FLAGS GLOBALES (Crosshair Gate)
    // =========================
    public static bool IsPausedGlobal { get; private set; } = false;
    public static bool IsPlayerDeadGlobal { get; private set; } = false;

    // ✅ evita que otra instancia te pise el flag
    static PauseMenu _activePauseMenu = null;

    // ✅ NEW: bloquea apagar IsPausedGlobal durante transición a Main Menu
    static bool _lockPausedUntilSceneChange = false;
    static int _sceneHandleForStatics = -1;

    [Header("UI Root (se activa/desactiva)")]
    public GameObject pauseRoot;

    [Header("Opciones (orden fijo)")]
    public RectTransform[] optionRects;
    public Graphic[] optionGraphics;
    public Graphic[] extraGraphics;

    [Header("Fade del menú (sin CanvasGroup)")]
    public float menuFadeSeconds = 0.12f;

    [Header("Tambor de revólver")]
    public RectTransform drumCenter;
    public float radiusY = 150f;
    public float radiusX = 40f;
    public float stepAngleDegrees = 38f;

    [Header("Centro protagonista")]
    public float frontScale = 1.28f;
    public float centerPop = 0.10f;
    public float backScale = 0.70f;

    [Header("Difuminado atrás (sin shaders)")]
    [Range(0f, 1f)] public float backAlpha = 0.18f;
    [Range(0f, 1f)] public float frontAlpha = 1.0f;
    public Color backTint = new Color(0.65f, 0.70f, 0.75f, 1f);
    [Range(0f, 1f)] public float backTintStrength = 0.85f;

    public float sideTiltDegrees = 10f;
    public float wheelSmooth = 18f;

    [Header("Visibilidad del tambor (3 opciones)")]
    public bool limitVisibleOptions = true;
    public int visibleNeighbours = 1;
    public float hideFadeBuffer = 0.45f;

    [Header("Input")]
    public KeyCode pauseKey = KeyCode.Escape;

    // ✅ NEW: tecla extra para pausar
    public KeyCode pauseKeyAlt = KeyCode.P;

    public KeyCode acceptKey1 = KeyCode.Return;
    public KeyCode acceptKey2 = KeyCode.Space;

    public bool useVerticalAxisForGamepad = true;
    public float navInitialDelay = 0.25f;
    public float navRepeatRate = 0.10f;

    [Header("Anti doble ESC (debounce)")]
    public float toggleDebounceSeconds = 0.20f;

    [Header("Bloqueo de pausa (para intro/título)")]
    public bool allowPauseWhenTimeScaleZero = false;
    public float blockPauseForUnscaledSeconds = 0f;

    [Header("Main Menu")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Fade a negro para cargar MainMenu (auto)")]
    public float fadeToMenuSeconds = 0.25f;

    [Header("Audio - SFX Navegación/Selección")]
    public AudioClip sfxNavigate;
    public AudioClip sfxSelect;
    public AudioClip sfxBack;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioMixerGroup sfxMixerGroup;

    [Header("Audio - Música baja mientras pausa (elige UNO)")]
    public AudioMixer musicMixer;
    public string musicVolumeParam = "MusicVolume";
    public float pausedMusicOffsetDb = -10f;
    public float musicFadeSeconds = 0.15f;

    public AudioSource musicSource;
    [Range(0.05f, 1f)] public float pausedMusicMultiplier = 0.45f;

    [Header("Opcional - bloquear gameplay")]
    public Behaviour[] disableWhilePaused;

    [Header("Cursor del sistema (mouse)")]
    public bool showSystemCursorWhenPaused = true;

    [Header("Bloqueo por Muerte / GameOver")]
    public bool blockPauseWhenDead = true;
    public bool isPlayerDead = false;
    public bool forceClosePauseIfDead = true;

    [Header("Comportamiento al abrir")]
    public bool startOnResumeEveryOpen = true;

    // ==================================
    // Mouse Sensibility (CS2 style)
    // ==================================
    [Header("Mouse Sensibility (multiplicador estilo CS2)")]
    public int mouseSensitivityIndex = -1;
    public Slider mouseSensitivitySlider;
    public Component mouseSensitivityValueLabel;

    [Header("Rango / Default")]
    public float sensitivityMin = 0.10f;
    public float sensitivityMax = 8.00f;
    public float sensitivityDefault = 1.00f;

    [Tooltip("Paso por click (0.01 recomendado).")]
    public float sensitivityStep = 0.01f;

    [Header("Hold accel (3 marchas)")]
    public bool accelerateOnHold = true;

    [Tooltip("Después de este tiempo de hold, el step pasa a 0.10")]
    public float holdStep1After = 0.35f;
    public float holdStep1 = 0.10f;

    [Tooltip("Después de este tiempo de hold, el step pasa a 0.50")]
    public float holdStep2After = 3.00f;
    public float holdStep2 = 0.50f;

    [Tooltip("Después de este tiempo de hold, el step pasa a 1.00")]
    public float holdStep3After = 6.00f;
    public float holdStep3 = 1.00f;

    [Header("Guardar")]
    public string mouseSensitivityPrefKey = "MouseSensitivity";

    [Header("SFX Slider")]
    public AudioClip sfxSlider;

    [Header("Aplicación (CameraFollow)")]
    public MonoBehaviour mouseSensitivityTarget;
    public string mouseSensitivityTargetMethod = "SetMouseSensitivity";

    [Header("TMP Value Label - Estabilidad")]
    public bool forceTmpLeftAlignment = true;
    public bool forceTmpDisableAutoSize = true;
    public bool forceTmpNoWrap = true;

    [Range(0.50f, 1.00f)] public float valueLabelMonospaceEm = 0.72f;
    public bool useMonospaceTag = true;

    // ===== runtime =====
    int _index = 0;
    bool _isPaused = false;
    bool _transitioning = false;
    float _prevTimeScale = 1f;

    int _heldDir = 0;
    float _nextRepeatTime = 0f;
    bool _wasDirHeld = false;

    int _heldHDir = 0;
    float _nextHRepeatTime = 0f;
    bool _wasHDirHeld = false;
    float _hHoldStartTime = 0f;

    float _uiAlpha = 0f;
    float _uiAlphaTarget = 0f;

    Color[] _optBaseColors;
    Color[] _extraBaseColors;

    Vector3[] _origScale;
    Vector2[] _origPos;
    Quaternion[] _origRot;
    bool _origCached = false;

    float _sceneStartUnscaled;
    float _nextToggleAllowedTime = 0f;

    CursorLockMode _prevLockMode;
    bool _prevCursorVisible;

    bool _hasMixerValue = false;
    float _musicDbOriginal = 0f;
    float _musicSrcOriginal = 1f;
    Coroutine _musicFadeRoutine;

    static AudioSource _persistSfxSource;
    static GameObject _persistSfxGO;

    float _wheel;
    float _wheelTarget;
    Vector2 _centerPos;

    Image _fadeImg;
    static Sprite _whiteSprite;

    bool _ignoreSensCallback = false;
    static bool _sensInitializedThisSession = false;

    void Awake()
    {
        // ✅ NEW: Reset de estáticos al entrar a una escena nueva (evita que quede pegado en true al volver)
        int h = SceneManager.GetActiveScene().handle;
        if (h != _sceneHandleForStatics)
        {
            _sceneHandleForStatics = h;
            _lockPausedUntilSceneChange = false;
            IsPausedGlobal = false;
            _activePauseMenu = null;
            IsPlayerDeadGlobal = false;
        }

        // ✅ NO tocar IsPausedGlobal acá (hay escenarios con múltiples instancias)
        // Solo reseteamos dead global si este menú maneja ese estado con SetPlayerDead()
        IsPlayerDeadGlobal = false;

        _sceneStartUnscaled = Time.unscaledTime;

        if ((optionGraphics == null || optionGraphics.Length == 0) && optionRects != null)
        {
            optionGraphics = new Graphic[optionRects.Length];
            for (int i = 0; i < optionRects.Length; i++)
                optionGraphics[i] = optionRects[i] ? optionRects[i].GetComponent<Graphic>() : null;
        }

        if (optionGraphics != null)
        {
            _optBaseColors = new Color[optionGraphics.Length];
            for (int i = 0; i < optionGraphics.Length; i++)
                _optBaseColors[i] = optionGraphics[i] ? optionGraphics[i].color : Color.white;
        }

        if (extraGraphics != null)
        {
            _extraBaseColors = new Color[extraGraphics.Length];
            for (int i = 0; i < extraGraphics.Length; i++)
                _extraBaseColors[i] = extraGraphics[i] ? extraGraphics[i].color : Color.white;
        }

        SetupPersistentSfx();
        CacheMusicOriginal();

        if (pauseRoot != null) pauseRoot.SetActive(false);
        _uiAlpha = 0f;
        _uiAlphaTarget = 0f;

        ResolveCenter();
        _wheel = 0f;
        _wheelTarget = 0f;

        EnsureLocalFade();
        SetFadeAlpha(0f);

        InitMouseSensitivity();
        SyncMouseSensitivityUI();
    }

    void OnDisable()
    {
        // ✅ Si este menú era el dueño, apaga el flag (PERO no durante transición a MainMenu)
        if (_activePauseMenu == this && !_lockPausedUntilSceneChange)
        {
            IsPausedGlobal = false;
            _activePauseMenu = null;
        }
    }

    void OnDestroy()
    {
        if (_musicFadeRoutine != null)
        {
            try { StopCoroutine(_musicFadeRoutine); } catch { }
            _musicFadeRoutine = null;
        }

        ApplyPausedMusic(false, immediate: true);

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);

        // ✅ Si este menú era el dueño, apaga el flag (PERO no durante transición a MainMenu)
        if (_activePauseMenu == this && !_lockPausedUntilSceneChange)
        {
            IsPausedGlobal = false;
            _activePauseMenu = null;
        }
    }

    void Update()
    {
        if (blockPauseWhenDead && isPlayerDead)
        {
            if (forceClosePauseIfDead && _isPaused)
                ForceClosePause_NoSfx();
        }

        if (_transitioning) return;

        // ✅ NEW: ESC o P
        bool pausePressed = Input.GetKeyDown(pauseKey) || Input.GetKeyDown(pauseKeyAlt);

        if (pausePressed && Time.unscaledTime >= _nextToggleAllowedTime)
        {
            _nextToggleAllowedTime = Time.unscaledTime + toggleDebounceSeconds;

            if (blockPauseWhenDead && isPlayerDead) return;

            if (_isPaused) Resume(true);
            else TryPause();
        }

        if (_isPaused || _uiAlpha > 0.001f || _uiAlphaTarget > 0f)
        {
            UpdateMenuFade(Time.unscaledDeltaTime);
            ApplyMenuExtrasAlpha();
            ApplyDrumLayout(Time.unscaledDeltaTime);
        }

        // ✅ cuando termina el fade-out, recién ahí apagamos IsPausedGlobal (si no estamos yendo a MainMenu)
        if (!_isPaused && pauseRoot != null && pauseRoot.activeSelf && _uiAlpha <= 0.01f)
        {
            pauseRoot.SetActive(false);
            RestoreOriginalTransforms();

            if (_activePauseMenu == this && !_lockPausedUntilSceneChange)
            {
                IsPausedGlobal = false;
                _activePauseMenu = null;
            }
        }

        if (!_isPaused) return;

        if (showSystemCursorWhenPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        HandleNavigation();
        HandleMouseSensitivityAdjust();
        SyncMouseSensitivityUI();
    }

    void TryPause()
    {
        if (blockPauseWhenDead && isPlayerDead) return;

        if (!allowPauseWhenTimeScaleZero && Time.timeScale <= 0.0001f) return;

        if (blockPauseForUnscaledSeconds > 0f &&
            Time.unscaledTime - _sceneStartUnscaled < blockPauseForUnscaledSeconds)
            return;

        Pause();
    }

    public void Pause()
    {
        if (_isPaused) return;
        if (pauseRoot == null) return;
        if (blockPauseWhenDead && isPlayerDead) return;

        CacheOriginalTransforms();

        _isPaused = true;
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        pauseRoot.SetActive(true);
        _uiAlpha = 0f;
        _uiAlphaTarget = 1f;

        SetGameplayEnabled(false);

        if (showSystemCursorWhenPaused)
        {
            _prevLockMode = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        ApplyPausedMusic(true);

        _index = startOnResumeEveryOpen ? 0 : Mathf.Clamp(_index, 0, optionRects.Length - 1);
        _wheel = _index;
        _wheelTarget = _index;

        _heldDir = 0;
        _wasDirHeld = false;

        _heldHDir = 0;
        _wasHDirHeld = false;

        SyncMouseSensitivityUI();

        // ✅ ESTE menú pasa a ser el “dueño” del flag global
        _activePauseMenu = this;
        IsPausedGlobal = true;

        // ✅ Si estabas en transición antes, la cortamos acá
        _lockPausedUntilSceneChange = false;
    }

    public void Resume(bool fromEsc = false)
    {
        if (!_isPaused) return;

        if (fromEsc && sfxBack != null) PlaySfx(sfxBack);

        _isPaused = false;
        Time.timeScale = (_prevTimeScale <= 0.0001f) ? 1f : _prevTimeScale;

        SetGameplayEnabled(true);

        if (showSystemCursorWhenPaused)
        {
            Cursor.lockState = _prevLockMode;
            Cursor.visible = _prevCursorVisible;
        }

        ApplyPausedMusic(false);

        _uiAlphaTarget = 0f;

        _heldDir = 0;
        _wasDirHeld = false;

        _heldHDir = 0;
        _wasHDirHeld = false;

        SyncMouseSensitivityUI();

        // ✅ NO apagamos IsPausedGlobal acá (se apaga cuando el UI realmente se desactiva)
    }

    void ForceClosePause_NoSfx()
    {
        _isPaused = false;
        Time.timeScale = (_prevTimeScale <= 0.0001f) ? 1f : _prevTimeScale;

        ApplyPausedMusic(false, immediate: true);

        _uiAlpha = 0f;
        _uiAlphaTarget = 0f;

        _heldDir = 0;
        _wasDirHeld = false;

        _heldHDir = 0;
        _wasHDirHeld = false;

        if (pauseRoot != null) pauseRoot.SetActive(false);
        RestoreOriginalTransforms();

        if (showSystemCursorWhenPaused)
        {
            Cursor.lockState = _prevLockMode;
            Cursor.visible = _prevCursorVisible;
        }

        SyncMouseSensitivityUI();

        if (_activePauseMenu == this)
        {
            IsPausedGlobal = false;
            _activePauseMenu = null;
        }

        _lockPausedUntilSceneChange = false;
    }

    void SetGameplayEnabled(bool enabled)
    {
        if (disableWhilePaused == null) return;
        for (int i = 0; i < disableWhilePaused.Length; i++)
            if (disableWhilePaused[i] != null) disableWhilePaused[i].enabled = enabled;
    }

    // ✅ Llamala desde tu GameOver/Death
    public void SetPlayerDead(bool dead)
    {
        isPlayerDead = dead;
        IsPlayerDeadGlobal = dead;

        if (dead)
        {
            if (forceClosePauseIfDead && _isPaused)
                ForceClosePause_NoSfx();
        }
    }

    // ========================= NAV =========================

    void HandleNavigation()
    {
        if (Input.GetKeyDown(acceptKey1) || Input.GetKeyDown(acceptKey2) || Input.GetButtonDown("Submit"))
        {
            _nextToggleAllowedTime = Time.unscaledTime + toggleDebounceSeconds;
            ActivateCurrent();
            return;
        }

        float now = Time.unscaledTime;

        int keyHeldDir = GetKeyboardHeldDir();
        int keyDownDir = GetKeyboardDownDir();

        if (keyDownDir != 0)
        {
            MoveWrap(keyDownDir);
            _heldDir = keyDownDir;
            _wasDirHeld = true;
            _nextRepeatTime = now + navInitialDelay;
            return;
        }

        if (keyHeldDir != 0)
        {
            if (!_wasDirHeld || _heldDir != keyHeldDir)
            {
                _heldDir = keyHeldDir;
                _wasDirHeld = true;
                _nextRepeatTime = now + navInitialDelay;
                MoveWrap(keyHeldDir);
                return;
            }

            if (now >= _nextRepeatTime)
            {
                _nextRepeatTime = now + navRepeatRate;
                MoveWrap(_heldDir);
                return;
            }

            return;
        }

        if (_wasDirHeld && _heldDir != 0)
        {
            _heldDir = 0;
            _wasDirHeld = false;
        }

        if (!useVerticalAxisForGamepad) return;

        float v = Input.GetAxisRaw("Vertical");
        int dirAxis = 0;
        if (v > 0.55f) dirAxis = -1;
        else if (v < -0.55f) dirAxis = +1;
        if (dirAxis == 0) return;

        if (!_wasDirHeld || _heldDir != dirAxis)
        {
            _heldDir = dirAxis;
            _wasDirHeld = true;
            _nextRepeatTime = now + navInitialDelay;
            MoveWrap(dirAxis);
            return;
        }

        if (now >= _nextRepeatTime)
        {
            _nextRepeatTime = now + navRepeatRate;
            MoveWrap(dirAxis);
        }
    }

    int GetKeyboardHeldDir()
    {
        bool up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        bool down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        if (up == down) return 0;
        return up ? -1 : +1;
    }

    int GetKeyboardDownDir()
    {
        bool upDown = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool downDown = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        if (upDown == downDown) return 0;
        return upDown ? -1 : +1;
    }

    void MoveWrap(int dir)
    {
        if (optionRects == null || optionRects.Length == 0) return;
        int n = optionRects.Length;

        _index = (_index + dir) % n;
        if (_index < 0) _index += n;

        _wheelTarget += dir;

        if (sfxNavigate != null) PlaySfx(sfxNavigate);

        SyncMouseSensitivityUI();
    }

    void ActivateCurrent()
    {
        // ✅ Si estás en Mouse Sensibility y apretás Enter: salir del pause
        if (mouseSensitivityIndex >= 0 && _index == mouseSensitivityIndex)
        {
            if (sfxSelect != null) PlaySfx(sfxSelect);
            Resume(false);
            return;
        }

        if (sfxSelect != null) PlaySfx(sfxSelect);

        // Orden esperado: 0 Resume, 1 Restart, 2 MouseSens, 3 MainMenu
        switch (_index)
        {
            case 0:
                Resume(false);
                break;

            case 1:
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;

            case 3:
                // ✅ NEW: mantener crosshair OFF durante transición a MainMenu
                _lockPausedUntilSceneChange = true;
                _activePauseMenu = this;
                IsPausedGlobal = true;

                StartCoroutine(CoToMainMenu_FadeBlackThenLoad());
                break;
        }
    }

    IEnumerator CoToMainMenu_FadeBlackThenLoad()
    {
        if (_transitioning) yield break;
        _transitioning = true;

        _uiAlphaTarget = 0f;
        if (pauseRoot) pauseRoot.SetActive(false);

        EnsureLocalFade();
        _fadeImg.transform.SetAsLastSibling();
        Time.timeScale = 0f;

        float dur = Mathf.Max(0.01f, fadeToMenuSeconds);
        float t = 0f;
        float startA = _fadeImg.color.a;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            SetFadeAlpha(Mathf.Lerp(startA, 1f, t));
            yield return null;
        }
        SetFadeAlpha(1f);

        AudioListener.pause = false;
        Time.timeScale = 1f;

        // ✅ NEW: NO limpiar IsPausedGlobal acá (si lo limpiás, puede reaparecer el crosshair durante el fade)
        // El reset se hace en Awake al entrar a la próxima escena.

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ========================= TAMBOR =========================

    void ApplyDrumLayout(float dtUnscaled)
    {
        if (optionRects == null || optionRects.Length == 0) return;

        float k = 1f - Mathf.Exp(-Mathf.Max(1f, wheelSmooth) * dtUnscaled);
        _wheel = Mathf.Lerp(_wheel, _wheelTarget, k);

        int n = optionRects.Length;
        int best = -1;
        float bestDepth = -999f;

        for (int i = 0; i < n; i++)
        {
            RectTransform rt = optionRects[i];
            if (rt == null) continue;

            float delta = WrapDelta(i - _wheel, n);
            float absDelta = Mathf.Abs(delta);

            float visK = 1f;
            if (limitVisibleOptions)
            {
                float start = Mathf.Max(0f, visibleNeighbours + 0.05f);
                float end = Mathf.Max(start + 0.01f, visibleNeighbours + hideFadeBuffer);
                if (absDelta <= start) visK = 1f;
                else if (absDelta >= end) visK = 0f;
                else visK = 1f - Mathf.InverseLerp(start, end, absDelta);
                visK = Mathf.Clamp01(visK);
            }

            float ang = delta * stepAngleDegrees * Mathf.Deg2Rad;
            float depth01 = (Mathf.Cos(ang) + 1f) * 0.5f;

            float x = Mathf.Sin(ang) * radiusX;
            float y = -Mathf.Sin(ang) * radiusY;

            rt.anchoredPosition = _centerPos + new Vector2(x, y);

            float s = Mathf.Lerp(backScale, frontScale, depth01);
            float centerBoost = Mathf.SmoothStep(0f, 1f, depth01);
            s *= (1f + centerPop * centerBoost);
            s *= Mathf.Lerp(0.001f, 1f, visK);

            rt.localScale = Vector3.one * s;

            float tilt = -Mathf.Sin(ang) * sideTiltDegrees;
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

            if (optionGraphics != null && _optBaseColors != null && i < optionGraphics.Length && optionGraphics[i] != null)
            {
                float aLocal = Mathf.Lerp(backAlpha, frontAlpha, depth01);
                float aFinal = aLocal * Mathf.Clamp01(_uiAlpha) * visK;

                float backness = 1f - depth01;
                float tintK = Mathf.Clamp01(backness * backTintStrength);

                Color baseC = _optBaseColors[i];
                Color rgb = Color.Lerp(baseC, new Color(backTint.r, backTint.g, backTint.b, baseC.a), tintK);
                rgb.a = baseC.a * aFinal;

                optionGraphics[i].color = rgb;
            }

            if (depth01 > bestDepth)
            {
                bestDepth = depth01;
                best = i;
            }
        }

        if (best >= 0 && optionRects[best] != null)
            optionRects[best].SetAsLastSibling();
    }

    static float WrapDelta(float d, int n)
    {
        float half = n * 0.5f;
        while (d > half) d -= n;
        while (d < -half) d += n;
        return d;
    }

    void ResolveCenter()
    {
        if (drumCenter != null)
        {
            _centerPos = drumCenter.anchoredPosition;
            return;
        }

        if (optionRects == null || optionRects.Length == 0)
        {
            _centerPos = Vector2.zero;
            return;
        }

        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < optionRects.Length; i++)
        {
            if (optionRects[i] == null) continue;
            sum += optionRects[i].anchoredPosition;
            count++;
        }

        _centerPos = (count > 0) ? (sum / count) : Vector2.zero;
    }

    void UpdateMenuFade(float dtUnscaled)
    {
        if (menuFadeSeconds <= 0f)
            _uiAlpha = _uiAlphaTarget;
        else
            _uiAlpha = Mathf.Lerp(_uiAlpha, _uiAlphaTarget, 1f - Mathf.Exp(-12f * dtUnscaled / menuFadeSeconds));
    }

    void ApplyMenuExtrasAlpha()
    {
        float a = Mathf.Clamp01(_uiAlpha);

        if (extraGraphics != null && _extraBaseColors != null)
        {
            for (int i = 0; i < extraGraphics.Length; i++)
            {
                var g = extraGraphics[i];
                if (g == null) continue;

                Color baseC = _extraBaseColors[i];
                baseC.a = _extraBaseColors[i].a * a;
                g.color = baseC;
            }
        }
    }

    // ========================= SENSIBILIDAD =========================

    void InitMouseSensitivity()
    {
        if (mouseSensitivitySlider == null) return;

        sensitivityMin = Mathf.Max(0.01f, sensitivityMin);
        sensitivityMax = Mathf.Max(sensitivityMin, sensitivityMax);
        sensitivityDefault = Mathf.Clamp(sensitivityDefault, sensitivityMin, sensitivityMax);

        mouseSensitivitySlider.minValue = sensitivityMin;
        mouseSensitivitySlider.maxValue = sensitivityMax;
        mouseSensitivitySlider.wholeNumbers = false;

        if (!_sensInitializedThisSession)
        {
            PlayerPrefs.SetFloat(mouseSensitivityPrefKey, sensitivityDefault);
            _sensInitializedThisSession = true;
        }

        float v = PlayerPrefs.GetFloat(mouseSensitivityPrefKey, sensitivityDefault);
        v = Mathf.Clamp(v, sensitivityMin, sensitivityMax);
        v = Mathf.Round(v * 100f) / 100f;

        _ignoreSensCallback = true;
        mouseSensitivitySlider.value = v;
        _ignoreSensCallback = false;

        UpdateMouseSensitivityLabel(v);
        ApplyMouseSensitivityToTarget(v);

        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderChanged);
    }

    void OnMouseSensitivitySliderChanged(float v)
    {
        if (_ignoreSensCallback) return;

        v = Mathf.Clamp(v, sensitivityMin, sensitivityMax);
        v = Mathf.Round(v * 100f) / 100f;

        PlayerPrefs.SetFloat(mouseSensitivityPrefKey, v);
        UpdateMouseSensitivityLabel(v);
        ApplyMouseSensitivityToTarget(v);
    }

    void SyncMouseSensitivityUI()
    {
        bool show = _isPaused && mouseSensitivityIndex >= 0 && _index == mouseSensitivityIndex;

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.gameObject.SetActive(show);

        if (mouseSensitivityValueLabel != null)
            mouseSensitivityValueLabel.gameObject.SetActive(show);
    }

    float GetAccelStep(float now)
    {
        float baseStep = Mathf.Max(0.001f, sensitivityStep);
        if (!accelerateOnHold) return baseStep;

        float held = Mathf.Max(0f, now - _hHoldStartTime);

        float step = baseStep;
        if (held >= Mathf.Max(0f, holdStep1After)) step = Mathf.Max(step, holdStep1);
        if (held >= Mathf.Max(0f, holdStep2After)) step = Mathf.Max(step, holdStep2);
        if (held >= Mathf.Max(0f, holdStep3After)) step = Mathf.Max(step, holdStep3);

        return step;
    }

    void StepMouseSensitivity(int dir, float now, bool playSfx)
    {
        float step = GetAccelStep(now);

        float v = mouseSensitivitySlider.value;
        v += (step * dir);
        v = Mathf.Clamp(v, sensitivityMin, sensitivityMax);
        v = Mathf.Round(v * 100f) / 100f;

        _ignoreSensCallback = true;
        mouseSensitivitySlider.value = v;
        _ignoreSensCallback = false;

        PlayerPrefs.SetFloat(mouseSensitivityPrefKey, v);
        UpdateMouseSensitivityLabel(v);
        ApplyMouseSensitivityToTarget(v);

        if (playSfx && sfxSlider != null)
            PlaySfx(sfxSlider);
    }

    void HandleMouseSensitivityAdjust()
    {
        if (mouseSensitivityIndex < 0) return;
        if (_index != mouseSensitivityIndex) return;
        if (mouseSensitivitySlider == null) return;

        float now = Time.unscaledTime;

        int downDir = GetHorizontalDownDir();
        int heldDir = GetHorizontalHeldDir();

        if (downDir != 0)
        {
            _hHoldStartTime = now;
            StepMouseSensitivity(downDir, now, playSfx: true);
            _heldHDir = downDir;
            _wasHDirHeld = true;
            _nextHRepeatTime = now + navInitialDelay;
            return;
        }

        if (heldDir != 0)
        {
            if (!_wasHDirHeld || _heldHDir != heldDir)
            {
                _hHoldStartTime = now;
                _heldHDir = heldDir;
                _wasHDirHeld = true;
                _nextHRepeatTime = now + navInitialDelay;
                StepMouseSensitivity(heldDir, now, playSfx: true);
                return;
            }

            if (now >= _nextHRepeatTime)
            {
                _nextHRepeatTime = now + navRepeatRate;
                StepMouseSensitivity(_heldHDir, now, playSfx: true);
                return;
            }

            return;
        }

        if (_wasHDirHeld && _heldHDir != 0)
        {
            _heldHDir = 0;
            _wasHDirHeld = false;
        }
    }

    int GetHorizontalHeldDir()
    {
        bool left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        if (left == right) return 0;
        return right ? +1 : -1;
    }

    int GetHorizontalDownDir()
    {
        bool leftDown = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool rightDown = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        if (leftDown == rightDown) return 0;
        return rightDown ? +1 : -1;
    }

    void UpdateMouseSensitivityLabel(float v)
    {
        if (mouseSensitivityValueLabel == null) return;

        string raw = "x " + v.ToString("0.00", CultureInfo.InvariantCulture);
        string finalText = useMonospaceTag ? $"<mspace={valueLabelMonospaceEm}em>{raw}</mspace>" : raw;

        TMP_Text tmp = null;
        if (mouseSensitivityValueLabel is TMP_Text asTmp) tmp = asTmp;
        if (tmp == null) tmp = mouseSensitivityValueLabel.GetComponent<TMP_Text>();

        if (tmp != null)
        {
            if (forceTmpNoWrap)
            {
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
            }

            if (forceTmpDisableAutoSize)
                tmp.enableAutoSizing = false;

            if (forceTmpLeftAlignment)
                tmp.alignment = TextAlignmentOptions.MidlineLeft;

            tmp.richText = true;
            tmp.text = finalText;
        }
    }

    void ApplyMouseSensitivityToTarget(float multiplier)
    {
        if (mouseSensitivityTarget == null) return;
        if (string.IsNullOrEmpty(mouseSensitivityTargetMethod)) return;

        mouseSensitivityTarget.SendMessage(mouseSensitivityTargetMethod, multiplier, SendMessageOptions.DontRequireReceiver);
    }

    // ========================= MUSIC SAFE =========================

    void CacheMusicOriginal()
    {
        if (musicMixer != null && !string.IsNullOrEmpty(musicVolumeParam))
        {
            if (musicMixer.GetFloat(musicVolumeParam, out _musicDbOriginal))
                _hasMixerValue = true;
        }

        if (musicSource != null)
            _musicSrcOriginal = musicSource.volume;
    }

    void ApplyPausedMusic(bool paused, bool immediate = false)
    {
        bool canCoroutine = isActiveAndEnabled && gameObject.activeInHierarchy && !immediate;

        if (musicMixer != null && _hasMixerValue && !string.IsNullOrEmpty(musicVolumeParam))
        {
            float target = paused ? (_musicDbOriginal + pausedMusicOffsetDb) : _musicDbOriginal;

            if (!canCoroutine || musicFadeSeconds <= 0.0001f)
            {
                musicMixer.SetFloat(musicVolumeParam, target);
                return;
            }

            if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = StartCoroutine(FadeMixerParam(musicVolumeParam, target, musicFadeSeconds));
            return;
        }

        if (musicSource != null)
        {
            float target = paused ? (_musicSrcOriginal * pausedMusicMultiplier) : _musicSrcOriginal;

            if (!canCoroutine || musicFadeSeconds <= 0.0001f)
            {
                musicSource.volume = target;
                return;
            }

            if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = StartCoroutine(FadeMusicSource(target, musicFadeSeconds));
        }
    }

    IEnumerator FadeMixerParam(string param, float targetDb, float seconds)
    {
        if (musicMixer == null) yield break;

        float startDb;
        if (!musicMixer.GetFloat(param, out startDb))
            startDb = targetDb;

        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            musicMixer.SetFloat(param, Mathf.Lerp(startDb, targetDb, t));
            yield return null;
        }
        musicMixer.SetFloat(param, targetDb);
    }

    IEnumerator FadeMusicSource(float targetVol, float seconds)
    {
        if (musicSource == null) yield break;

        float start = musicSource.volume;
        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            musicSource.volume = Mathf.Lerp(start, targetVol, t);
            yield return null;
        }
        musicSource.volume = targetVol;
    }

    // ========================= SFX =========================

    void SetupPersistentSfx()
    {
        if (_persistSfxSource != null) return;

        _persistSfxGO = new GameObject("PauseMenu_SFX");
        DontDestroyOnLoad(_persistSfxGO);

        _persistSfxSource = _persistSfxGO.AddComponent<AudioSource>();
        _persistSfxSource.playOnAwake = false;
        _persistSfxSource.loop = false;
        _persistSfxSource.spatialBlend = 0f;
        _persistSfxSource.ignoreListenerPause = true;

        if (sfxMixerGroup != null)
            _persistSfxSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        SetupPersistentSfx();

        if (sfxMixerGroup != null && _persistSfxSource.outputAudioMixerGroup != sfxMixerGroup)
            _persistSfxSource.outputAudioMixerGroup = sfxMixerGroup;

        _persistSfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ========================= FADE LOCAL =========================

    void EnsureLocalFade()
    {
        if (_fadeImg != null) return;

        Canvas canvas = (pauseRoot != null) ? pauseRoot.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
        if (!canvas) return;

        GameObject go = new GameObject("PauseMenu_FadeBlack");
        go.transform.SetParent(canvas.transform, false);

        _fadeImg = go.AddComponent<Image>();
        _fadeImg.raycastTarget = false;
        _fadeImg.sprite = GetWhiteSprite();
        _fadeImg.type = Image.Type.Simple;

        RectTransform rt = _fadeImg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        SetFadeAlpha(0f);
        go.SetActive(true);
    }

    void SetFadeAlpha(float a)
    {
        if (_fadeImg == null) return;
        Color c = _fadeImg.color;
        c.r = 0f; c.g = 0f; c.b = 0f;
        c.a = Mathf.Clamp01(a);
        _fadeImg.color = c;
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    // ========================= TRANSFORMS =========================

    void CacheOriginalTransforms()
    {
        if (_origCached || optionRects == null) return;

        int n = optionRects.Length;
        _origScale = new Vector3[n];
        _origPos = new Vector2[n];
        _origRot = new Quaternion[n];

        for (int i = 0; i < n; i++)
        {
            if (!optionRects[i]) continue;
            _origScale[i] = optionRects[i].localScale;
            _origPos[i] = optionRects[i].anchoredPosition;
            _origRot[i] = optionRects[i].localRotation;
        }

        _origCached = true;
    }

    void RestoreOriginalTransforms()
    {
        if (!_origCached || optionRects == null) return;

        for (int i = 0; i < optionRects.Length; i++)
        {
            if (!optionRects[i]) continue;
            optionRects[i].localScale = _origScale[i];
            optionRects[i].anchoredPosition = _origPos[i];
            optionRects[i].localRotation = _origRot[i];

            if (optionGraphics != null && _optBaseColors != null && i < optionGraphics.Length && optionGraphics[i])
                optionGraphics[i].color = _optBaseColors[i];
        }
    }
}
