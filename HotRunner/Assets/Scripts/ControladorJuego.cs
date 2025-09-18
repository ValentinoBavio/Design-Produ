using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ControladorJuego : MonoBehaviour
{
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
        }

        if (_tiempoActual <= 0)
        {
            Debug.Log("Derrota");
            CambiarTemporizador(false);
        }
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
}
