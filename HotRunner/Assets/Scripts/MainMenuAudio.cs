using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MainMenuAudio : MonoBehaviour
{
    [Header("Mixer Groups (recomendado)")]
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    [Header("Bloquear mouse (solo teclado/WASD)")]
    public bool bloquearMouseUI = true;
    public bool ocultarCursor = true;

    [Header("Selección inicial (obligatorio para navegación + move)")]
    public GameObject firstSelected;

    [Header("BGM MainMenu")]
    public AudioClip bgmMainMenu;
    [Range(0f, 1f)] public float bgmVol = 0.8f;
    public bool bgmPlayOnStart = true;

    [Header("Ambient Loop (extra)")]
    public AudioClip ambientLoop;
    [Range(0f, 1f)] public float ambientVol = 0.55f;
    public bool ambientPlayOnStart = true;

    [Header("Move / Select UI")]
    public AudioClip sfxMoveOption;
    public AudioClip sfxSelectOption;
    public float moveCooldown = 0.06f;

    [Header("Slice (Sliders)")]
    public AudioClip sfxSlice;
    public float sliceCooldown = 0.06f;

    [Header("Back SFX")]
    public AudioClip sfxBack;

    [Header("Exit / Init (one-shot que debe terminar)")]
    public AudioClip sfxExitTerminal;
    public AudioClip sfxInitSession;
    public string loadingSceneName = "LoadingScene";

    [Header("Logs Typing Loop (mientras tipea)")]
    public bool usarTypingLoop = true;
    public TextMeshProUGUI logsTMP;
    public AudioClip sfxTypingLoop;
    [Range(0f, 1f)] public float typingVol = 0.65f;
    public float typingIdleStopDelay = 0.08f;

    [Header("Laptop Focus (Raycast)")]
    public bool usarDetectorLaptop = true;
    public Camera camMenu;
    public LayerMask laptopMask;
    public float laptopMaxDist = 6f;

    [Header("Laptop Boot (one-shot)")]
    public AudioClip sfxLaptopBoot;
    public bool bootSoloUnaVez = true;

    [Header("Focus Laptop LOOP + Fade")]
    public AudioClip sfxFocusLaptopLoop;
    [Range(0f, 1f)] public float focusLoopVol = 0.85f;
    public float focusFadeIn = 0.08f;
    public float focusFadeOut = 0.10f;

    [Header("Auto-wire (no asignar GO)")]
    public bool autoWireUI = true;

    [Tooltip("Si está ON, EXIT/INIT reemplazan el onClick para poder esperar el audio antes de salir/cargar.")]
    public bool overrideExitInitOnClick = true;

    [Tooltip("Tokens para detectar botones por texto/nombre.")]
    public string[] tokSysConfig = { "sys", "config" };
    public string[] tokLogs = { "logs" };
    public string[] tokBack = { "back" };
    public string[] tokExit = { "exit" };
    public string[] tokInit = { "init", "session" };

    // --- Sources ---
    AudioSource _bgm;
    AudioSource _amb;
    AudioSource _ui;
    AudioSource _focus;
    AudioSource _typing;

    // --- State ---
    float _lastMoveTime;
    float _lastSliceTime;

    Coroutine _coFocusFade;
    bool _focusOn;
    bool _bootYaSono;

    GameObject _lastSelected;
    bool _skipFirstMoveSound = true;
    bool _suppressNextMove = false;

    Slider _lastSlider;
    float _lastSliderValue;

    string _lastLogsText = "";
    float _lastLogsChangeTime;

    bool _wired = false;

    void Awake()
    {
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.loop = true; _bgm.playOnAwake = false; _bgm.volume = bgmVol; _bgm.spatialBlend = 0f;
        if (bgmGroup) _bgm.outputAudioMixerGroup = bgmGroup;

        _amb = gameObject.AddComponent<AudioSource>();
        _amb.loop = true; _amb.playOnAwake = false; _amb.volume = ambientVol; _amb.spatialBlend = 0f;
        if (bgmGroup) _amb.outputAudioMixerGroup = bgmGroup;

        _ui = gameObject.AddComponent<AudioSource>();
        _ui.loop = false; _ui.playOnAwake = false; _ui.spatialBlend = 0f;
        if (sfxGroup) _ui.outputAudioMixerGroup = sfxGroup;

        _focus = gameObject.AddComponent<AudioSource>();
        _focus.loop = true; _focus.playOnAwake = false; _focus.spatialBlend = 0f; _focus.volume = 0f;
        if (sfxGroup) _focus.outputAudioMixerGroup = sfxGroup;

        _typing = gameObject.AddComponent<AudioSource>();
        _typing.loop = true; _typing.playOnAwake = false; _typing.spatialBlend = 0f; _typing.volume = typingVol;
        if (sfxGroup) _typing.outputAudioMixerGroup = sfxGroup;

        if (!camMenu) camMenu = Camera.main;
    }

    void Start()
    {
        if (ocultarCursor) Cursor.visible = false;
        if (bloquearMouseUI) BlockMouseUI();

        StartCoroutine(CoSelectFirst());

        if (bgmPlayOnStart && bgmMainMenu) PlayBGM();
        if (ambientPlayOnStart && ambientLoop) PlayAmbient();

        if (autoWireUI)
            StartCoroutine(CoAutoWireNextFrame());
    }

    IEnumerator CoSelectFirst()
    {
        yield return null;
        if (!EventSystem.current || !firstSelected) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelected);

        _lastSelected = firstSelected;
        _skipFirstMoveSound = true;
        CacheSliderIfAny(_lastSelected);
    }

    IEnumerator CoAutoWireNextFrame()
    {
        yield return null;
        AutoWireUIOnce();
    }

    void Update()
    {
        if (usarDetectorLaptop) HandleLaptopFocus();

        HandleMoveSoundBySelection();
        HandleSliderSlice();
        HandleCancelBackSound();
        HandleTypingLoop();
    }

    // ---------------- Auto-wire UI ----------------
    void AutoWireUIOnce()
    {
        if (_wired) return;
        _wired = true;

        // Botones (incluye inactivos)
        var buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (!b) continue;

            string label = GetUILabelOrName(b.gameObject);

            // EXIT: reemplazar onClick para poder esperar audio antes de salir
            if (MatchesTokens(label, tokExit))
            {
                if (overrideExitInitOnClick)
                    b.onClick.RemoveAllListeners();

                b.onClick.AddListener(() => ExitTerminal());
                continue;
            }

            // INIT: reemplazar onClick para esperar audio antes de cargar
            if (MatchesTokens(label, tokInit))
            {
                if (overrideExitInitOnClick)
                    b.onClick.RemoveAllListeners();

                b.onClick.AddListener(() => InitSessionAndLoad());
                continue;
            }

            // BACK: siempre su propio sonido (nunca Select)
            if (MatchesTokens(label, tokBack))
            {
                b.onClick.AddListener(() => PlayBackSFX());
                continue;
            }

            // SYS CONFIG / LOGS: suena Select
            if (MatchesTokens(label, tokSysConfig) || MatchesTokens(label, tokLogs))
            {
                b.onClick.AddListener(() => PlaySelectSFX());
                continue;
            }
        }

        // Sliders: slice por OnValueChanged (más fiable que comparar valores)
        var sliders = FindObjectsOfType<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            var s = sliders[i];
            if (!s) continue;

            // evitamos duplicar listeners si el objeto se reinicia en play
            s.onValueChanged.AddListener(_ => PlaySliceSFX());
        }
    }

    string GetUILabelOrName(GameObject go)
    {
        // Primero intentamos texto visible del botón
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp && !string.IsNullOrEmpty(tmp.text))
            return tmp.text.Trim().ToLowerInvariant();

        var txt = go.GetComponentInChildren<Text>(true);
        if (txt && !string.IsNullOrEmpty(txt.text))
            return txt.text.Trim().ToLowerInvariant();

        // fallback: nombre del GO
        return go.name.Trim().ToLowerInvariant();
    }

    bool MatchesTokens(string haystackLower, string[] tokens)
    {
        if (string.IsNullOrEmpty(haystackLower)) return false;
        if (tokens == null || tokens.Length == 0) return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i];
            if (string.IsNullOrEmpty(t)) continue;
            t = t.ToLowerInvariant();
            if (!haystackLower.Contains(t))
                return false;
        }
        return true;
    }

    // ---------------- Mouse block ----------------
    void BlockMouseUI()
    {
        var gr = FindObjectsOfType<GraphicRaycaster>(true);
        for (int i = 0; i < gr.Length; i++) gr[i].enabled = false;

        var allBehaviours = FindObjectsOfType<Behaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (!allBehaviours[i]) continue;
            string n = allBehaviours[i].GetType().Name;
            if (n == "PhysicsRaycaster" || n == "Physics2DRaycaster")
                allBehaviours[i].enabled = false;
        }
    }

    // ---------------- Move sound (solo navegación) ----------------
    void HandleMoveSoundBySelection()
    {
        if (!sfxMoveOption) return;
        if (!EventSystem.current) return;

        var cur = EventSystem.current.currentSelectedGameObject;
        if (!cur) return;

        if (cur != _lastSelected)
        {
            _lastSelected = cur;
            CacheSliderIfAny(_lastSelected);

            if (_skipFirstMoveSound)
            {
                _skipFirstMoveSound = false;
                return;
            }

            if (_suppressNextMove)
            {
                _suppressNextMove = false;
                return;
            }

            if (Time.unscaledTime - _lastMoveTime >= moveCooldown)
            {
                _lastMoveTime = Time.unscaledTime;
                _ui.PlayOneShot(sfxMoveOption);
            }
        }
    }

    // ---------------- Slider slice (fallback por comparación) ----------------
    void CacheSliderIfAny(GameObject go)
    {
        _lastSlider = null;
        if (!go) return;

        _lastSlider = go.GetComponent<Slider>();
        if (_lastSlider) _lastSliderValue = _lastSlider.value;
    }

    void HandleSliderSlice()
    {
        // Si ya estás usando auto-wire por onValueChanged, esto igual no molesta.
        if (!sfxSlice) return;
        if (!_lastSlider) return;

        float v = _lastSlider.value;
        if (Mathf.Abs(v - _lastSliderValue) > 0.0001f)
        {
            _lastSliderValue = v;

            if (Time.unscaledTime - _lastSliceTime >= sliceCooldown)
            {
                _lastSliceTime = Time.unscaledTime;
                _ui.PlayOneShot(sfxSlice);
            }
        }
    }

    // ---------------- Cancel -> Back sound ----------------
    void HandleCancelBackSound()
    {
        if (!sfxBack) return;

        if (IsCancelDown())
        {
            PlayBackSFX();
            _suppressNextMove = true;
        }
    }

    bool IsCancelDown()
    {
        bool b = false;
        try { b = Input.GetButtonDown("Cancel"); } catch { }

        if (Input.GetKeyDown(KeyCode.Escape)) b = true;
        if (Input.GetKeyDown(KeyCode.Backspace)) b = true;
        if (Input.GetKeyDown(KeyCode.JoystickButton1)) b = true;

        return b;
    }

    // ---------------- SFX helpers ----------------
    public void PlaySelectSFX()
    {
        if (!sfxSelectOption) return;
        _ui.PlayOneShot(sfxSelectOption);
        _suppressNextMove = true;
    }

    public void PlayBackSFX()
    {
        if (!sfxBack) return;
        _ui.PlayOneShot(sfxBack);
        _suppressNextMove = true;
    }

    public void PlaySliceSFX()
    {
        if (!sfxSlice) return;

        if (Time.unscaledTime - _lastSliceTime >= sliceCooldown)
        {
            _lastSliceTime = Time.unscaledTime;
            _ui.PlayOneShot(sfxSlice);
        }
    }

    // ---------------- Logs typing loop ----------------
    void HandleTypingLoop()
    {
        if (!usarTypingLoop) return;
        if (!logsTMP) return;
        if (!sfxTypingLoop) return;

        float now = Time.unscaledTime;

        if (logsTMP.text != _lastLogsText)
        {
            _lastLogsText = logsTMP.text;
            _lastLogsChangeTime = now;

            if (!_typing.isPlaying)
            {
                _typing.clip = sfxTypingLoop;
                _typing.volume = typingVol;
                _typing.Play();
            }
        }

        if (_typing.isPlaying && (now - _lastLogsChangeTime) > typingIdleStopDelay)
        {
            _typing.Stop();
        }
    }

    // ---------------- Laptop Focus ----------------
    void HandleLaptopFocus()
    {
        if (!camMenu) return;

        Ray ray = camMenu.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hitLaptop = Physics.Raycast(ray, laptopMaxDist, laptopMask, QueryTriggerInteraction.Ignore);

        if (hitLaptop != _focusOn)
        {
            _focusOn = hitLaptop;

            if (_focusOn)
                PlayLaptopBoot();

            SetFocusLaptopLoop(_focusOn);
        }
    }

    public void PlayLaptopBoot()
    {
        if (!sfxLaptopBoot) return;
        if (bootSoloUnaVez && _bootYaSono) return;
        _bootYaSono = true;
        _ui.PlayOneShot(sfxLaptopBoot);
    }

    public void SetFocusLaptopLoop(bool on)
    {
        if (_coFocusFade != null) StopCoroutine(_coFocusFade);
        _coFocusFade = StartCoroutine(CoFadeFocus(on));
    }

    IEnumerator CoFadeFocus(bool turnOn)
    {
        if (turnOn)
        {
            if (!sfxFocusLaptopLoop) yield break;

            if (_focus.clip != sfxFocusLaptopLoop)
                _focus.clip = sfxFocusLaptopLoop;

            if (!_focus.isPlaying)
                _focus.Play();

            float t = 0f;
            float dur = Mathf.Max(0.01f, focusFadeIn);
            float start = _focus.volume;
            float end = focusLoopVol;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                _focus.volume = Mathf.Lerp(start, end, t);
                yield return null;
            }
            _focus.volume = end;
        }
        else
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, focusFadeOut);
            float start = _focus.volume;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                _focus.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }

            _focus.volume = 0f;
            if (_focus.isPlaying) _focus.Stop();
        }
    }

    // ---------------- BGM / Ambient ----------------
    public void PlayBGM()
    {
        if (!bgmMainMenu) return;
        _bgm.clip = bgmMainMenu;
        _bgm.volume = bgmVol;
        if (!_bgm.isPlaying) _bgm.Play();
    }

    public void PlayAmbient()
    {
        if (!ambientLoop) return;
        _amb.clip = ambientLoop;
        _amb.volume = ambientVol;
        if (!_amb.isPlaying) _amb.Play();
    }

    public void StopBGM() { if (_bgm.isPlaying) _bgm.Stop(); }
    public void StopAmbient() { if (_amb.isPlaying) _amb.Stop(); }

    // ---------------- Exit / Init wait-full ----------------
    public void ExitTerminal()
    {
        StartCoroutine(CoPlayThenQuit(sfxExitTerminal));
    }

    public void InitSessionAndLoad()
    {
        StartCoroutine(CoPlayThenLoadScene(sfxInitSession, loadingSceneName));
    }

    IEnumerator CoPlayThenQuit(AudioClip clip)
    {
        StopBGM();
        StopAmbient();
        SetFocusLaptopLoop(false);
        if (_typing.isPlaying) _typing.Stop();

        if (!clip)
        {
            Application.Quit();
            yield break;
        }

        yield return PlayPersistentOneShot(clip, sfxGroup, 1f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator CoPlayThenLoadScene(AudioClip clip, string sceneName)
    {
        StopBGM();
        StopAmbient();
        SetFocusLaptopLoop(false);
        if (_typing.isPlaying) _typing.Stop();

        if (!clip)
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        yield return PlayPersistentOneShot(clip, sfxGroup, 1f);
        SceneManager.LoadScene(sceneName);
    }

    static IEnumerator PlayPersistentOneShot(AudioClip clip, AudioMixerGroup group, float volume)
    {
        GameObject go = new GameObject("OneShot_Persist");
        DontDestroyOnLoad(go);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 0f;
        if (group) src.outputAudioMixerGroup = group;

        src.Play();
        yield return new WaitForSecondsRealtime(clip.length);
        Destroy(go);
    }
}