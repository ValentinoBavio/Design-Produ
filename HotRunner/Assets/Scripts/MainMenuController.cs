using System.Collections;
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

    [Header("Evitar UI duplicada (cuando usás el rig)")]
    [Tooltip("Si laptopMenuRoot contiene un Canvas viejo con botones (INIT SESSION clásico), se apaga para que no se dispare LoadingScene por ese UI.")]
    public bool disableOtherCanvasesInLaptopMenuRootWhenUsingRig = true;

    [Header("Timing")]
    public float ignoreInputSeconds = 0.25f;
    public float afterBlendDelay = 0.10f;

    [Header("Evitar que Enter dispare Start")]
    [Tooltip("Tiempo extra de 'cooldown' antes de habilitar interacción del menú, por si venís de apretar Enter.")]
    public float menuInputGuardSeconds = 0.10f;

    [Header("Cursor")]
    public bool unlockCursorOnMenu = true;

    bool _transitioning;
    bool _inLaptop;
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

        _inLaptop = false;
        _transitioning = false;

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
        if (_inLaptop) return;
        if (_transitioning) return;
        if (Time.unscaledTime < _ignoreUntil) return;

        if (Input.anyKeyDown)
            StartCoroutine(CoGoToLaptop());
    }

    IEnumerator CoGoToLaptop()
    {
        _transitioning = true;

        if (pressAnyRoot) pressAnyRoot.SetActive(false);
        if (introRoot) introRoot.SetActive(false);

        if (vcamLaptop) vcamLaptop.Priority = laptopPriorityActive;

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

        // ✅ Si usás el rig, apagamos canvases viejos dentro de laptopMenuRoot (pero NO apagamos lo que cuelgue del rig).
        if (disableOtherCanvasesInLaptopMenuRootWhenUsingRig && laptopMenuRoot != null && laptopMenuRig != null)
        {
            var canvases = laptopMenuRoot.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null) continue;

                // Si este canvas es parte del rig, lo dejamos.
                if (c.transform.IsChildOf(laptopMenuRig.transform)) continue;

                // Apagamos canvas viejo (evita botón INIT SESSION duplicado que te manda a LoadingScene)
                c.gameObject.SetActive(false);
            }
        }

        // ✅ MOSTRAR menú usando el rig (incluye su propio guard de Submit)
        if (laptopMenuRig != null)
        {
            laptopMenuRig.MostrarMenu(true);

            if (menuInputGuardSeconds > 0f)
                yield return new WaitForSecondsRealtime(menuInputGuardSeconds);
        }

        if (unlockCursorOnMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        _inLaptop = true;
        _transitioning = false;
    }
}
