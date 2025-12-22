using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    [Header("Fuente")]
    public ControladorJuego controladorJuego;

    [Header("HUD a ocultar al morir")]
    public GameObject[] hudObjectsToHide;

    [Header("Deshabilitar control del player")]
    public MonoBehaviour[] componentesAApagar;

    [Header("Cámara (efecto caída)")]
    public Transform cameraRig; // Head
    public Camera cam;

    [Header("Evitar atravesar suelo (Raycast)")]
    public LayerMask sueloMask = ~0;
    public float distanciaRaySuelo = 6f;
    public float alturaMinSobreSuelo = 0.15f;

    [Header("Impacto (más duro)")]
    public float durImpactoSeg = 0.18f;
    public float impactBajarLocalY = 0.25f;
    public float impactPitchExtra = 25f;
    public float impactShakeExtra = 0.04f;
    public float impactFovExtra = 18f;

    [Header("Caída (más rápida)")]
    public float durCaidaSeg = 0.38f;
    public float bajarLocalY = 0.65f;
    public float pitchHaciaAbajo = 75f;
    public float rollGrados = 12f;

    public float shakeAmp = 0.02f;
    public float shakeFreq = 18f;

    [Header("FOV (caída)")]
    public float fovExtraCaida = 8f;

    [Header("Mini rebote (opcional)")]
    public bool usarRebote = true;
    public float reboteSubeLocalY = 0.06f;
    public float reboteBajaLocalY = 0.02f;
    public float reboteDurSeg = 0.12f;

    [Header("Hold antes del fade")]
    public float durHoldAntesFade = 0.25f;

    [Header("Fade a negro (3s) SIN CanvasGroup")]
    public Image panelNegro;
    public TextMeshProUGUI txtGameOver;
    public float fadeToBlackSeg = 3f;

    [Header("Press Any (NO debe verse hasta el final)")]
    [Tooltip("Arrastrá acá el GO padre (PressAnyButton).")]
    public GameObject pressAnyRoot;

    [Tooltip("Arrastrá el TMP del texto Press Any.")]
    public TextMeshProUGUI txtPressAny;

    [Tooltip("Si usás pulso, arrastrá el componente TMPPulseUnscaled del PressAny.")]
    public TMPPulseUnscaled pulsePressAny;

    [Header("Tiempo (slow motion antes del fade)")]
    public bool usarSlowMo = true;
    public float timeScaleDuranteEfecto = 0.25f;

    [Header("Bloqueo extra (anti look)")]
    public bool forzarBloqueoCamara = true;

    bool _gameOverActivo = false;
    bool _puedeReiniciar = false;

    Vector3 _rigLocalPos0;
    Quaternion _rigLocalRot0;
    float _fov0;

    Vector3 _lockLocalPos;
    Quaternion _lockLocalRot;
    bool _lockInicializado = false;

    Coroutine _co;

    private void Awake()
    {
        if (cameraRig)
        {
            _rigLocalPos0 = cameraRig.localPosition;
            _rigLocalRot0 = cameraRig.localRotation;
        }
        if (cam)
            _fov0 = cam.fieldOfView;

        // Arrancar oculto
        SetAlphaImage(panelNegro, 0f);
        SetAlphaTMP(txtGameOver, 0f);

        // ✅ FORZAR ocultar PressAny desde el arranque
        ForceHidePressAny();
    }

    private void Start()
    {
        // por si algún Animator lo prende en Start, lo volvemos a ocultar
        ForceHidePressAny();
    }

    private void OnEnable()
    {
        if (controladorJuego != null)
            controladorJuego.OnTiempoAgotado += DispararGameOver;
    }

    private void OnDisable()
    {
        if (controladorJuego != null)
            controladorJuego.OnTiempoAgotado -= DispararGameOver;
    }

    private void LateUpdate()
    {
        if (_gameOverActivo && forzarBloqueoCamara && cameraRig && _lockInicializado)
        {
            cameraRig.localPosition = _lockLocalPos;
            cameraRig.localRotation = _lockLocalRot;
        }
    }

    private void Update()
    {
        // ✅ Mientras NO sea game over, que NO se vea nunca (aunque otro script lo active)
        if (!_gameOverActivo)
        {
            ForceHidePressAny();
            return;
        }

        if (!_puedeReiniciar)
            return;

        if (
            Input.anyKeyDown
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonDown(1)
            || Input.GetMouseButtonDown(2)
        )
            ReiniciarNivel();
    }

    public void DispararGameOver()
    {
        if (_gameOverActivo)
            return;
        _gameOverActivo = true;

        if (_co != null)
            StopCoroutine(_co);
        _co = StartCoroutine(RutinaGameOver());
    }

    IEnumerator RutinaGameOver()
    {
        // HUD off
        for (int i = 0; i < hudObjectsToHide.Length; i++)
            if (hudObjectsToHide[i])
                hudObjectsToHide[i].SetActive(false);

        // Player control off
        for (int i = 0; i < componentesAApagar.Length; i++)
            if (componentesAApagar[i])
                componentesAApagar[i].enabled = false;

        if (cameraRig)
        {
            _rigLocalPos0 = cameraRig.localPosition;
            _rigLocalRot0 = cameraRig.localRotation;

            _lockLocalPos = _rigLocalPos0;
            _lockLocalRot = _rigLocalRot0;
            _lockInicializado = true;
        }
        if (cam)
            _fov0 = cam.fieldOfView;

        // SlowMo opcional
        if (usarSlowMo)
            Time.timeScale = Mathf.Clamp(timeScaleDuranteEfecto, 0.01f, 1f);

        // Impacto + caída
        yield return FaseImpactoDuro();
        yield return FaseCaidaRapida();

        if (usarRebote)
            yield return FaseRebote();

        yield return WaitUnscaled(durHoldAntesFade);

        // Fade: aparece GAME OVER
        yield return FadeToBlack_SinCanvasGroup();

        // Pausa total
        Time.timeScale = 0f;

        // ✅ recién acá aparece PRESS ANY
        ShowPressAny();

        yield return WaitUnscaled(0.15f);
        _puedeReiniciar = true;
    }

    // ------------------- PRESS ANY (FORCE HIDE / SHOW) -------------------

    void ForceHidePressAny()
    {
        if (pressAnyRoot && pressAnyRoot.activeSelf)
            pressAnyRoot.SetActive(false);

        if (txtPressAny && txtPressAny.gameObject.activeSelf)
            txtPressAny.gameObject.SetActive(false);

        SetAlphaTMP(txtPressAny, 0f);

        if (pulsePressAny)
            pulsePressAny.enabled = false;

        // Si le pusiste GlitchTypewriterTMP al mismo TMP, lo apagamos para que no escriba antes
        if (txtPressAny)
        {
            var glitch = txtPressAny.GetComponent<GlitchTypewriterTMP>();
            if (glitch)
                glitch.enabled = false;
        }
    }

    void ShowPressAny()
    {
        if (pressAnyRoot)
            pressAnyRoot.SetActive(true);
        if (txtPressAny)
            txtPressAny.gameObject.SetActive(true);

        SetAlphaTMP(txtPressAny, 1f);

        if (pulsePressAny)
            pulsePressAny.enabled = true;

        if (txtPressAny)
        {
            var glitch = txtPressAny.GetComponent<GlitchTypewriterTMP>();
            if (glitch)
                glitch.enabled = true;
        }
    }

    // ------------------- EFECTOS CÁMARA -------------------

    IEnumerator FaseImpactoDuro()
    {
        float dur = Mathf.Max(0.01f, durImpactoSeg);
        float t = 0f;

        Vector3 startPos = _rigLocalPos0;
        Quaternion startRot = _rigLocalRot0;

        Vector3 impactLocal = _rigLocalPos0 + Vector3.down * impactBajarLocalY;
        Vector3 impactWorld = LocalToWorld(impactLocal);
        impactWorld = ClampWorldToGround(impactWorld);
        Vector3 impactLocalClamped = WorldToLocal(impactWorld);

        float roll = Random.Range(-rollGrados, rollGrados);
        Quaternion impactRot = _rigLocalRot0 * Quaternion.Euler(impactPitchExtra, 0f, roll);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = EaseOutCubic01(t);

            float shake = Shake01() * (shakeAmp + impactShakeExtra);

            if (cameraRig)
            {
                cameraRig.localPosition =
                    Vector3.Lerp(startPos, impactLocalClamped, s)
                    + new Vector3(shake, -shake * 0.5f, 0f);
                cameraRig.localRotation = Quaternion.Slerp(startRot, impactRot, s);

                _lockLocalPos = cameraRig.localPosition;
                _lockLocalRot = cameraRig.localRotation;
                _lockInicializado = true;
            }

            if (cam)
                cam.fieldOfView = Mathf.Lerp(_fov0, _fov0 + impactFovExtra, s);

            yield return null;
        }
    }

    IEnumerator FaseCaidaRapida()
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

        float roll = Random.Range(-rollGrados, rollGrados);
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

                _lockLocalPos = cameraRig.localPosition;
                _lockLocalRot = cameraRig.localRotation;
                _lockInicializado = true;
            }

            if (cam)
                cam.fieldOfView = Mathf.Lerp(startFov, _fov0 + fovExtraCaida, s);

            yield return null;
        }

        if (cameraRig)
        {
            cameraRig.localPosition = posTargetLocal;
            cameraRig.localRotation = rotTargetLocal;
            _lockLocalPos = posTargetLocal;
            _lockLocalRot = rotTargetLocal;
        }
        if (cam)
            cam.fieldOfView = _fov0 + fovExtraCaida;
    }

    IEnumerator FaseRebote()
    {
        if (!cameraRig)
            yield break;

        Vector3 basePos = cameraRig.localPosition;
        Quaternion baseRot = cameraRig.localRotation;

        yield return MoveLocalY(
            basePos,
            baseRot,
            basePos.y + reboteSubeLocalY,
            reboteDurSeg * 0.55f
        );
        yield return MoveLocalY(
            cameraRig.localPosition,
            baseRot,
            basePos.y - reboteBajaLocalY,
            reboteDurSeg * 0.45f
        );

        Vector3 w = cameraRig.position;
        w = ClampWorldToGround(w);
        cameraRig.position = w;

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

            _lockLocalPos = cameraRig.localPosition;
            _lockLocalRot = cameraRig.localRotation;
            _lockInicializado = true;

            yield return null;
        }
    }

    // ------------------- FADE -------------------

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

    // ------------------- REINICIO -------------------

    void ReiniciarNivel()
    {
        Time.timeScale = 1f;

        if (cameraRig)
        {
            cameraRig.localPosition = _rigLocalPos0;
            cameraRig.localRotation = _rigLocalRot0;
        }
        if (cam)
            cam.fieldOfView = _fov0;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------- HELPERS -------------------

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
        if (!img)
            return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    void SetAlphaTMP(TextMeshProUGUI txt, float a)
    {
        if (!txt)
            return;
        Color c = txt.color;
        c.a = a;
        txt.color = c;
    }

    Vector3 LocalToWorld(Vector3 localPos)
    {
        if (!cameraRig)
            return localPos;
        Transform p = cameraRig.parent;
        return p ? p.TransformPoint(localPos) : localPos;
    }

    Vector3 WorldToLocal(Vector3 worldPos)
    {
        if (!cameraRig)
            return worldPos;
        Transform p = cameraRig.parent;
        return p ? p.InverseTransformPoint(worldPos) : worldPos;
    }

    Vector3 ClampWorldToGround(Vector3 worldPos)
    {
        Vector3 rayOrigin = worldPos + Vector3.up * 0.35f;

        if (
            Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                distanciaRaySuelo,
                sueloMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            float sueloY = hit.point.y + alturaMinSobreSuelo;
            if (worldPos.y < sueloY)
                worldPos.y = sueloY;
        }

        return worldPos;
    }
}
