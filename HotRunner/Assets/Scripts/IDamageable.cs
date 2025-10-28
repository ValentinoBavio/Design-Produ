using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    // Devuelve cuánto daño fue realmente aplicado (por si hay resistencias)
    float ApplyDamage(DamageMessage msg);
}

public struct DamageMessage
{
    public float amount;
    public Vector3 point;        // punto de impacto del raycast
    public Vector3 normal;       // normal de la superficie
    public Vector3 direction;    // dirección del disparo
    public GameObject source;    // quien hizo el daño (arma / jugador)
}