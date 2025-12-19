using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecurityCamTour : MonoBehaviour
{
    [Header("Cámara a mover")]
    public Camera cam;

    [Header("Puntos de vista (Transforms en la escena)")]
    public List<Transform> puntos = new List<Transform>();

    [Header("Movimiento")]
    public float durMoverSeg = 2.0f;
    public float esperarEnPuntoSeg = 1.0f;

    [Header("Paneo tipo cámara vieja")]
    public bool panear = true;
    public float panAngulo = 15f;
    public float panVelocidad = 1.2f;

    [Header("Activación")]
    public bool activoAlInicio = false;

    Coroutine _rutina;
    int _idx;

    private void Awake()
    {
        Activar(activoAlInicio);
    }

    public void Activar(bool on)
    {
        if (cam) cam.enabled = on;
        gameObject.SetActive(on);

        if (!on)
        {
            if (_rutina != null) StopCoroutine(_rutina);
            _rutina = null;
            return;
        }

        if (_rutina != null) StopCoroutine(_rutina);
        _rutina = StartCoroutine(Rutina());
    }

    IEnumerator Rutina()
    {
        if (puntos == null || puntos.Count == 0)
            yield break;

        _idx = Mathf.Clamp(_idx, 0, puntos.Count - 1);

        // arrancar en el primer punto
        Transform t0 = puntos[_idx];
        transform.position = t0.position;
        transform.rotation = t0.rotation;

        while (true)
        {
            Transform desde = puntos[_idx];
            _idx = (_idx + 1) % puntos.Count;
            Transform hacia = puntos[_idx];

            yield return MoverSuave(desde, hacia, durMoverSeg);
            yield return MirarConPaneo(hacia, esperarEnPuntoSeg);
        }
    }

    IEnumerator MoverSuave(Transform a, Transform b, float dur)
    {
        float t = 0f;
        Vector3 p0 = a.position;
        Quaternion r0 = a.rotation;

        Vector3 p1 = b.position;
        Quaternion r1 = b.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dur);
            transform.position = Vector3.Lerp(p0, p1, t);
            transform.rotation = Quaternion.Slerp(r0, r1, t);
            yield return null;
        }

        transform.position = p1;
        transform.rotation = r1;
    }

    IEnumerator MirarConPaneo(Transform punto, float dur)
    {
        float t = 0f;
        Quaternion baseRot = punto.rotation;

        while (t < dur)
        {
            t += Time.deltaTime;

            if (panear)
            {
                float s = Mathf.Sin(Time.time * panVelocidad);
                Quaternion pan = Quaternion.Euler(0f, s * panAngulo, 0f);
                transform.rotation = baseRot * pan;
            }
            else
            {
                transform.rotation = baseRot;
            }

            yield return null;
        }
    }
}
