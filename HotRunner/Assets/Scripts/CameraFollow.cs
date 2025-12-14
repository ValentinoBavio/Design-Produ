using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraFollow : MonoBehaviour
{
    public float mouseSensitivity = 100f;
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

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

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

    void OnValidate()
    {
        if (yawRoot != null)
        {
            var e = yawRoot.eulerAngles;
            yawRoot.eulerAngles = new Vector3(0f, e.y, 0f);
        }
    }
}