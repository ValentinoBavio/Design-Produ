using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaminaUI_FrontBack : MonoBehaviour
{
    [Header("Refs")]
    public PlayerStamina stamina;
    public Image frontFill; // Filled
    public Image backFill;  // Filled

    [Header("Animación")]
    public float frontLerpSpeed = 10f;
    public float backDelay = 0.25f;
    public float backLerpSpeed = 2f;

    float _front = 1f;
    float _back  = 1f;
    float _target = 1f;
    float _backDelayUntil = 0f;

    void OnEnable()
    {
        if (stamina) stamina.OnChanged += OnStaminaChanged;
        if (stamina) OnStaminaChanged(stamina.Value01);
    }

    void OnDisable()
    {
        if (stamina) stamina.OnChanged -= OnStaminaChanged;
    }

    void OnStaminaChanged(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        // si sube, snap ambos para que no “quede back abajo”
        if (v01 > _target + 0.0001f)
        {
            _front = v01;
            _back  = v01;
        }
        else if (v01 < _target - 0.0001f)
        {
            _backDelayUntil = Time.time + Mathf.Max(0f, backDelay);
        }

        _target = v01;
        Apply();
    }

    void Update()
    {
        if (!frontFill && !backFill) return;

        float dt = Time.deltaTime;

        _front = Mathf.MoveTowards(_front, _target, Mathf.Max(0.01f, frontLerpSpeed) * dt);

        if (_back < _target) _back = _target;
        else
        {
            if (Time.time >= _backDelayUntil)
                _back = Mathf.MoveTowards(_back, _target, Mathf.Max(0.01f, backLerpSpeed) * dt);
        }

        Apply();
    }

    void Apply()
    {
        if (frontFill) frontFill.fillAmount = _front;
        if (backFill)  backFill.fillAmount  = _back;
    }
}