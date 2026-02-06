using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraFollow : MonoBehaviour
{
    [Header("Sensibilidad")]
    [Tooltip("Sensibilidad REAL usada por el look. 100 = 100%.")]
    public float mouseSensitivity = 100f;

    [Tooltip("Valor NORMALIZADO (1.00 = 100%). Se usa para UI/PlayerPrefs.")]
    [SerializeField] private float mouseSensitivityNormalized = 1f;

    public Transform yawRoot; // Head (padre de la cámara)

    [HideInInspector] public float roll = 0f;

    [Header("Start Options")]
    public bool recenterOnPlay = true;
    public bool consumeFirstMouseFrame = true;

    [Header("Start Patch")]
    public int recenterFrames = 2;

    [Tooltip("De dónde sacar el yaw inicial. Si está vacío, usa el padre del yawRoot (Player).")]
    public Transform yawReference;

    [Tooltip("Offset extra en grados. Si arranca mirando atrás, poné 180.")]
    public float startYawOffsetDeg = 0f;

    [Header("Opcional - Permitir look con pausa")]
    [Tooltip("Si Time.timeScale = 0 y esto está ON, usa unscaledDeltaTime para poder mover la cámara.")]
    public bool allowLookWhilePaused = false;

    float xRotation = 0f;
    bool firstMouseEaten = false;
    int _forceLateFrames = 0;

    void OnEnable()
    {
        firstMouseEaten = false;

        if (recenterOnPlay)
        {
            _forceLateFrames = Mathf.Max(1, recenterFrames);
            StartCoroutine(SnapAfterFrame());
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // Asegura consistencia entre normalized y real al inicio
        ApplyNormalizedToReal();
    }

    IEnumerator SnapAfterFrame()
    {
        yield return null; // 1 frame
        SnapToHorizon();
    }

    void Update()
    {
        if (consumeFirstMouseFrame && !firstMouseEaten)
        {
            Input.GetAxis("Mouse X");
            Input.GetAxis("Mouse Y");
            firstMouseEaten = true;
            return;
        }

        // Si estás en pausa y querés permitir look, usamos unscaledDeltaTime
        float dt = Time.deltaTime;
        if (Time.timeScale <= 0.0001f && allowLookWhilePaused)
            dt = Time.unscaledDeltaTime;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * dt;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * dt;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (yawRoot != null)
            yawRoot.Rotate(Vector3.up * mouseX, Space.World);
    }

    void LateUpdate()
    {
        if (_forceLateFrames > 0)
        {
            SnapToHorizon();
            _forceLateFrames--;
            return;
        }

        transform.localRotation = Quaternion.Euler(xRotation, 0f, roll);
    }

    public void SnapToHorizon()
    {
        // cámara: pitch 0, roll 0
        xRotation = 0f;
        transform.localRotation = Quaternion.identity;

        if (yawRoot != null)
        {
            // referencia de yaw (idealmente el Player)
            Transform refT = yawReference;
            if (!refT && yawRoot.parent) refT = yawRoot.parent;

            float targetYaw = (refT ? refT.eulerAngles.y : yawRoot.eulerAngles.y) + startYawOffsetDeg;
            yawRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
        }
    }

    // ==============================
    //  API para el PauseMenu (UI)
    // ==============================
    /// <summary>
    /// normalized01: 1.00 = 100% (sensibilidad 100).
    /// Ej: 0.50 = 50 (50%), 2.00 = 200 (200%).
    /// </summary>
    public void SetMouseSensitivity(float normalized01)
    {
        mouseSensitivityNormalized = Mathf.Clamp(normalized01, 0.05f, 5f);
        ApplyNormalizedToReal();
    }

    /// <summary>Devuelve el valor normalizado actual (1.00 = 100%).</summary>
    public float GetMouseSensitivityNormalized()
    {
        return mouseSensitivityNormalized;
    }

    void ApplyNormalizedToReal()
    {
        // 1.00 => 100, 1.25 => 125, etc.
        mouseSensitivity = mouseSensitivityNormalized * 100f;
    }

    void OnValidate()
    {
        if (yawRoot != null)
        {
            var e = yawRoot.eulerAngles;
            yawRoot.eulerAngles = new Vector3(0f, e.y, 0f);
        }

        // Mantener coherencia en editor cuando tocás valores
        if (mouseSensitivity < 0f) mouseSensitivity = 0f;

        // Si editás mouseSensitivity a mano, aproximamos el normalized
        mouseSensitivityNormalized = Mathf.Clamp(mouseSensitivity / 100f, 0.05f, 5f);
    }
}
