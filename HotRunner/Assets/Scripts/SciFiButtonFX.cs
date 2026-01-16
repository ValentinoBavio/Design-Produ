using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SciFiButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Refs")]
    public RectTransform target;
    public Image fondo;
    public TMP_Text label;
    public Image barraAcentoIzq;
    public Image underline;
    public TMP_Text chevron;

    [Header("Owner (opcional)")]
    public LaptopMenuRenderTextureRig owner;

    [Header("Colores")]
    public Color uiCian = new Color(0f, 0.898f, 1f, 1f);
    public Color textoCianSuave = new Color(0.482f, 0.937f, 1f, 1f);
    public Color seleccion = new Color(1f, 0.690f, 0f, 1f);

    [Header("Comportamiento")]
    [Tooltip("✅ Dejalo en true para que el mouse NO te rompa la navegación con teclado.")]
    public bool noForzarSeleccionEnHover = true;

    bool _hover;
    bool _selected;

    void Reset()
    {
        target = GetComponent<RectTransform>();
        fondo = GetComponent<Image>();
    }

    void OnEnable()
    {
        // evita “estado pegado” si el GO vuelve a activarse sin eventos de salida
        _hover = false;
        RefreshVisual();
    }

    void OnDisable()
    {
        // ✅ IMPORTANTÍSIMO: Unity a veces no manda PointerExit/Deselect al desactivar panels
        _hover = false;
        _selected = false;
        RefreshVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // ✅ si el rig está bloqueando hover (modo teclado / al volver de paneles), ignorá el enter
        if (owner != null && (owner.MainHoverBlocked || owner.OptionsHoverBlocked))
            return;

        _hover = true;
        RefreshVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // ❗NO bloquear el Exit: lo usamos para “limpiar” hover a la fuerza desde el rig
        _hover = false;
        RefreshVisual();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        _hover = false; // evita mezcla rara hover+selected
        RefreshVisual();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        RefreshVisual();
    }

    void RefreshVisual()
    {
        bool hi = _selected || _hover;

        if (fondo)
        {
            fondo.color = hi
                ? new Color(0.02f, 0.10f, 0.14f, 0.92f)
                : new Color(0.02f, 0.10f, 0.14f, 0.75f);
        }

        if (label)
            label.color = _selected ? seleccion : textoCianSuave;

        if (barraAcentoIzq)
        {
            var c = _selected ? seleccion : uiCian;
            c.a = _selected ? 0.75f : 0.45f;
            barraAcentoIzq.color = c;
        }

        if (underline)
        {
            var c = _selected ? seleccion : uiCian;
            c.a = _selected ? 0.35f : (_hover ? 0.18f : 0.08f);
            underline.color = c;
        }

        if (chevron)
        {
            var c = _selected ? seleccion : uiCian;
            c.a = _selected ? 0.85f : (_hover ? 0.55f : 0.35f);
            chevron.color = c;
        }

        if (target)
            target.localScale = hi ? Vector3.one * 1.01f : Vector3.one;
    }
}
