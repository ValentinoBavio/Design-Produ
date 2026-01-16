using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LaptopRTInput : BaseInput
{
    [Header("Raycast al collider de la laptop")]
    public Camera camaraRaycast;
    public Collider pantallaCollider;

    [Header("RenderTexture destino (para convertir UV->pixeles)")]
    public RenderTexture renderTexture;

    float _blockPointerUntil;

    public void BlockPointer(float seconds)
    {
        _blockPointerUntil = Mathf.Max(_blockPointerUntil, Time.unscaledTime + seconds);
    }

    bool PointerBlocked => Time.unscaledTime < _blockPointerUntil;

    // =========================
    //  MOUSE (mapeado a RT)
    // =========================
    public override Vector2 mousePosition
    {
        get
        {
            if (PointerBlocked) return new Vector2(-99999, -99999);

            if (camaraRaycast == null || pantallaCollider == null || renderTexture == null)
                return Input.mousePosition; // fallback

            Ray ray = camaraRaycast.ScreenPointToRay(Input.mousePosition);
            if (pantallaCollider.Raycast(ray, out RaycastHit hit, 9999f))
            {
                Vector2 uv = hit.textureCoord;

                float px = uv.x * renderTexture.width;
                float py = uv.y * renderTexture.height;

                return new Vector2(px, py);
            }

            return new Vector2(-99999, -99999);
        }
    }

    public override bool mousePresent => true;

    public override bool GetMouseButtonDown(int button)
    {
        if (PointerBlocked) return false;
        return Input.GetMouseButtonDown(button);
    }

    public override bool GetMouseButtonUp(int button)
    {
        if (PointerBlocked) return false;
        return Input.GetMouseButtonUp(button);
    }

    public override bool GetMouseButton(int button)
    {
        if (PointerBlocked) return false;
        return Input.GetMouseButton(button);
    }

    // =========================
    //  NAVEGACIÓN (ejes)
    // =========================
    public override float GetAxisRaw(string axisName)
    {
        float v = 0f;

        // Intenta Input Manager (si existe)
        try { v = Input.GetAxisRaw(axisName); } catch { v = 0f; }

        // Fallback por si no tenés ejes configurados
        if (Mathf.Abs(v) < 0.001f)
        {
            if (axisName == "Horizontal")
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) return -1f;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) return 1f;
            }
            else if (axisName == "Vertical")
            {
                if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) return -1f;
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) return 1f;
            }
        }

        return v;
    }

    // =========================
    //  NAVEGACIÓN (Submit/Cancel)
    //  OJO: BaseInput en 2022.3 solo expone GetButtonDown()
    // =========================
    public override bool GetButtonDown(string buttonName)
    {
        bool b = false;
        try { b = Input.GetButtonDown(buttonName); } catch { b = false; }

        if (!b)
        {
            if (buttonName == "Submit")
                b = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);

            if (buttonName == "Cancel")
                b = Input.GetKeyDown(KeyCode.Escape);
        }

        return b;
    }
}