using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Calificacion
{
    SPlus, S, A, B, C, D, E, F
}

[CreateAssetMenu(menuName = "HotRunner/Calificacion/Nivel Config", fileName = "NivelCalificacionConfig")]
public class CalificacionNivelConfigSO : ScriptableObject
{
    [Header("ID del nivel (igual al que uses en tu sistema/escena)")]
    public string nivelId = "Nivel_1";

    [Header("Reglas por tiempo (si tiempo <= tiempoMaxSegundos => esa letra)")]
    public List<ReglaCalificacion> reglas = new List<ReglaCalificacion>();

    public ReglaCalificacion ObtenerRegla(float tiempoSegundos)
    {
        // Ordena mentalmente: de mejor a peor. En Inspector ponelas así.
        for (int i = 0; i < reglas.Count; i++)
        {
            if (tiempoSegundos <= reglas[i].tiempoMaxSegundos)
                return reglas[i];
        }

        // Si no entra en ninguna, devuelve la última (normalmente F).
        return reglas.Count > 0 ? reglas[reglas.Count - 1] : new ReglaCalificacion(Calificacion.F);
    }
}

[Serializable]
public class ReglaCalificacion
{
    public Calificacion calificacion = Calificacion.F;

    [Tooltip("Si el tiempo del nivel es <= a esto, aplica esta calificación.")]
    public float tiempoMaxSegundos = 99999f;

    [Header("Chips fijos por letra (S+ 100 / S 75 / A 50, etc)")]
    public int chipsFijos = 0;

    [Header("Multiplicador SOLO de chips recolectados en el nivel")]
    [Tooltip("S+ 5 / S 3 / A 2 / B-F 1")]
    public int multiplicadorChipsRecolectados = 1;

    [Header("Estilo → chips")]
    [Tooltip("S+ /2, S /3, A /4, B-F /10")]
    public int divisorEstilo = 10;

    public ReglaCalificacion(Calificacion c)
    {
        calificacion = c;
    }
}

public static class CalificacionUtils
{
    public static string ATexto(this Calificacion c)
    {
        return c == Calificacion.SPlus ? "S+" : c.ToString();
    }
}