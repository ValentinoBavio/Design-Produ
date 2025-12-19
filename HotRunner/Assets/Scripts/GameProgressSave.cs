using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameProgressSave
{
    public int chipsTotales = 0;

    // Mejor calificación por nivel (texto: "S+", "A", etc)
    public List<NivelRecord> records = new List<NivelRecord>();

    // Coleccionables secretos encontrados
    public List<string> secretosEncontrados = new List<string>();
}

[Serializable]
public class NivelRecord
{
    public string nivelId;
    public string mejorCalificacion;
    public float mejorTiempo;

    public bool secretoEncontrado;
}