using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public Transform yawRoot; // Asigná tu "Head" (padre de la cámara)

    [HideInInspector]
    public float roll = 0f;

    [Header("Start Options")]
    public bool recenterOnPlay = true; // mirar al horizonte al iniciar
    public bool consumeFirstMouseFrame = true; // evita tirón del lock de cursor

    float xRotation = 0f; // pitch
    bool firstMouseEaten = false;

    void OnEnable()
    {
        if (recenterOnPlay)
            SnapToHorizon();
        firstMouseEaten = false;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        if (recenterOnPlay)
            SnapToHorizon();
    }

    void Update()
    {
        // comer el primer frame de input para evitar salto de rotación
        if (consumeFirstMouseFrame && !firstMouseEaten)
        {
            Input.GetAxis("Mouse X");
            Input.GetAxis("Mouse Y");
            firstMouseEaten = true;
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // pitch + roll en la cámara
        transform.localRotation = Quaternion.Euler(xRotation, 0f, roll);

        // yaw en el "Head"
        if (yawRoot != null)
            yawRoot.Rotate(Vector3.up * mouseX, Space.World);
    }

    public void SnapToHorizon()
    {
        // pitch 0, roll 0 en la cámara
        xRotation = 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        // eliminar inclinación X/Z del Head; conservar yaw
        if (yawRoot != null)
        {
            var e = yawRoot.eulerAngles;
            yawRoot.rotation = Quaternion.Euler(0f, e.y, 0f);
        }
    }

    void OnValidate()
    {
        // En editor, evitar que el Head quede inclinado en X/Z por accidente
        if (yawRoot != null)
        {
            var e = yawRoot.eulerAngles;
            yawRoot.eulerAngles = new Vector3(0f, e.y, 0f);
        }
    }
}
