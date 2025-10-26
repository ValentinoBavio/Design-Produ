using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeadzoneTrigger : MonoBehaviour
{
    [Tooltip("Usar tag del Player para filtrar (vacío = cualquiera).")]
    public string requiredTag = "Player";

    [Tooltip("Retraso opcional antes de recargar (segundos).")]
    public float delay = 0f;

    bool reloading;

    void OnTriggerEnter(Collider other)
    {
        if (reloading) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        reloading = true;
        if (delay <= 0f)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        else
            Invoke(nameof(DoReload), delay);
    }

    void DoReload()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}