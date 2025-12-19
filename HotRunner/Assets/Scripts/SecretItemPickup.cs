using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecretItemPickup : MonoBehaviour
{
    public string secretId = "Secreto_01";
    public int chipsBonus = 50;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var end = FindObjectOfType<LevelEndManager>();
        if (end != null)
        {
            end.MarcarSecretoEncontrado(secretId, chipsBonus);
        }

        Destroy(gameObject);
    }
}