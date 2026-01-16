using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuNoMouseUI : MonoBehaviour
{
    [Header("Cursor")]
    public bool ocultarCursor = true;
    public bool lockCursor = false; // si lo lockeás, ojo con FPS scripts que dependan del mouse

    [Header("Bloquear interacción por mouse en UI")]
    public bool desactivarGraphicRaycasters = true;
    public bool desactivarPhysicsRaycasters = true;

    GraphicRaycaster[] _uiRaycasters;
    Behaviour[] _physicsRaycasters;

    void Awake()
    {
        if (ocultarCursor)
            Cursor.visible = false;

        if (lockCursor)
            Cursor.lockState = CursorLockMode.Locked;
        else
            Cursor.lockState = CursorLockMode.None;

        // Mantener navegación por teclado
        if (EventSystem.current)
            EventSystem.current.sendNavigationEvents = true;

        if (desactivarGraphicRaycasters)
        {
            _uiRaycasters = FindObjectsOfType<GraphicRaycaster>(true);
            for (int i = 0; i < _uiRaycasters.Length; i++)
                _uiRaycasters[i].enabled = false;
        }

        if (desactivarPhysicsRaycasters)
        {
            // PhysicsRaycaster está en UnityEngine.EventSystems, pero lo tratamos como Behaviour para no importar extra.
            // Desactiva clicks sobre UI/objetos por raycast desde cámara.
            _physicsRaycasters = FindObjectsOfType<Behaviour>(true);
            for (int i = 0; i < _physicsRaycasters.Length; i++)
            {
                if (_physicsRaycasters[i] == null) continue;
                var type = _physicsRaycasters[i].GetType().Name;
                if (type == "PhysicsRaycaster" || type == "Physics2DRaycaster")
                    _physicsRaycasters[i].enabled = false;
            }
        }
    }
}