using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class MainMenuIntroController : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineVirtualCamera vcamIntro;
    public CinemachineVirtualCamera vcamLaptop;
    public CinemachineBrain brain;

    [Header("Prioridades")]
    public int introPriority = 20;
    public int laptopPriorityIdle = 10;
    public int laptopPriorityActive = 40;

    [Header("UI / Objetos a prender y apagar")]
    [Tooltip("Raíz del UI/objetos de intro (título + press any).")]
    public GameObject introRoot;

    [Tooltip("Si tu 'Press any...' NO está dentro de introRoot, arrastralo acá para apagarlo igual.")]
    public GameObject pressAnyRoot;

    [Tooltip("Raíz del menú en la laptop (si tenés un root para activar).")]
    public GameObject laptopMenuRoot;

    [Tooltip("Si usás el rig por script (RenderTexture menu), arrastralo acá para mostrarlo bien.")]
    public LaptopMenuRenderTextureRig laptopMenuRig;

    [Header("Timing")]
    public float ignoreInputSeconds = 0.25f;
    public float afterBlendDelay = 0.10f;

    [Header("Evitar que Enter dispare Start")]
    [Tooltip("Tiempo extra de 'cooldown' antes de habilitar interacción del menú, por si venís de apretar Enter.")]
    public float menuInputGuardSeconds = 0.10f;

    [Header("Cursor")]
    public bool unlockCursorOnMenu = true;

    bool _transitioning;
    float _ignoreUntil;

    void Awake()
    {
        if (!brain)
        {
            var cam = Camera.main;
            if (cam) brain = cam.GetComponent<CinemachineBrain>();
        }

        if (vcamIntro)  vcamIntro.Priority  = introPriority;
        if (vcamLaptop) vcamLaptop.Priority = laptopPriorityIdle;

        if (introRoot) introRoot.SetActive(true);
        if (pressAnyRoot && pressAnyRoot != introRoot) pressAnyRoot.SetActive(true);

        // OJO: si el rig está dentro de laptopMenuRoot, NO lo apagues acá (se corta el Awake del rig).
        if (laptopMenuRig == null && laptopMenuRoot)
            laptopMenuRoot.SetActive(false);

        if (laptopMenuRig == null)
            laptopMenuRig = FindObjectOfType<LaptopMenuRenderTextureRig>(true);

        // si hay rig, lo ocultamos pero lo dejamos vivo
        if (laptopMenuRig != null)
            laptopMenuRig.OcultarMenu(false);

        if (unlockCursorOnMenu)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Start()
    {
        _ignoreUntil = Time.unscaledTime + Mathf.Max(0f, ignoreInputSeconds);
    }

    void Update()
    {
        if (_transitioning) return;
        if (Time.unscaledTime < _ignoreUntil) return;

        // Esto detecta cualquier tecla/botón/mouse.
        if (Input.anyKeyDown)
            StartCoroutine(CoGoToLaptop());
    }

    IEnumerator CoGoToLaptop()
    {
        _transitioning = true;

        // ✅ apagar “Press any” sí o sí primero (aunque esté en otro canvas)
        if (pressAnyRoot) pressAnyRoot.SetActive(false);

        // ✅ apagar todo el intro (título + lo que sea)
        if (introRoot) introRoot.SetActive(false);

        // cambiar a cámara laptop
        if (vcamLaptop) vcamLaptop.Priority = laptopPriorityActive;

        // esperar blend
        if (brain != null)
        {
            yield return null;
            float safety = 6f;
            while (brain.ActiveBlend != null && safety > 0f)
            {
                safety -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }

        if (afterBlendDelay > 0f)
            yield return new WaitForSecondsRealtime(afterBlendDelay);

        // prender root si lo usás
        if (laptopMenuRoot) laptopMenuRoot.SetActive(true);

        // ✅ MOSTRAR menú usando el rig (incluye su propio guard de Submit)
        if (laptopMenuRig != null)
        {
            laptopMenuRig.MostrarMenu(true);

            // Guard extra para que Enter no dispare Start por rebote (por si tu input module mete Submit)
            if (menuInputGuardSeconds > 0f)
                yield return new WaitForSecondsRealtime(menuInputGuardSeconds);
        }

        if (unlockCursorOnMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        _transitioning = false;
    }
}