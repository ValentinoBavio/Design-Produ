using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ControladorJuego : MonoBehaviour
{
    public enum ModoTextoTemporizador
    {
        MinSeg,
        Porcentaje
    }

    [Header("UI - Temporizador (cuenta regresiva)")]
    [SerializeField] private TextMeshProUGUI _textoCrono;

    [Tooltip("Cómo se muestra el countdown en el texto.")]
    [SerializeField] private ModoTextoTemporizador modoTextoTemporizador = ModoTextoTemporizador.Porcentaje;

    [Tooltip("Formato del porcentaje. Puede ser {0:000} o {0:000}% o {0:000} % (da igual, el % lo ponemos nosotros).")]
    [SerializeField] private string formatoPorcentaje = "{0:000} %";

    [Tooltip("Espacio FINO entre número y % en PIXELES. Probá 2..8 (4 suele quedar como tu 1ra captura).")]
    [SerializeField] private int espacioPorcentajePx = 4;

    [Tooltip("UI A: puede ser circular o barra (Slider).")]
    [SerializeField] private Slider slider;

    [Tooltip("UI B: otro Slider opcional (otra barra / otro círculo).")]
    [SerializeField] private Slider sliderAlternativo;

    [Tooltip("UI B alternativa: Image tipo Filled simple (life bar).")]
    [SerializeField] private Image barraFillImage; // Image Type = Filled, Fill Method = Horizontal

    [SerializeField] private float _tiempoMaximo = 60f;

    [Header("Auto start temporizador")]
    [SerializeField] private bool iniciarTemporizadorAlPlay = true;
    [SerializeField] private bool resetearTemporizadorAlIniciar = true;

    [Header("UI - Cronómetro de nivel (tiempo transcurrido)")]
    [SerializeField] private TextMeshProUGUI _textoCronoNivel;
    [SerializeField] private bool iniciarCronometroNivelAlPlay = true;

    [Header("Anti “baila” del texto (TMP)")]
    [SerializeField] private bool usarMonospaceTMP = true;
    [SerializeField] private float monospaceEm = 0.65f;

    [Header("Tiempo sin escala (si usás pausas con timeScale=0)")]
    [SerializeField] private bool usarTiempoSinEscala = false;

    // ===================== BARRA TIEMPO (FRONT/BACK) =====================
    [Header("UI - Barra tiempo tipo VIDA (Front/Back Fill)")]
    [Tooltip("Front: baja rápido (la “real”). Image Type = Filled.")]
    [SerializeField] private Image tiempoFrontFill;

    [Tooltip("Back: baja más lento con delay (efecto backfill). Image Type = Filled.")]
    [SerializeField] private Image tiempoBackFill;

    [Tooltip("Para que el backfill funcione bien con un timer que baja siempre, por defecto actualiza por SEGUNDOS enteros (tick).")]
    [SerializeField] private bool barraUsaSegundosEnteros = true;

    [Header("Animación Front (rápida)")]
    [Tooltip("Velocidad de acercamiento del front (fill por segundo).")]
    [SerializeField] private float frontLerpSpeed = 10f;

    [Header("Animación Back (lenta)")]
    [SerializeField] private float hbDelay = 0.35f;
    [SerializeField] private float hbLerpSpeed = 2.0f;

    float _frontFillVis = 1f;
    float _backFillVis = 1f;
    float _backDelayUntil = 0f;
    float _backTargetFill = 1f;

    // -------- runtime (temporizador) --------
    private float _tiempoActual;
    private bool _tiempoActivado = false;

    // -------- runtime (cronómetro nivel) --------
    private float _tiempoNivel;
    private bool _cronometroNivelActivo = false;

    // ===== EVENTO GAME OVER (TIEMPO AGOTADO) =====
    public event System.Action OnTiempoAgotado;
    private bool _tiempoAgotadoNotificado = false;

    void NotificarTiempoAgotadoUnaVez()
    {
        if (_tiempoAgotadoNotificado) return;
        _tiempoAgotadoNotificado = true;
        OnTiempoAgotado?.Invoke();
    }

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
        _tiempoMaximo = Mathf.Max(0.01f, _tiempoMaximo);

        _tiempoActual = _tiempoMaximo;
        SyncTiempoUI();
        ActualizarTextoTemporizador();

        InitBarraTiempoFrontBack();

        _tiempoNivel = 0f;
        ActualizarTextoCronometroNivel();

        _chips = Mathf.Max(0, chipsIniciales);
        ActualizarTextoChips();
    }

    void Start()
    {
        if (iniciarTemporizadorAlPlay)
        {
            if (resetearTemporizadorAlIniciar) ActivarTemporizador();
            else ReanudarTemporizadorSinReset();
        }

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

        TickBarraTiempoFrontBack(dt);
    }

    float Now()
    {
        return usarTiempoSinEscala ? Time.unscaledTime : Time.time;
    }

    // ================= TEMPORIZADOR (COUNTDOWN) =================
    void TickTemporizador(float dt)
    {
        _tiempoActual -= dt;

        if (_tiempoActual <= 0f)
        {
            _tiempoActual = 0f;

            SyncTiempoUI();
            ForceSetBarraTiempoFrontBack();

            // ✅ queda en 0% (sin “Derrota”)
            ActualizarTextoTemporizador();

            // ✅ lo pausamos (después vemos game over)
            _tiempoActivado = false;

            // ✅ EVENTO: tiempo agotado (una sola vez)
            NotificarTiempoAgotadoUnaVez();
            return;
        }

        SyncTiempoUI();
        ActualizarTextoTemporizador();
    }

    void ActualizarTextoTemporizador()
    {
        if (!_textoCrono) return;

        if (modoTextoTemporizador == ModoTextoTemporizador.Porcentaje)
        {
            float f01 = (_tiempoMaximo > 0f) ? Mathf.Clamp01(_tiempoActual / _tiempoMaximo) : 0f;
            int pct = Mathf.Clamp(Mathf.CeilToInt(f01 * 100f), 0, 100);

            // Querés 0% (no 000%) cuando termina
            string numero;
            if (pct <= 0)
            {
                numero = "000";
            }
            else
            {
                // Soporta que tu formato tenga o no el símbolo %
                string tmp = string.Format(formatoPorcentaje, pct);
                tmp = tmp.Replace("%", "").Trim(); // nos quedamos solo con el número formateado
                numero = tmp;
            }

            int px = Mathf.Clamp(espacioPorcentajePx, 0, 50);
            string txt = $"{numero}<space={px}px>%";

            if (usarMonospaceTMP) txt = $"<mspace={monospaceEm}em>{txt}</mspace>";
            _textoCrono.text = txt;
        }
        else
        {
            int totalSeconds = Mathf.CeilToInt(_tiempoActual);
            int mm = totalSeconds / 60;
            int ss = totalSeconds % 60;

            string txt = $"{mm:00}:{ss:00}";
            if (usarMonospaceTMP) txt = $"<mspace={monospaceEm}em>{txt}</mspace>";
            _textoCrono.text = txt;
        }
    }

    void SyncTiempoUI()
    {
        float t = Mathf.Clamp(_tiempoActual, 0f, _tiempoMaximo);

        if (slider)
        {
            slider.maxValue = _tiempoMaximo;
            slider.value = t;
        }

        if (sliderAlternativo)
        {
            sliderAlternativo.maxValue = _tiempoMaximo;
            sliderAlternativo.value = t;
        }

        if (barraFillImage)
        {
            float f = (_tiempoMaximo > 0f) ? (t / _tiempoMaximo) : 0f;
            barraFillImage.fillAmount = Mathf.Clamp01(f);
        }

        _backTargetFill = GetFillTargetFromTiempoActual();
        if (_backFillVis < _backTargetFill)
            _backFillVis = _backTargetFill;
    }

    public void ActivarTemporizador()
    {
        _tiempoAgotadoNotificado = false; // ✅ reset del evento

        _tiempoMaximo = Mathf.Max(0.01f, _tiempoMaximo);
        _tiempoActual = _tiempoMaximo;

        _tiempoActivado = true;

        SyncTiempoUI();
        ActualizarTextoTemporizador();
        ForceSetBarraTiempoFrontBack();
    }

    public void ReanudarTemporizadorSinReset()
    {
        _tiempoAgotadoNotificado = false; // ✅ reset del evento

        _tiempoMaximo = Mathf.Max(0.01f, _tiempoMaximo);
        _tiempoActual = Mathf.Clamp(_tiempoActual, 0f, _tiempoMaximo);

        _tiempoActivado = true;

        SyncTiempoUI();
        ActualizarTextoTemporizador();
        ForceSetBarraTiempoFrontBack();
    }

    // ✅ POWER UP: restaurar a 60s (100%)
    public void RestaurarTiempoCompleto()
    {
        _tiempoActual = _tiempoMaximo;

        _backTargetFill = GetFillTargetFromTiempoActual();
        _frontFillVis = _backTargetFill;
        _backFillVis = _backTargetFill;
        _backDelayUntil = 0f;

        SyncTiempoUI();
        ActualizarTextoTemporizador();
        ApplyBarraTiempoToUI();
    }

    // ===================== “DAÑO” AL TIEMPO (ENEMIGO) =====================
    public void RestarTiempo(float segundos)
    {
        if (segundos <= 0f) return;

        float prev = _tiempoActual;
        _tiempoActual = Mathf.Max(0f, _tiempoActual - segundos);

        if (_tiempoActual < prev)
            DispararBackfill();

        SyncTiempoUI();
        ActualizarTextoTemporizador();

        if (_tiempoActual <= 0f)
        {
            _tiempoActual = 0f;
            SyncTiempoUI();
            ForceSetBarraTiempoFrontBack();

            // ✅ queda en 0%
            ActualizarTextoTemporizador();

            _tiempoActivado = false;

            // ✅ EVENTO: tiempo agotado (una sola vez)
            NotificarTiempoAgotadoUnaVez();
        }
    }

    public void SumarTiempo(float segundos)
    {
        if (segundos <= 0f) return;

        _tiempoActual = Mathf.Min(_tiempoMaximo, _tiempoActual + segundos);

        _backTargetFill = GetFillTargetFromTiempoActual();
        _frontFillVis = _backTargetFill;
        _backFillVis = _backTargetFill;

        SyncTiempoUI();
        ActualizarTextoTemporizador();
        ApplyBarraTiempoToUI();
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

        int totalCent = Mathf.FloorToInt(_tiempoNivel * 100f);
        int mm = (totalCent / 6000);
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
        ActualizarTextoCronometroNivel();
    }

    public float GetTiempoNivel() => _tiempoNivel;

    // ================= STOP GLOBAL (META FINAL) =================
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

    // ===================== Barra Front/Back helpers =====================
    void InitBarraTiempoFrontBack()
    {
        _backTargetFill = GetFillTargetFromTiempoActual();
        _frontFillVis = _backTargetFill;
        _backFillVis = _backTargetFill;
        _backDelayUntil = 0f;
        ApplyBarraTiempoToUI();
    }

    float GetFillTargetFromTiempoActual()
    {
        float t = Mathf.Clamp(_tiempoActual, 0f, _tiempoMaximo);
        if (barraUsaSegundosEnteros) t = Mathf.Ceil(t);
        return (_tiempoMaximo > 0f) ? Mathf.Clamp01(t / _tiempoMaximo) : 0f;
    }

    void DispararBackfill()
    {
        _backDelayUntil = Now() + Mathf.Max(0f, hbDelay);
    }

    void TickBarraTiempoFrontBack(float dt)
    {
        if (!tiempoFrontFill && !tiempoBackFill) return;

        float target = GetFillTargetFromTiempoActual();

        if (target < _backTargetFill - 0.0001f)
            DispararBackfill();

        _backTargetFill = target;

        float fSpeed = Mathf.Max(0.01f, frontLerpSpeed);
        _frontFillVis = Mathf.MoveTowards(_frontFillVis, target, fSpeed * dt);

        if (_backFillVis < target)
        {
            _backFillVis = target;
        }
        else
        {
            if (Now() >= _backDelayUntil)
            {
                float bSpeed = Mathf.Max(0.01f, hbLerpSpeed);
                _backFillVis = Mathf.MoveTowards(_backFillVis, target, bSpeed * dt);
            }
        }

        ApplyBarraTiempoToUI();
    }

    void ForceSetBarraTiempoFrontBack()
    {
        float target = GetFillTargetFromTiempoActual();
        _backTargetFill = target;
        _frontFillVis = target;
        _backFillVis = target;
        _backDelayUntil = 0f;
        ApplyBarraTiempoToUI();
    }

    void ApplyBarraTiempoToUI()
    {
        if (tiempoFrontFill) tiempoFrontFill.fillAmount = _frontFillVis;
        if (tiempoBackFill) tiempoBackFill.fillAmount = _backFillVis;
    }
}