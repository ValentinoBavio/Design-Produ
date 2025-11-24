using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;


public class ControladorJuego : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _textoCrono;    

    int _tiempoMinutos, _tiempoSegundos, _tiempoDecimales;     

    #region Cronometro con circulo
    
    [SerializeField] private float _tiempoMaximo;

    [SerializeField] private Slider slider;

    private float _tiempoActual;

    private bool _tiempoActivado = false;



    private void Update()
    {
        if (_tiempoActivado)
        {
            CambiarContador();
        }
    }



    private void CambiarContador()
    {
        _tiempoActual -= Time.deltaTime;

        if (_tiempoActual >= 0)
        {
            slider.value = _tiempoActual;
            _textoCrono.text = Mathf.Ceil(_tiempoActual).ToString();

        }

        if (_tiempoActual <= 0)
        {
            Debug.Log("Derrota");
            CambiarTemporizador(false);

            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.name);

            _textoCrono.text = "Derrota";
            return;
        }

        _tiempoMinutos = Mathf.FloorToInt(_tiempoActual / 60);
        _tiempoSegundos = Mathf.FloorToInt(_tiempoActual % 60);


        _textoCrono.text = string.Format("{0:00}:{1:00}", _tiempoMinutos, _tiempoSegundos);
    }

    private void CambiarTemporizador(bool estado)
    {
        _tiempoActivado = estado;
    }

    public void ActivarTemporizador()
    {
        _tiempoActual = _tiempoMaximo;
        slider.maxValue = _tiempoMaximo;
        CambiarTemporizador(true);
    }

    public void DesactivarTemporizador()
    {
        CambiarTemporizador(false);
    }
    
    #endregion


}
