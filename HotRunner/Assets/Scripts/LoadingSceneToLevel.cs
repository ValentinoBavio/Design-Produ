using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneToLevel : MonoBehaviour
{
    public string targetSceneName = "Level1";
    public float minShowTime = 0.6f;

    void Start()
    {
        StartCoroutine(CoLoad());
    }

    IEnumerator CoLoad()
    {
        float t0 = Time.unscaledTime;

        var op = SceneManager.LoadSceneAsync(targetSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        // aseguramos que se vea un toque la loading scene
        while (Time.unscaledTime - t0 < minShowTime)
            yield return null;

        op.allowSceneActivation = true;
    }
}