using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Guardado simple (PlayerPrefs)")]
    public bool guardarProgreso = true;
    public string playerPrefsKeyTotalChips = "TOTAL_CHIPS";

    bool _secretoEncontrado;
    string _secretId;
    int _chipsBonusSecreto;

    /// <summary>
    /// Llamalo desde tu SecretItemPickup cuando el player lo encuentra.
    /// </summary>
    public void MarcarSecretoEncontrado(string secretId, int chipsBonus)
    {
        _secretoEncontrado = true;
        _secretId = secretId;
        _chipsBonusSecreto = Mathf.Max(0, chipsBonus);
    }

    /// <summary>
    /// Llamalo cuando el player toca la meta.
    /// </summary>
    public void CompletarNivel()
    {
        // 1) Ocultar HUD (no el panel de resultados)
        for (int i = 0; i < hudObjectsToHide.Length; i++)
            if (hudObjectsToHide[i]) hudObjectsToHide[i].SetActive(false);

        // 2) Deshabilitar scripts del player (movement, grapple, etc)
        for (int i = 0; i < componentesAApagar.Length; i++)
            if (componentesAApagar[i]) componentesAApagar[i].enabled = false;

        // 3) Activar cámara seguridad si existe
        if (securityCamTour) securityCamTour.Activar(true);

        // 4) Frenar tiempo/cronómetro en tu controlador
        if (usarControladorJuegoParaTiempoYChips && controladorJuego != null)
            controladorJuego.DesactivarTemporizador();

        // 5) Tomar datos
        float tiempo = 0f;
        int chipsRecolectados = 0;

        if (usarControladorJuegoParaTiempoYChips && controladorJuego != null)
        {
            tiempo = controladorJuego.GetTiempoNivel();
            chipsRecolectados = controladorJuego.GetChips();
        }

        int estilo = styleTracker ? styleTracker.puntosEstilo : 0;

        // 6) Obtener regla según tiempo
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

        // 7) Guardado simple (opcional)
        int chipsTotalesAhora = 0;
        if (guardarProgreso)
        {
            chipsTotalesAhora = PlayerPrefs.GetInt(playerPrefsKeyTotalChips, 0) + totalGanado;
            PlayerPrefs.SetInt(playerPrefsKeyTotalChips, chipsTotalesAhora);

            // Mejor tiempo por nivel
            string keyBestTime = $"BEST_TIME_{nivelId}";
            float bestTime = PlayerPrefs.GetFloat(keyBestTime, float.MaxValue);
            if (tiempo < bestTime)
                PlayerPrefs.SetFloat(keyBestTime, tiempo);

            // Mejor calificación por nivel
            string keyBestGrade = $"BEST_GRADE_{nivelId}";
            string bestGrade = PlayerPrefs.GetString(keyBestGrade, "F");
            if (EsMejorCalificacion(califTexto, bestGrade))
                PlayerPrefs.SetString(keyBestGrade, califTexto);

            // Secreto encontrado (completacionismo)
            if (_secretoEncontrado && !string.IsNullOrEmpty(_secretId))
            {
                PlayerPrefs.SetInt($"SECRET_FOUND_{_secretId}", 1);
                PlayerPrefs.SetInt($"SECRET_FOUND_IN_{nivelId}", 1);
            }

            PlayerPrefs.Save();
        }
        else
        {
            chipsTotalesAhora = PlayerPrefs.GetInt(playerPrefsKeyTotalChips, 0); // por si querés mostrar algo
        }

        // 8) Armar data para la UI
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

        // 9) Mostrar UI
        if (resultadosUI != null)
            resultadosUI.Mostrar(data);
        else
            Debug.LogWarning("LevelEndManager: falta asignar ResultadosNivelUI en el Inspector.");
    }

    // Ranking: S+ > S > A > B > C > D > E > F
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