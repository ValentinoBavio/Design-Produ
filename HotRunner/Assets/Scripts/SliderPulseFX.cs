using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover/Select pulse para sliders (SIN cambiar tamaño).
/// </summary>
public class SliderPulseFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IBeginDragHandler,
    ISelectHandler, IDeselectHandler
{
    public LaptopMenuRenderTextureRig owner;
    public int slot;

    public EventSystem eventSystem;
    public Selectable selectable;

    public TextMeshProUGUI label;
    public Image bar;
    public Image fill;
    public Image handle;

    public Color baseText;
    public Color hiText;
    public Color baseFill;
    public Color hiFill;
    public Color baseBar;
    public Color hiBar;

    public bool enabledPulse = true;
    public float pulseAmount = 0.35f;
    public float pulseSpeed = 5.5f;

    bool _hover;
    bool _selected;

    void Awake() => Apply(false);

    void Update()
    {
        if (!enabledPulse) return;
        if (!(_hover || _selected)) return;

        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f);
        float k = Mathf.Lerp(1f - pulseAmount, 1f, t);

        if (fill != null) fill.color = Color.Lerp(baseFill, hiFill, k);
        if (bar != null) bar.color = Color.Lerp(baseBar, hiBar, k);
        // handle lo dejamos fijo (como lo creás en el rig) para no cambiar el look
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null && owner.OptionsHoverBlocked) return;
        _hover = true;
        if (!_selected) Apply(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null && owner.OptionsHoverBlocked) return;
        _hover = false;
        if (!_selected) Apply(false);
    }

    public void OnPointerDown(PointerEventData eventData) => ForceSelectNow();
    public void OnBeginDrag(PointerEventData eventData) => ForceSelectNow();

    void ForceSelectNow()
    {
        if (owner != null && owner.OptionsHoverBlocked) return;

        if (eventSystem != null && selectable != null)
            eventSystem.SetSelectedGameObject(selectable.gameObject);

        owner?.NotifyOptionsSelected(selectable, slot);
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        _hover = false;
        Apply(true);
        owner?.NotifyOptionsSelected(selectable, slot);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        if (!_hover) Apply(false);
    }

    public void ForceHoverOffOnly()
    {
        _hover = false;
        if (!_selected) Apply(false);
    }

    void Apply(bool active)
    {
        if (label != null) label.color = active ? hiText : baseText;
        if (fill != null) fill.color = active ? hiFill : baseFill;
        if (bar != null) bar.color = active ? hiBar : baseBar;
    }
}