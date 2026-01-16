using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelCameraEnforcer : MonoBehaviour
{
    [Header("Opciones")]
    [Tooltip("Si está activado, este script también deja activo SOLO un AudioListener (evita el warning de 2 listeners).")]
    public bool forzarUnSoloAudioListener = true;

    [Tooltip("Si está activado, fuerza que solo exista UNA cámara activa (la que tenga Tag MainCamera).")]
    public bool forzarSoloMainCamera = true;

    void Awake()
    {
        // 1) Asegurar que exista Camera.main (tag MainCamera) en esta escena
        Camera main = Camera.main;
        if (main == null)
        {
            Debug.LogWarning("[LevelCameraEnforcer] No hay ninguna cámara con Tag 'MainCamera' en esta escena. " +
                             "Asignale el Tag MainCamera a la cámara del nivel (Level1Camera).");
        }

        // 2) Desactivar cualquier cámara extra que haya quedado viva (menú/loading)
        if (forzarSoloMainCamera && main != null)
        {
            Camera[] cams = FindObjectsOfType<Camera>(true); // incluye inactivas
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] == null) continue;
                bool esMain = (cams[i] == main);
                cams[i].enabled = esMain;

                // Si alguna cámara extra tenía AudioListener, lo apagamos también
                if (forzarUnSoloAudioListener)
                {
                    var al = cams[i].GetComponent<AudioListener>();
                    if (al != null) al.enabled = esMain;
                }
            }
        }

        // 3) Opcional: si hay AudioListeners sueltos fuera de cámaras, dejar solo 1
        if (forzarUnSoloAudioListener)
        {
            var listeners = FindObjectsOfType<AudioListener>(true);
            if (listeners.Length > 1)
            {
                // Dejamos el del MainCamera si existe; si no, dejamos el primero habilitado
                AudioListener mainListener = null;
                if (Camera.main != null) mainListener = Camera.main.GetComponent<AudioListener>();

                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] == null) continue;
                    if (mainListener != null)
                        listeners[i].enabled = (listeners[i] == mainListener);
                    else
                        listeners[i].enabled = (i == 0);
                }
            }
        }
    }
}