using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class ResultadosNivelData
{
    public string nivelId;

    public float tiempoSeg;
    public string calificacionTexto;

    public int chipsRecolectados;
    public int multiplicador;
    public int chipsMultiplicados;

    public int chipsFijos;

    public int puntosEstilo;
    public int divisorEstilo;
    public int chipsPorEstilo;

    public bool secretoEncontrado;
    public string secretId;
    public int chipsSecreto;

    public int totalGanado;
    public int chipsTotalesAhora;
}
