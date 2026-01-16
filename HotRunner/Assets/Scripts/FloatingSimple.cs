using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingSimple : MonoBehaviour
{
    [Header("Bobbing (sube/baja)")]
    public bool bobbing = true;
    public float amplitude = 0.25f;   // altura del vaivén
    public float frequency = 1.2f;    // velocidad del vaivén

    [Header("Rotación")]
    public bool rotate = true;
    public Vector3 rotationSpeed = new Vector3(0f, 35f, 0f); // grados/seg

    [Header("Offset opcional")]
    public bool useRandomStartOffset = true;

    Vector3 startPos;
    float tOffset;

    void Awake()
    {
        startPos = transform.localPosition;
        if (useRandomStartOffset) tOffset = Random.Range(0f, 1000f);
    }

    void Update()
    {
        // Subir/bajar
        if (bobbing)
        {
            float t = (Time.time + tOffset) * frequency;
            float y = Mathf.Sin(t) * amplitude;
            transform.localPosition = startPos + new Vector3(0f, y, 0f);
        }

        // Rotación suave
        if (rotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }

    // Si movés el objeto en el editor y querés que tome esa posición como base
    public void RecalibrarBase()
    {
        startPos = transform.localPosition;
    }
}
