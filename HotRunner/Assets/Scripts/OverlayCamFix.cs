using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverlayCamFix : MonoBehaviour
{
    [Tooltip("Quita este overlay del sistema de eventos de mouse.")]
    public bool disableMouseEvents = true;

    [Tooltip("Evita que esta cámara pinte la capa UI.")]
    public bool stripUILayer = true;

    [Tooltip("Ajustes típicos de cámaras overlay.")]
    public bool applyOverlayDefaults = true;

    void OnEnable()
    {
        var cam = GetComponent<Camera>();

        if (disableMouseEvents)
        {
            cam.eventMask = 0;
        }

        if (stripUILayer)
        {
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) cam.cullingMask &= ~(1 << ui);
        }

        if (applyOverlayDefaults)
        {
            if (cam.clearFlags == CameraClearFlags.Skybox)
                cam.clearFlags = CameraClearFlags.Depth;
            cam.nearClipPlane = Mathf.Max(0.01f, cam.nearClipPlane);
            var al = GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }
    }
}