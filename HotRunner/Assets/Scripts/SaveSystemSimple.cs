using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SaveSystemSimple
{
    const string KEY = "HOTRUNNER_SAVE_V1";

    public static GameProgressSave Cargar()
    {
        if (!PlayerPrefs.HasKey(KEY))
            return new GameProgressSave();

        string json = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(json))
            return new GameProgressSave();

        return JsonUtility.FromJson<GameProgressSave>(json) ?? new GameProgressSave();
    }

    public static void Guardar(GameProgressSave data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
    }
}