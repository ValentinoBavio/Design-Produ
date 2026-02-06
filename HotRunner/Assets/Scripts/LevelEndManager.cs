using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class LevelEndManager : MonoBehaviour
{
    [Header("Config del nivel (reglas de tiempo->calificación)")]
    public CalificacionNivelConfigSO config;

    [Header("Fuente de datos (TU sistema actual)")]
    public ControladorJuego controladorJuego;
    public bool usarControladorJuegoParaTiempoYChips = true;

    [Header("Estilo (opcional)")]
    public StylePointsTracker styleTracker;

    [Header("UI Resultados (tu script del canvas)")]
    public ResultadosNivelUI resultadosUI;

    [Header("Ocultar HUD Gameplay (arrastrá acá tu HUD_Root o canvases del HUD)")]
    public GameObject[] hudObjectsToHide;

    [Header("Cámara tipo seguridad (opcional)")]
    public SecurityCamTour securityCamTour;

    [Header("Deshabilitar componentes del Player (opcional)")]
    public MonoBehaviour[] componentesAApagar;

    [Header("Freeze físico del Player (anti-deslizamiento)")]
    [Tooltip("Si no lo asignás, lo intenta auto-detectar desde 'componentesAApagar'.")]
    public Rigidbody playerRb;

    public enum FreezeMode { Kinematic, FreezeAllConstraints }
    [Tooltip("Kinematic es lo más sólido para que NO se mueva nada.")]
    public FreezeMode freezeMode = FreezeMode.Kinematic;

    [Tooltip("Corta toda inercia al terminar.")]
    public bool zeroVelocityOnFinish = true;

    [Header("Guardado simple (PlayerPrefs)")]
    public bool guardarProgreso = true;
    public string playerPrefsKeyTotalChips = "TOTAL_CHIPS";

    // ✅ Música victoria (respeta AudioMixer Music/Master)
    [Header("Audio - Victoria (Music)")]
    [Tooltip("AudioSource que reproduce la música/BGM del nivel (ruta a tu Mixer->Music).")]
    public AudioSource musicSource;

    [Tooltip("AudioMixerGroup 'Music' (para respetar tu Master/Music volume).")]
    public AudioMixerGroup musicMixerGroup;

    [Tooltip("Clip de victoria (LOOP).")]
    public AudioClip victoryLoop;

    [Range(0f, 1f)] public float victoryVolume = 1f;
    public bool victoryLoopEnabled = true;

    [Header("Audio - Transición")]
    public bool fadeOutMusic = true;
    public float fadeOutDur = 0.35f;

    [Tooltip("Fade-in al entrar la música de victoria.")]
    public bool fadeInVictory = true;
    public float fadeInDur = 0.35f;

    [Tooltip("Si musicSource es null, intenta encontrar uno (opcional).")]
    public bool autoFindMusicSourceIfNull = true;

    [Tooltip("Opcional: si tu GO de música tiene tag, ponelo acá (ej: BGM o Music).")]
    public string musicTag = "BGM";

    Coroutine _coMusic;

    bool _secretoEncontrado;
    string _secretId;
    int _chipsBonusSecreto;

    bool _nivelCompletado = false;

    // cache RB (por si algún día querés unfreeze)
    bool _rbKin0;
    bool _rbUseGrav0;
    RigidbodyConstraints _rbCons0;
    float _rbDrag0;
    float _rbAngDrag0;

    public void MarcarSecretoEncontrado(string secretId, int chipsBonus)
    {
        _secretoEncontrado = true;
        _secretId = secretId;
        _chipsBonusSecreto = Mathf.Max(0, chipsBonus);
    }

    public void CompletarNivel()
    {
        if (_nivelCompletado) return;
        _nivelCompletado = true;

        FreezePlayerRB();

        // ✅ Música de victoria
        SwitchToVictoryMusic();

        for (int i = 0; i < hudObjectsToHide.Length; i++)
            if (hudObjectsToHide[i]) hudObjectsToHide[i].SetActive(false);

        for (int i = 0; i < componentesAApagar.Length; i++)
            if (componentesAApagar[i]) componentesAApagar[i].enabled = false;

        if (securityCamTour) securityCamTour.Activar(true);

        if (usarControladorJuegoParaTiempoYChips && controladorJuego != null)
            controladorJuego.DesactivarTemporizador();

        float tiempo = 0f;
        int chipsRecolectados = 0;

        if (usarControladorJuegoParaTiempoYChips && controladorJuego != null)
        {
            tiempo = controladorJuego.GetTiempoNivel();
            chipsRecolectados = controladorJuego.GetChips();
        }

        int estilo = styleTracker ? styleTracker.puntosEstilo : 0;

        var regla = (config != null) ? config.ObtenerRegla(tiempo) : new ReglaCalificacion(Calificacion.F);

        int mult = Mathf.Max(1, regla.multiplicadorChipsRecolectados);
        int chipsMultiplicados = chipsRecolectados * mult;

        int chipsFijos = Mathf.Max(0, regla.chipsFijos);

        int divisor = Mathf.Max(1, regla.divisorEstilo);
        int chipsPorEstilo = Mathf.FloorToInt(estilo / (float)divisor);

        int chipsSecreto = _secretoEncontrado ? _chipsBonusSecreto : 0;

        int totalGanado = chipsMultiplicados + chipsFijos + chipsPorEstilo + chipsSecreto;

        string nivelId = (config != null && !string.IsNullOrEmpty(config.nivelId)) ? config.nivelId : "Nivel_SinID";
        string califTexto = regla.calificacion.ATexto();

        int chipsTotalesAhora = 0;
        if (guardarProgreso)
        {
            chipsTotalesAhora = PlayerPrefs.GetInt(playerPrefsKeyTotalChips, 0) + totalGanado;
            PlayerPrefs.SetInt(playerPrefsKeyTotalChips, chipsTotalesAhora);

            string keyBestTime = $"BEST_TIME_{nivelId}";
            float bestTime = PlayerPrefs.GetFloat(keyBestTime, float.MaxValue);
            if (tiempo < bestTime) PlayerPrefs.SetFloat(keyBestTime, tiempo);

            string keyBestGrade = $"BEST_GRADE_{nivelId}";
            string bestGrade = PlayerPrefs.GetString(keyBestGrade, "F");
            if (EsMejorCalificacion(califTexto, bestGrade))
                PlayerPrefs.SetString(keyBestGrade, califTexto);

            if (_secretoEncontrado && !string.IsNullOrEmpty(_secretId))
            {
                PlayerPrefs.SetInt($"SECRET_FOUND_{_secretId}", 1);
                PlayerPrefs.SetInt($"SECRET_FOUND_IN_{nivelId}", 1);
            }

            PlayerPrefs.Save();
        }
        else
        {
            chipsTotalesAhora = PlayerPrefs.GetInt(playerPrefsKeyTotalChips, 0);
        }

        var data = new ResultadosNivelData
        {
            nivelId = nivelId,
            tiempoSeg = tiempo,
            calificacionTexto = califTexto,

            chipsRecolectados = chipsRecolectados,
            multiplicador = mult,
            chipsMultiplicados = chipsMultiplicados,

            chipsFijos = chipsFijos,

            puntosEstilo = estilo,
            divisorEstilo = divisor,
            chipsPorEstilo = chipsPorEstilo,

            secretoEncontrado = _secretoEncontrado,
            secretId = _secretId,
            chipsSecreto = chipsSecreto,

            totalGanado = totalGanado,
            chipsTotalesAhora = chipsTotalesAhora
        };

        if (resultadosUI != null)
            resultadosUI.Mostrar(data);
        else
            Debug.LogWarning("LevelEndManager: falta asignar ResultadosNivelUI en el Inspector.");
    }

    // ===================== AUDIO VICTORIA =====================

    void SwitchToVictoryMusic()
    {
        if (!victoryLoop) return;

        if (!musicSource && autoFindMusicSourceIfNull)
            musicSource = TryFindMusicSource();

        if (!musicSource)
        {
            Debug.LogWarning("LevelEndManager: musicSource es null. Asignalo (AudioSource del BGM).");
            return;
        }

        // ✅ asegurar ruteo a Music del Mixer
        if (musicMixerGroup && musicSource.outputAudioMixerGroup != musicMixerGroup)
            musicSource.outputAudioMixerGroup = musicMixerGroup;

        if (_coMusic != null) StopCoroutine(_coMusic);

        if (fadeOutMusic && musicSource.isPlaying)
            _coMusic = StartCoroutine(FadeOutThenPlayVictory());
        else
        {
            musicSource.Stop();
            PlayVictoryNow();
        }
    }

    IEnumerator FadeOutThenPlayVictory()
    {
        if (!musicSource) yield break;

        float dur = Mathf.Max(0.01f, fadeOutDur);
        float startVol = musicSource.volume;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        musicSource.Stop();

        // restaurar para cuando vuelvas a usar BGM normal
        musicSource.volume = startVol;

        PlayVictoryNow();
        _coMusic = null;
    }

    void PlayVictoryNow()
    {
        if (!musicSource || !victoryLoop) return;

        if (musicMixerGroup && musicSource.outputAudioMixerGroup != musicMixerGroup)
            musicSource.outputAudioMixerGroup = musicMixerGroup;

        musicSource.Stop();
        musicSource.clip = victoryLoop;
        musicSource.loop = victoryLoopEnabled;

        if (fadeInVictory)
        {
            // arrancar en 0 y subir
            musicSource.volume = 0f;
            musicSource.Play();

            if (_coMusic != null) StopCoroutine(_coMusic);
            _coMusic = StartCoroutine(FadeInTo(musicSource, Mathf.Clamp01(victoryVolume), fadeInDur));
        }
        else
        {
            musicSource.volume = Mathf.Clamp01(victoryVolume);
            musicSource.Play();
        }
    }

    IEnumerator FadeInTo(AudioSource src, float targetVol, float dur)
    {
        dur = Mathf.Max(0.01f, dur);
        float start = src.volume;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            src.volume = Mathf.Lerp(start, targetVol, t);
            yield return null;
        }
        src.volume = targetVol;
        _coMusic = null;
    }

    AudioSource TryFindMusicSource()
    {
        if (!string.IsNullOrEmpty(musicTag))
        {
            var go = GameObject.FindGameObjectWithTag(musicTag);
            if (go)
            {
                var a = go.GetComponent<AudioSource>();
                if (a) return a;
            }
        }

        var all = FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var a = all[i];
            if (!a) continue;
            if (a.isPlaying && a.clip != null) return a;
        }

        return null;
    }

    // ===================== FREEZE PLAYER =====================

    void FreezePlayerRB()
    {
        if (playerRb == null)
            playerRb = TryAutoFindPlayerRB();

        if (playerRb == null) return;

        _rbKin0 = playerRb.isKinematic;
        _rbUseGrav0 = playerRb.useGravity;
        _rbCons0 = playerRb.constraints;
        _rbDrag0 = playerRb.drag;
        _rbAngDrag0 = playerRb.angularDrag;

        if (zeroVelocityOnFinish)
        {
            playerRb.velocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        if (freezeMode == FreezeMode.Kinematic)
        {
            playerRb.isKinematic = true;
            playerRb.useGravity = false;
        }
        else
        {
            playerRb.constraints = RigidbodyConstraints.FreezeAll;
            playerRb.drag = 999f;
            playerRb.angularDrag = 999f;
        }

        playerRb.Sleep();
    }

    Rigidbody TryAutoFindPlayerRB()
    {
        if (componentesAApagar != null)
        {
            for (int i = 0; i < componentesAApagar.Length; i++)
            {
                var mb = componentesAApagar[i];
                if (!mb) continue;

                var rb = mb.GetComponentInParent<Rigidbody>();
                if (rb) return rb;
            }
        }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go) return go.GetComponentInChildren<Rigidbody>();

        return null;
    }

    // ===================== CALIFICACIÓN =====================

    bool EsMejorCalificacion(string nueva, string actual)
    {
        return PuntajeCalif(nueva) > PuntajeCalif(actual);
    }

    int PuntajeCalif(string c)
    {
        switch (c)
        {
            case "S+": return 7;
            case "S": return 6;
            case "A": return 5;
            case "B": return 4;
            case "C": return 3;
            case "D": return 2;
            case "E": return 1;
            case "F": return 0;
            default: return 0;
        }
    }
}
