using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaptopFocusLoopDetector : MonoBehaviour
{
    [Header("Refs")]
    public MainMenuAudio audioBus;
    public Camera cam;

    [Header("Qué cuenta como 'laptop'")]
    public LayerMask laptopMask;
    public string laptopTag = ""; // opcional, dejalo vacío si usás layer

    [Header("Raycast")]
    public float maxDist = 6f;
    [Range(0f, 8f)] public float centerTolerancePx = 60f; // permite no estar EXACTO en el centro

    bool _isOn;

    void Reset()
    {
        cam = Camera.main;
        // laptopMask lo seteás a mano en el inspector
    }

    void Update()
    {
        if (!audioBus || !cam) return;

        // Ray al centro (con tolerancia)
        Vector3 center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        // Si querés tolerancia, elegimos un puntito random dentro del radio para “suavizar”
        if (centerTolerancePx > 0f)
        {
            Vector2 r = Random.insideUnitCircle * centerTolerancePx;
            center.x += r.x;
            center.y += r.y;
        }

        Ray ray = cam.ScreenPointToRay(center);

        bool hitLaptop = false;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, laptopMask, QueryTriggerInteraction.Ignore))
        {
            if (string.IsNullOrEmpty(laptopTag))
                hitLaptop = true;
            else
                hitLaptop = hit.collider.CompareTag(laptopTag);
        }

        if (hitLaptop != _isOn)
        {
            _isOn = hitLaptop;
            audioBus.SetFocusLaptopLoop(_isOn);
        }
    }
}