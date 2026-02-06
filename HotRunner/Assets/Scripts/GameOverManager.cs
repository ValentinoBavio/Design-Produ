using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    // ✅ Flag global para que DeadzoneRespawn / GlitchTransition se corten en Game Over
    public static bool IsGameOverActive { get; private set; } = false;

    [Header("Fuente")]
    public ControladorJuego controladorJuego;

    [Header("HUD a ocultar al morir")]
    public GameObject[] hudObjectsToHide;

    [Header("Deshabilitar control del player")]
    public MonoBehaviour[] componentesAApagar;

    [Header("Cámara (efecto muerte)")]
    public Transform cameraRig; // Head
    public Camera cam;

    [Header("Clamp cámara contra suelo (anti clipping)")]
    public LayerMask sueloMask = ~0;
    public float distanciaRaySuelo = 6f;
    public float clampSphereRadius = 0.12f;
    public float alturaMinSobreSuelo = 0.15f;
    public float extraAntiClip = 0.05f;

    [Header("Decisión Aire vs Suelo (importante)")]
    [Tooltip("Si dist al piso > esto => AIRE.")]
    public float deathAirDistance = 0.55f;

    [Tooltip("Si dist al piso <= esto => podría ser SUELO (pero mirá el umbral de velY).")]
    public float deathGroundDistance = 0.08f;

    [Tooltip("Si |velY| > esto => SIEMPRE se considera AIRE (aunque esté cerca del piso).")]
    public float deathVelYThreshold = 0.25f;

    [Header("Impacto (AIRE)")]
    public float durImpactoSeg = 1f;
    public float impactBajarLocalY = 0.25f;
    public float impactPitchExtra = 25f;
    public float impactShakeExtra = 0.04f;
    public float impactFovExtra = 18f;

    [Header("Caída cámara (AIRE)")]
    public float durCaidaSeg = 2f;
    public float bajarLocalY = 0.85f;
    public float pitchHaciaAbajo = 70f;

    [Header("Shake")]
    public float shakeAmp = 0.02f;
    public float shakeFreq = 18f;

    [Header("FOV (AIRE)")]
    public float fovExtraCaida = 6f;

    [Header("Mini rebote (AIRE)")]
    public bool usarRebote = true;
    public float reboteSubeLocalY = 0.06f;
    public float reboteBajaLocalY = 0.02f;
    public float reboteDurSeg = 0.12f;

    [Header("Aire - Roll opcional (OFF)")]
    public bool aireUsarRoll = false;
    public float aireRollCaida = 0f;
    public float aireRollImpacto = 0f;

    [Header("Suelo - Caída de costado")]
    public float durSueloSeg = 1.5f;
    public float sueloBajarLocalY = 0.16f;
    public float sueloPitch = 5f;
    public float sueloRoll = 75f;
    public float sueloFovExtra = 3f;
    public float sueloShakeExtra = 0.1f;

    [Header("Mostrar caída extra (solo AIRE)")]
    [Tooltip("Tiempo extra para ver el cuerpo seguir cayendo (después de la animación) antes del fade.")]
    public float durExtraCaidaAire = 0.75f;

    [Header("Hold antes del fade (general)")]
    public float durHoldAntesFade = 0.35f;

    [Header("Fade a negro")]
    public Image panelNegro;
    public TextMeshProUGUI txtGameOver;
    public float fadeToBlackSeg = 3f;

    [Header("Press Any (NO debe verse hasta el final)")]
    public GameObject pressAnyRoot;
    public TextMeshProUGUI txtPressAny;
    public TMPPulseUnscaled pulsePressAny;

    [Header("Tiempo (slow motion antes del fade)")]
    public bool usarSlowMo = true;
    public float timeScaleDuranteEfecto = 0.25f;

    [Header("Bloqueo extra (anti look)")]
    public bool forzarBloqueoCamara = true;

    // =======================
    //  AUDIO
    // =======================
    [Header("Audio - Música Game Over")]
    public AudioSource levelMusicSource;
    public AudioClip gameOverMusicClip;
    public AudioSource gameOverMusicSource;
    [Range(0f, 1f)] public float gameOverTargetVolume = 0.3f;
    public float levelMusicFadeOutSeg = 3f;
    public float gameOverFadeInSeg = 5f;
    public UnityEngine.Audio.AudioMixerGroup musicMixerGroup;

    [Header("Audio - SFX Muerte (OneShot)")]
    public AudioClip sfxMuerteClip;
    [Range(0f, 1f)] public float sfxMuerteVolumen = 0.5f;
    public AudioSource sfxMuerteSource;
    public UnityEngine.Audio.AudioMixerGroup sfxMixerGroup;

    [Header("Audio - Golpe al tocar el suelo (solo si murió en el aire)")]
    public AudioClip sfxGolpeSuelo;
    [Range(0f, 1f)] public float sfxGolpeSueloVol = 0.9f;
    [Tooltip("Debe venir cayendo al menos así (más negativo = más fuerte) para sonar.")]
    public float golpeMinVelY = -6f;
    [Tooltip("Distancia para considerar 'tocó suelo' (spherecast hacia abajo).")]
    public float groundHitCheckDist = 0.12f;

    // =======================
    //  PLAYER (RB)
    // =======================
    [Header("Player (Rigidbody)")]
    public Transform playerRoot;
    public Rigidbody playerRigidbody;

    [Header("Aire - Caída MÁS rápida (REAL)")]
    public float extraGravityReal = 120f;
    public float minDownSpeedReal = 25f;
    public float maxDownSpeedReal = 110f;
    public bool quitarDerivaEnAire = true;

    [Header("Congelar")]
    [Tooltip("Congela SOLO si murió en suelo.")]
    public bool congelarEnMuerteSuelo = true;

    // =======================
    //  GLITCH OVERLAY (DontDestroyOnLoad)
    // =======================
    [Header("Respawn Glitch Overlay - Forzar OFF")]
    public GameObject glitchTransitionRoot;
    public GameObject glitchOverlay;
    public bool autoBuscarGlitchPorNombre = true;
    public string glitchOverlayName = "GlitchOverlay";
    public string glitchRootName = "GlitchTransition";
    public bool forzarOffGlitchCadaFrame = true;

    [Header("Debug")]
    public bool debug = false;

    // ------------------- internos -------------------
    Coroutine _co;
    Coroutine _coMusic;

    bool _gameOverActivo = false;
    bool _puedeReiniciar = false;
    bool _murioEnAire = false;

    bool _impactoSueloDisparado = false;

    Vector3 _rigLocalPos0;
    Quaternion _rigLocalRot0;
    float _fov0;

    Vector3 _lockLocalPos;
    Quaternion _lockLocalRot;
    bool _lockInicializado = false;

    float _fixedDelta0;
    float _levelMusicVol0 = 1f;
    bool _cachedLevelVol = false;

    // ✅ Ignorar colliders del player en los casts
    readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();
    readonly RaycastHit[] _hits = new RaycastHit[16];

    void Awake()
    {
        IsGameOverActive = false;
        _fixedDelta0 = Time.fixedDeltaTime;

        CachePlayerRefsIfNeeded();
        RefreshPlayerColliderCache();

        if (cameraRig)
        {
            _rigLocalPos0 = cameraRig.localPosition;
            _rigLocalRot0 = cameraRig.localRotation;
        }
        if (cam) _fov0 = cam.fieldOfView;

        SetAlphaImage(panelNegro, 0f);
        SetAlphaTMP(txtGameOver, 0f);
        ForceHidePressAny();

        EnsureGameOverMusicSource();
        CacheLevelMusicSourceIfNeeded();
        EnsureDeathSfxSource();
    }

    void Start()
    {
        ForceHidePressAny();
        CacheLevelMusicSourceIfNeeded();
    }

    void OnEnable()
    {
        if (controladorJuego != null)
            controladorJuego.OnTiempoAgotado += DispararGameOver;
    }

    void OnDisable()
    {
        if (controladorJuego != null)
            controladorJuego.OnTiempoAgotado -= DispararGameOver;
    }

    void LateUpdate()
    {
        if (_gameOverActivo && forzarBloqueoCamara && cameraRig && _lockInicializado)
        {
            cameraRig.localPosition = _lockLocalPos;
            cameraRig.localRotation = _lockLocalRot;
            ClampRigNow();
        }

        if (_gameOverActivo && forzarOffGlitchCadaFrame)
            ForceDisableGlitchOverlay();
    }

    void Update()
    {
        if (!_gameOverActivo)
        {
            ForceHidePressAny();
            return;
        }

        if (forzarOffGlitchCadaFrame)
            ForceDisableGlitchOverlay();

        // ✅ En aire: el cuerpo sigue cayendo hasta el final (se frena recién cuando ponemos timeScale=0)
        if (_murioEnAire && Time.timeScale > 0.001f)
        {
            ApplyExtraGravityAirEachFrame();
            TryPlayGroundImpactSfx();
        }

        if (!_puedeReiniciar) return;

        if (Input.anyKeyDown
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonDown(1)
            || Input.GetMouseButtonDown(2))
        {
            ReiniciarNivel();
        }
    }

    public void DispararGameOver()
    {
        if (_gameOverActivo) return;

        _gameOverActivo = true;
        IsGameOverActive = true;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RutinaGameOver());
    }

    IEnumerator RutinaGameOver()
    {
        CachePlayerRefsIfNeeded();
        RefreshPlayerColliderCache();

        _murioEnAire = DecideMurioEnAire();
        _impactoSueloDisparado = false;

        if (forzarOffGlitchCadaFrame)
            ForceDisableGlitchOverlay();

        // HUD off
        for (int i = 0; i < hudObjectsToHide.Length; i++)
            if (hudObjectsToHide[i]) hudObjectsToHide[i].SetActive(false);

        // Control off
        for (int i = 0; i < componentesAApagar.Length; i++)
            if (componentesAApagar[i]) componentesAApagar[i].enabled = false;

        // Player: si es aire, NO se congela nunca
        if (!_murioEnAire)
        {
            if (congelarEnMuerteSuelo)
                FreezePlayerFully();
        }
        else
        {
            PrepareRBForAirFall();
            if (quitarDerivaEnAire)
                RemoveHorizontalDriftAndSpin();
        }

        // cache cam rig
        if (cameraRig)
        {
            _rigLocalPos0 = cameraRig.localPosition;
            _rigLocalRot0 = cameraRig.localRotation;

            _lockLocalPos = _rigLocalPos0;
            _lockLocalRot = _rigLocalRot0;
            _lockInicializado = true;
        }
        if (cam) _fov0 = cam.fieldOfView;

        // audio
        PlayDeathSfx();
        StartGameOverMusicTransition();

        // slowmo (opcional)
        if (usarSlowMo)
        {
            Time.timeScale = Mathf.Clamp(timeScaleDuranteEfecto, 0.01f, 1f);
            Time.fixedDeltaTime = _fixedDelta0 * Time.timeScale;
        }

        // ✅ Animación de muerte mientras el cuerpo SIGUE cayendo
        if (_murioEnAire)
        {
            yield return FaseImpactoAire_Suave();
            yield return FaseCaidaAire();
            if (usarRebote) yield return FaseRebote();
        }
        else
        {
            yield return FaseSueloCostado_Suave();
        }

        // ✅ Mostrar la caída real un rato más (solo aire)
        if (_murioEnAire && durExtraCaidaAire > 0f)
            yield return WaitUnscaled(durExtraCaidaAire);

        // hold general
        yield return WaitUnscaled(durHoldAntesFade);

        // ✅ Fade + GAME OVER sin depender del suelo
        yield return FadeToBlack_SinCanvasGroup();

        // pausa total al final
        Time.timeScale = 0f;

        ShowPressAny();
        yield return WaitUnscaled(0.15f);
        _puedeReiniciar = true;
    }

    // ------------------- Golpe suelo (solo aire) -------------------

    void TryPlayGroundImpactSfx()
    {
        if (_impactoSueloDisparado) return;
        if (sfxGolpeSuelo == null) return;
        if (playerRigidbody == null) return;

        // debe venir cayendo fuerte
        if (playerRigidbody.velocity.y > golpeMinVelY) return;

        float dist = GetDistanceToGroundIgnoringSelf(out _);
        if (dist < 0f) return;

        if (dist <= groundHitCheckDist)
        {
            EnsureDeathSfxSource();
            if (sfxMuerteSource != null)
                sfxMuerteSource.PlayOneShot(sfxGolpeSuelo, Mathf.Clamp01(sfxGolpeSueloVol));

            _impactoSueloDisparado = true;
        }
    }

    // ------------------- Aire vs Suelo -------------------

    bool DecideMurioEnAire()
    {
        float vy = (playerRigidbody != null) ? playerRigidbody.velocity.y : 0f;

        // ✅ si se está moviendo en Y => aire sí o sí
        if (Mathf.Abs(vy) > deathVelYThreshold)
            return true;

        float dist = GetDistanceToGroundIgnoringSelf(out _);
        if (dist < 0f) return true;

        if (dist >= deathAirDistance) return true;
        if (dist <= deathGroundDistance) return false;

        // zona intermedia sin movimiento Y => lo tratamos como suelo
        return false;
    }

    float GetDistanceToGroundIgnoringSelf(out RaycastHit best)
    {
        best = default;
        if (playerRoot == null) return -1f;

        Collider col = GetPlayerMainCollider();
        Vector3 origin;
        float radius;

        if (col != null)
        {
            Bounds b = col.bounds;
            radius = Mathf.Max(0.06f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
            origin = new Vector3(b.center.x, b.min.y + radius + 0.05f, b.center.z);
        }
        else
        {
            origin = playerRoot.position + Vector3.up * 0.25f;
            radius = 0.25f;
        }

        float maxDist = Mathf.Max(6f, deathAirDistance + 2f);

        if (SphereCastFiltered(origin, radius, maxDist, sueloMask, out best))
            return best.distance;

        if (SphereCastFiltered(origin, radius, maxDist, ~0, out best))
            return best.distance;

        return -1f;
    }

    bool SphereCastFiltered(Vector3 origin, float radius, float dist, int mask, out RaycastHit best)
    {
        best = default;
        float bestDist = float.MaxValue;

        int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits, dist, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = _hits[i];
            if (h.collider == null) continue;
            if (_playerColliders.Contains(h.collider)) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
            }
        }

        return bestDist < float.MaxValue;
    }

    Collider GetPlayerMainCollider()
    {
        if (playerRoot == null) return null;

        var cols = playerRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            if (!cols[i].enabled) continue;
            if (cols[i].isTrigger) continue;
            return cols[i];
        }
        return null;
    }

    void CachePlayerRefsIfNeeded()
    {
        if (playerRoot == null && cameraRig != null)
        {
            var rb = cameraRig.GetComponentInParent<Rigidbody>();
            if (rb != null) { playerRigidbody = rb; playerRoot = rb.transform; }
        }

        if (playerRoot != null && playerRigidbody == null)
            playerRigidbody = playerRoot.GetComponent<Rigidbody>();
    }

    void RefreshPlayerColliderCache()
    {
        _playerColliders.Clear();
        if (playerRoot == null) return;

        var cols = playerRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            if (!c.enabled) continue;
            if (c.isTrigger) continue;
            _playerColliders.Add(c);
        }
    }

    // ------------------- Glitch overlay OFF -------------------

    void ForceDisableGlitchOverlay()
    {
        if ((glitchTransitionRoot == null || glitchOverlay == null) && autoBuscarGlitchPorNombre)
            AutoFindGlitchObjects();

        if (glitchOverlay != null && glitchOverlay.activeSelf)
            glitchOverlay.SetActive(false);

        if (glitchTransitionRoot != null && glitchTransitionRoot.activeSelf)
            glitchTransitionRoot.SetActive(false);
    }

    void AutoFindGlitchObjects()
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();

        if (glitchOverlay == null)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].name == glitchOverlayName)
                {
                    glitchOverlay = all[i].gameObject;
                    break;
                }
        }

        if (glitchTransitionRoot == null)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].name == glitchRootName)
                {
                    glitchTransitionRoot = all[i].gameObject;
                    break;
                }
        }
    }

    // ------------------- Caída aire -------------------

    void PrepareRBForAirFall()
    {
        if (playerRigidbody == null) return;

        playerRigidbody.detectCollisions = true;
        playerRigidbody.isKinematic = false;
        playerRigidbody.useGravity = true;

        var c = playerRigidbody.constraints;
        c &= ~RigidbodyConstraints.FreezePositionY;
        playerRigidbody.constraints = c;

        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        playerRigidbody.WakeUp();
    }

    void ApplyExtraGravityAirEachFrame()
    {
        if (playerRigidbody == null) return;

        PrepareRBForAirFall();

        if (quitarDerivaEnAire)
            RemoveHorizontalDriftAndSpin();

        float dtReal = Time.unscaledDeltaTime;
        if (dtReal <= 0f) return;

        float ts = Mathf.Max(0.01f, Time.timeScale); // compensa slowmo
        float comp = 1f / ts;

        float minVy = -Mathf.Abs(minDownSpeedReal) * comp;
        float maxVy = -Mathf.Abs(maxDownSpeedReal) * comp;

        Vector3 v = playerRigidbody.velocity;

        float dv = Mathf.Abs(extraGravityReal) * dtReal * comp;
        v.y -= dv;

        if (v.y > minVy) v.y = minVy;
        if (v.y < maxVy) v.y = maxVy;

        playerRigidbody.velocity = v;
    }

    void RemoveHorizontalDriftAndSpin()
    {
        if (playerRigidbody == null) return;

        Vector3 v = playerRigidbody.velocity;
        v.x = 0f;
        v.z = 0f;
        playerRigidbody.velocity = v;

        playerRigidbody.angularVelocity = Vector3.zero;
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
    }

    void FreezePlayerFully()
    {
        if (playerRigidbody == null) return;

        playerRigidbody.velocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;

        playerRigidbody.useGravity = false;
        playerRigidbody.isKinematic = true;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    // ------------------- Audio -------------------

    void CacheLevelMusicSourceIfNeeded()
    {
        if (levelMusicSource != null && !_cachedLevelVol)
        {
            _levelMusicVol0 = levelMusicSource.volume;
            _cachedLevelVol = true;
        }
    }

    void EnsureGameOverMusicSource()
    {
        if (gameOverMusicSource == null)
        {
            gameOverMusicSource = GetComponent<AudioSource>();
            if (gameOverMusicSource == null)
                gameOverMusicSource = gameObject.AddComponent<AudioSource>();
        }

        gameOverMusicSource.playOnAwake = false;
        gameOverMusicSource.loop = true;
        gameOverMusicSource.spatialBlend = 0f;
        gameOverMusicSource.ignoreListenerPause = true;

        if (musicMixerGroup != null)
            gameOverMusicSource.outputAudioMixerGroup = musicMixerGroup;
    }

    void StartGameOverMusicTransition()
    {
        EnsureGameOverMusicSource();
        CacheLevelMusicSourceIfNeeded();

        if (_coMusic != null) StopCoroutine(_coMusic);
        _coMusic = StartCoroutine(Co_CrossfadeToGameOver());
    }

    IEnumerator Co_CrossfadeToGameOver()
    {
        if (gameOverMusicClip != null)
        {
            gameOverMusicSource.clip = gameOverMusicClip;
            gameOverMusicSource.volume = 0f;
            if (!gameOverMusicSource.isPlaying)
                gameOverMusicSource.Play();
        }

        float startLevelVol = (levelMusicSource != null) ? levelMusicSource.volume : 0f;
        float startGoVol = (gameOverMusicSource != null) ? gameOverMusicSource.volume : 0f;

        float outDur = Mathf.Max(0.01f, levelMusicFadeOutSeg);
        float inDur = Mathf.Max(0.01f, gameOverFadeInSeg);

        float t = 0f;
        float total = Mathf.Max(outDur, inDur);

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            if (levelMusicSource != null)
            {
                float aOut = Mathf.Clamp01(t / outDur);
                levelMusicSource.volume = Mathf.Lerp(startLevelVol, 0f, aOut);
            }

            if (gameOverMusicClip != null && gameOverMusicSource != null)
            {
                float aIn = Mathf.Clamp01(t / inDur);
                gameOverMusicSource.volume = Mathf.Lerp(startGoVol, gameOverTargetVolume, aIn);
            }

            yield return null;
        }

        if (levelMusicSource != null)
        {
            levelMusicSource.volume = 0f;
            levelMusicSource.Stop();
        }

        if (gameOverMusicClip != null && gameOverMusicSource != null)
        {
            gameOverMusicSource.volume = gameOverTargetVolume;
            gameOverMusicSource.loop = true;
        }
    }

    void EnsureDeathSfxSource()
    {
        if (sfxMuerteSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != gameOverMusicSource)
                {
                    sfxMuerteSource = sources[i];
                    break;
                }
            }
            if (sfxMuerteSource == null)
                sfxMuerteSource = gameObject.AddComponent<AudioSource>();
        }

        sfxMuerteSource.playOnAwake = false;
        sfxMuerteSource.loop = false;
        sfxMuerteSource.spatialBlend = 0f;
        sfxMuerteSource.ignoreListenerPause = true;

        if (sfxMixerGroup != null)
            sfxMuerteSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    void PlayDeathSfx()
    {
        if (sfxMuerteClip == null) return;
        EnsureDeathSfxSource();
        sfxMuerteSource.PlayOneShot(sfxMuerteClip, Mathf.Clamp01(sfxMuerteVolumen));
    }

    // ------------------- PressAny -------------------

    void ForceHidePressAny()
    {
        if (pressAnyRoot && pressAnyRoot.activeSelf)
            pressAnyRoot.SetActive(false);

        if (txtPressAny && txtPressAny.gameObject.activeSelf)
            txtPressAny.gameObject.SetActive(false);

        SetAlphaTMP(txtPressAny, 0f);

        if (pulsePressAny)
            pulsePressAny.enabled = false;

        if (txtPressAny)
        {
            var glitch = txtPressAny.GetComponent<GlitchTypewriterTMP>();
            if (glitch) glitch.enabled = false;
        }
    }

    void ShowPressAny()
    {
        if (pressAnyRoot) pressAnyRoot.SetActive(true);
        if (txtPressAny) txtPressAny.gameObject.SetActive(true);

        SetAlphaTMP(txtPressAny, 1f);

        if (pulsePressAny) pulsePressAny.enabled = true;

        if (txtPressAny)
        {
            var glitch = txtPressAny.GetComponent<GlitchTypewriterTMP>();
            if (glitch) glitch.enabled = false;
        }
    }

    // ------------------- Cámara clamp -------------------

    void ClampRigNow()
    {
        if (!cameraRig) return;
        cameraRig.position = ClampWorldToGround(cameraRig.position);
    }

    Vector3 ClampWorldToGround(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 0.35f;

        float minExtra = alturaMinSobreSuelo;
        if (cam != null)
            minExtra = Mathf.Max(minExtra, cam.nearClipPlane + extraAntiClip);

        bool hitOk =
            Physics.SphereCast(origin, clampSphereRadius, Vector3.down, out RaycastHit hit, distanciaRaySuelo, sueloMask, QueryTriggerInteraction.Ignore)
            || Physics.SphereCast(origin, clampSphereRadius, Vector3.down, out hit, distanciaRaySuelo, ~0, QueryTriggerInteraction.Ignore);

        if (hitOk)
        {
            float sueloY = hit.point.y + minExtra;
            if (worldPos.y < sueloY) worldPos.y = sueloY;
        }

        return worldPos;
    }

    // ------------------- Efectos cámara -------------------

    IEnumerator FaseImpactoAire_Suave()
    {
        float dur = Mathf.Max(0.01f, durImpactoSeg);
        float t = 0f;

        Vector3 startPos = _rigLocalPos0;
        Quaternion startRot = _rigLocalRot0;

        Vector3 impactLocal = _rigLocalPos0 + Vector3.down * impactBajarLocalY;
        Vector3 impactWorld = LocalToWorld(impactLocal);
        impactWorld = ClampWorldToGround(impactWorld);
        Vector3 impactLocalClamped = WorldToLocal(impactWorld);

        float roll = aireUsarRoll ? Random.Range(-Mathf.Abs(aireRollImpacto), Mathf.Abs(aireRollImpacto)) : 0f;
        Quaternion impactRot = _rigLocalRot0 * Quaternion.Euler(impactPitchExtra, 0f, roll);

        float startFov = cam ? cam.fieldOfView : _fov0;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;

            float s = Smooth01(t);
            s = Smooth01(s);

            float shake = Shake01() * (shakeAmp + impactShakeExtra);

            if (cameraRig)
            {
                cameraRig.localPosition =
                    Vector3.Lerp(startPos, impactLocalClamped, s)
                    + new Vector3(shake, -shake * 0.5f, 0f);

                cameraRig.localRotation = Quaternion.Slerp(startRot, impactRot, s);
                ClampRigNow();

                _lockLocalPos = cameraRig.localPosition;
                _lockLocalRot = cameraRig.localRotation;
                _lockInicializado = true;
            }

            if (cam)
                cam.fieldOfView = Mathf.Lerp(startFov, _fov0 + impactFovExtra, s);

            yield return null;
        }
    }

    IEnumerator FaseCaidaAire()
    {
        float dur = Mathf.Max(0.01f, durCaidaSeg);
        float t = 0f;

        Vector3 startPos = cameraRig ? cameraRig.localPosition : _rigLocalPos0;
        Quaternion startRot = cameraRig ? cameraRig.localRotation : _rigLocalRot0;
        float startFov = cam ? cam.fieldOfView : _fov0;

        Vector3 desiredLocal = _rigLocalPos0 + Vector3.down * bajarLocalY;
        Vector3 desiredWorld = LocalToWorld(desiredLocal);
        desiredWorld = ClampWorldToGround(desiredWorld);
        Vector3 posTargetLocal = WorldToLocal(desiredWorld);

        float roll = aireUsarRoll ? Random.Range(-Mathf.Abs(aireRollCaida), Mathf.Abs(aireRollCaida)) : 0f;
        Quaternion rotTargetLocal = _rigLocalRot0 * Quaternion.Euler(pitchHaciaAbajo, 0f, roll);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = EaseOutCubic01(t);

            if (cameraRig)
            {
                cameraRig.localPosition = Vector3.Lerp(startPos, posTargetLocal, s);

                float shake = Shake01() * Mathf.Lerp(shakeAmp, 0f, s);
                cameraRig.localPosition += new Vector3(shake, shake * 0.5f, 0f);

                cameraRig.localRotation = Quaternion.Slerp(startRot, rotTargetLocal, s);
                ClampRigNow();

                _lockLocalPos = cameraRig.localPosition;
                _lockLocalRot = cameraRig.localRotation;
                _lockInicializado = true;
            }

            if (cam)
                cam.fieldOfView = Mathf.Lerp(startFov, _fov0 + fovExtraCaida, s);

            yield return null;
        }
    }

    IEnumerator FaseRebote()
    {
        if (!cameraRig) yield break;

        Vector3 basePos = cameraRig.localPosition;
        Quaternion baseRot = cameraRig.localRotation;

        yield return MoveLocalY(basePos, baseRot, basePos.y + reboteSubeLocalY, reboteDurSeg * 0.55f);
        yield return MoveLocalY(cameraRig.localPosition, baseRot, basePos.y - reboteBajaLocalY, reboteDurSeg * 0.45f);

        ClampRigNow();
        _lockLocalPos = cameraRig.localPosition;
        _lockLocalRot = cameraRig.localRotation;
    }

    IEnumerator MoveLocalY(Vector3 startPos, Quaternion startRot, float targetY, float dur)
    {
        float t = 0f;
        dur = Mathf.Max(0.01f, dur);

        Vector3 targetPos = new Vector3(startPos.x, targetY, startPos.z);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = Smooth01(t);

            cameraRig.localPosition = Vector3.Lerp(startPos, targetPos, s);
            cameraRig.localRotation = startRot;
            ClampRigNow();

            _lockLocalPos = cameraRig.localPosition;
            _lockLocalRot = cameraRig.localRotation;
            _lockInicializado = true;

            yield return null;
        }
    }

    IEnumerator FaseSueloCostado_Suave()
    {
        float dur = Mathf.Max(0.01f, durSueloSeg);
        float t = 0f;

        Vector3 startPos = _rigLocalPos0;
        Quaternion startRot = _rigLocalRot0;
        float startFov = cam ? cam.fieldOfView : _fov0;

        Vector3 targetLocal = _rigLocalPos0 + Vector3.down * sueloBajarLocalY;
        Vector3 targetWorld = LocalToWorld(targetLocal);
        targetWorld = ClampWorldToGround(targetWorld);
        Vector3 targetLocalClamped = WorldToLocal(targetWorld);

        float side = (Random.value < 0.5f) ? -1f : 1f;
        Quaternion targetRot = _rigLocalRot0 * Quaternion.Euler(sueloPitch, 0f, side * sueloRoll);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;

            float s = Smooth01(t);
            s = Smooth01(s);

            float shake = Shake01() * (shakeAmp + sueloShakeExtra);

            if (cameraRig)
            {
                cameraRig.localPosition = Vector3.Lerp(startPos, targetLocalClamped, s)
                    + new Vector3(shake, -shake * 0.5f, 0f);

                cameraRig.localRotation = Quaternion.Slerp(startRot, targetRot, s);
                ClampRigNow();

                _lockLocalPos = cameraRig.localPosition;
                _lockLocalRot = cameraRig.localRotation;
                _lockInicializado = true;
            }

            if (cam)
                cam.fieldOfView = Mathf.Lerp(startFov, _fov0 + sueloFovExtra, s);

            yield return null;
        }
    }

    // ------------------- Fade / Reinicio -------------------

    IEnumerator FadeToBlack_SinCanvasGroup()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeToBlackSeg);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float a = Mathf.Lerp(0f, 1f, Smooth01(t));
            SetAlphaImage(panelNegro, a);
            SetAlphaTMP(txtGameOver, a);
            yield return null;
        }

        SetAlphaImage(panelNegro, 1f);
        SetAlphaTMP(txtGameOver, 1f);
    }

    void ReiniciarNivel()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _fixedDelta0;

        IsGameOverActive = false;

        if (gameOverMusicSource != null && gameOverMusicSource.isPlaying)
            gameOverMusicSource.Stop();

        if (levelMusicSource != null && _cachedLevelVol)
            levelMusicSource.volume = _levelMusicVol0;

        if (cameraRig)
        {
            cameraRig.localPosition = _rigLocalPos0;
            cameraRig.localRotation = _rigLocalRot0;
        }
        if (cam) cam.fieldOfView = _fov0;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------- Helpers -------------------

    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    float Shake01()
    {
        return Mathf.Sin(Time.unscaledTime * shakeFreq) * 0.5f
            + (Mathf.PerlinNoise(Time.unscaledTime * 1.7f, 0f) - 0.5f);
    }

    float Smooth01(float x) => x * x * (3f - 2f * x);

    float EaseOutCubic01(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }

    void SetAlphaImage(Image img, float a)
    {
        if (!img) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    void SetAlphaTMP(TextMeshProUGUI txt, float a)
    {
        if (!txt) return;
        Color c = txt.color;
        c.a = a;
        txt.color = c;
    }

    Vector3 LocalToWorld(Vector3 localPos)
    {
        if (!cameraRig) return localPos;
        Transform p = cameraRig.parent;
        return p ? p.TransformPoint(localPos) : localPos;
    }

    Vector3 WorldToLocal(Vector3 worldPos)
    {
        if (!cameraRig) return worldPos;
        Transform p = cameraRig.parent;
        return p ? p.InverseTransformPoint(worldPos) : worldPos;
    }
}
