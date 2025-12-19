using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina (0..1)")]
    [Range(0f, 1f)] [SerializeField] private float stamina = 1f;

    public float Value01 => stamina;
    public bool IsEmpty => stamina <= 0.0001f;

    public event Action<float> OnChanged; // manda Value01

    void Awake()
    {
        stamina = Mathf.Clamp01(stamina);
    }

    public void SetFull()
    {
        stamina = 1f;
        OnChanged?.Invoke(stamina);
    }

    public void SetEmpty()
    {
        stamina = 0f;
        OnChanged?.Invoke(stamina);
    }

    public void Set01(float v01)
    {
        stamina = Mathf.Clamp01(v01);
        OnChanged?.Invoke(stamina);
    }

    public bool TrySpend01(float amount01)
    {
        amount01 = Mathf.Max(0f, amount01);
        if (stamina + 0.0001f < amount01) return false;

        stamina = Mathf.Clamp01(stamina - amount01);
        OnChanged?.Invoke(stamina);
        return true;
    }

    public void Drain01(float amount01)
    {
        if (amount01 <= 0f) return;
        stamina = Mathf.Clamp01(stamina - amount01);
        OnChanged?.Invoke(stamina);
    }

    public void Add01(float amount01)
    {
        if (amount01 <= 0f) return;
        stamina = Mathf.Clamp01(stamina + amount01);
        OnChanged?.Invoke(stamina);
    }
}