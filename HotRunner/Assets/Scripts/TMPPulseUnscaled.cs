using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class TMPPulseUnscaled : MonoBehaviour
{
    [Header("Pulse")]
    public bool pulseScale = true;
    public float scaleAmp = 0.08f;      // 0.05 - 0.12
    public float scaleSpeed = 2.5f;     // 1.5 - 4

    public bool pulseAlpha = false;     // si querés también parpadeo suave
    public float alphaMin = 0.55f;
    public float alphaMax = 1.0f;
    public float alphaSpeed = 2.0f;

    TMP_Text _txt;
    Vector3 _scale0;
    Color _color0;

    void Awake()
    {
        _txt = GetComponent<TMP_Text>(); // sirve para TextMeshProUGUI y TextMeshPro
        _scale0 = transform.localScale;

        if (_txt != null) _color0 = _txt.color;
    }

    void OnEnable()
    {
        // reseteo por si venías de otra escena/estado
        transform.localScale = _scale0;
        if (_txt != null) _txt.color = _color0;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (pulseScale)
        {
            float s = 1f + Mathf.Sin(t * scaleSpeed * Mathf.PI * 2f) * scaleAmp;
            transform.localScale = _scale0 * s;
        }

        if (pulseAlpha && _txt != null)
        {
            float k = (Mathf.Sin(t * alphaSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            float a = Mathf.Lerp(alphaMin, alphaMax, k);

            Color c = _txt.color;
            c.a = a;
            _txt.color = c;
        }
    }
}