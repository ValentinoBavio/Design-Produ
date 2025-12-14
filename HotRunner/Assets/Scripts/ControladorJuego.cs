using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ControladorJuego : MonoBehaviour
{
    [Header("UI - Temporizador (cuenta regresiva)")]
    [SerializeField] private TextMeshProUGUI _textoCrono;
    [SerializeField] private Slider slider;
    [SerializeField] private float _tiempoMaximo = 60f;

    [Header("Auto start temporizador")]
    [SerializeField] private bool iniciarTemporizadorAlPlay = true;
    [SerializeField] private bool resetearTemporizadorAlIniciar = true;

    [Header("UI - Cronómetro de nivel (tiempo transcurrido)")]
    [SerializeField] private TextMeshProUGUI _textoCronoNivel; // <-- asigná este TMP
    [SerializeField] private bool iniciarCronometroNivelAlPlay = true;

    [Header("Anti “baila” del texto (TMP)")]
    [SerializeField] private bool usarMonospaceTMP = true;
    [SerializeField] private float monospaceEm = 0.65f;

    [Header("Tiempo sin escala (si usás pausas con timeScale=0)")]
    [SerializeField] private bool usarTiempoSinEscala = false;

    // -------- runtime (temporizador) --------
    private float _tiempoActual;
    private bool _tiempoActivado = false;

    // -------- runtime (cronómetro nivel) --------
    private float _tiempoNivel;
    private bool _cronometroNivelActivo = false;

    // -------- Chips --------
    [Header("UI - Chips")]
    [SerializeField] private TextMeshProUGUI _textoChips; // Ej: "x 000"
    [SerializeField] private string formatoChips = "x {0:000}";
    [SerializeField] private int chipsIniciales = 0;
    private int _chips;

    [Header("FX UI (opcional)")]
    public ChipUIBridge chipUIBridge;

    void Awake()
    {
        // Countdown
        _tiempoActual = _tiempoMaximo;
        if (slider)
        {
            slider.maxValue = _tiempoMaximo;
            slider.value = _tiempoActual;
        }
        ActualizarTextoTemporizador();

        // Stopwatch
        _tiempoNivel = 0f;
        ActualizarTextoCronometroNivel();

        // Chips
        _chips = Mathf.Max(0, chipsIniciales);
        ActualizarTextoChips();
    }

    void Start()
    {
        // Arranque de temporizador
        if (iniciarTemporizadorAlPlay)
        {
            if (resetearTemporizadorAlIniciar) ActivarTemporizador();
            else ReanudarTemporizadorSinReset();
        }

        // Arranque de cronómetro de nivel
        if (iniciarCronometroNivelAlPlay)
            IniciarCronometroNivel(true);
    }

    void Update()
    {
        float dt = usarTiempoSinEscala ? Time.unscaledDeltaTime : Time.deltaTime;

        if (_tiempoActivado)
            TickTemporizador(dt);

        if (_cronometroNivelActivo)
            TickCronometroNivel(dt);
    }

    // ================= TEMPORIZADOR (COUNTDOWN) =================
    void TickTemporizador(float dt)
    {
        _tiempoActual -= dt;

        if (_tiempoActual <= 0f)
        {
            _tiempoActual = 0f;
            if (slider) slider.value = 0f;

            Debug.Log("Derrota");
            if (_textoCrono) _textoCrono.text = "Derrota";

            _tiempoActivado = false;

            // Si NO querés reiniciar escena al perder, comentá estas 2 líneas:
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.name);
            return;
        }

        if (slider) slider.value = _tiempoActual;
        ActualizarTextoTemporizador();
    }

    void ActualizarTextoTemporizador()
    {
        int totalSeconds = Mathf.CeilToInt(_tiempoActual);
        int mm = totalSeconds / 60;
        int ss = totalSeconds % 60;

        string txt = $"{mm:00}:{ss:00}";
        if (usarMonospaceTMP) txt = $"<mspace={monospaceEm}em>{txt}</mspace>";

        if (_textoCrono) _textoCrono.text = txt;
    }

    public void ActivarTemporizador()
    {
        _tiempoActual = _tiempoMaximo;

        if (slider)
        {
            slider.maxValue = _tiempoMaximo;
            slider.value = _tiempoActual;
        }

        ActualizarTextoTemporizador();
        _tiempoActivado = true;
    }

    public void ReanudarTemporizadorSinReset()
    {
        _tiempoActual = Mathf.Clamp(_tiempoActual, 0f, _tiempoMaximo);

        if (slider)
        {
            slider.maxValue = _tiempoMaximo;
            slider.value = _tiempoActual;
        }

        ActualizarTextoTemporizador();
        _tiempoActivado = true;
    }

    // ================= CRONÓMETRO DE NIVEL (STOPWATCH) =================
    void TickCronometroNivel(float dt)
    {
        _tiempoNivel += dt;
        ActualizarTextoCronometroNivel();
    }

    void ActualizarTextoCronometroNivel()
    {
        if (!_textoCronoNivel) return;

        int totalCent = Mathf.FloorToInt(_tiempoNivel * 100f); // centésimas
        int mm = (totalCent / 6000);                           // 60s * 100
        int ss = (totalCent / 100) % 60;
        int cc = totalCent % 100;

        string txt = $"{mm:00}:{ss:00}:{cc:00}";
        if (usarMonospaceTMP) txt = $"<mspace={monospaceEm}em>{txt}</mspace>";

        _textoCronoNivel.text = txt;
    }

    public void IniciarCronometroNivel(bool resetear)
    {
        if (resetear) _tiempoNivel = 0f;
        _cronometroNivelActivo = true;
        ActualizarTextoCronometroNivel();
    }

    public void DetenerCronometroNivel()
    {
        _cronometroNivelActivo = false;
        ActualizarTextoCronometroNivel(); // queda mostrando el tiempo final
    }

    public float GetTiempoNivel() => _tiempoNivel;

    // ================= STOP GLOBAL (META FINAL) =================
    // Tu MetaFinal llama a esto: frenamos TODO (temporizador + cronómetro nivel)
    public void DesactivarTemporizador()
    {
        _tiempoActivado = false;
        DetenerCronometroNivel();
    }

    // ================= CHIPS =================
    public void SumarChips(int cantidad = 1)
    {
        if (cantidad <= 0) return;

        _chips += cantidad;
        ActualizarTextoChips();

        if (chipUIBridge) chipUIBridge.Pulse();
    }

    public int GetChips() => _chips;

    void ActualizarTextoChips()
    {
        if (_textoChips)
            _textoChips.text = string.Format(formatoChips, _chips);
    }
}