using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StylePointsTracker : MonoBehaviour
{
    [Header("Puntos base")]
    public int puntosPorEnemigo = 25;
    public int puntosPorBreakable = 10;

    [Header("Combo tipo Tony Hawk (opcional)")]
    public bool usarCombo = true;
    public float ventanaComboSeg = 2.25f;
    public int bonusPorTrucoExtra = 5;

    public int puntosEstilo { get; private set; }
    public int enemigosEliminados { get; private set; }
    public int breakablesDestruidos { get; private set; }
    public int trucosGrapple { get; private set; }

    float _timerCombo;
    int _comboActual; // 0 = no hay combo

    public void Resetear()
    {
        puntosEstilo = 0;
        enemigosEliminados = 0;
        breakablesDestruidos = 0;
        trucosGrapple = 0;
        _timerCombo = 0f;
        _comboActual = 0;
    }

    private void Update()
    {
        if (!usarCombo) return;

        if (_comboActual > 0)
        {
            _timerCombo -= Time.deltaTime;
            if (_timerCombo <= 0f)
            {
                _comboActual = 0;
                _timerCombo = 0f;
            }
        }
    }

    void AbrirVentanaCombo()
    {
        if (!usarCombo) return;
        _timerCombo = ventanaComboSeg;
        _comboActual = Mathf.Max(1, _comboActual + 1);
    }

    int MultiplicadorCombo()
    {
        if (!usarCombo) return 1;
        return Mathf.Max(1, _comboActual);
    }

    public void NotificarEnemigoEliminado()
    {
        enemigosEliminados++;
        AbrirVentanaCombo();
        puntosEstilo += puntosPorEnemigo * MultiplicadorCombo();
    }

    public void NotificarBreakableDestruido()
    {
        breakablesDestruidos++;
        AbrirVentanaCombo();
        puntosEstilo += puntosPorBreakable * MultiplicadorCombo();
    }

    // Llamalo desde tu Grapple cuando hagas algo “piola”
    // (cambiar de dirección fuerte, encadenar 2 anclajes rápido, etc).
    public void NotificarTrucoGrapple(int puntosBase = 15)
    {
        trucosGrapple++;
        AbrirVentanaCombo();
        int extra = usarCombo ? (_comboActual - 1) * bonusPorTrucoExtra : 0;
        puntosEstilo += (puntosBase + extra) * MultiplicadorCombo();
    }
}