using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetaFinal : MonoBehaviour
{
    [SerializeField] private ControladorJuego controladorJuego;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            controladorJuego.DesactivarTemporizador();
            Debug.Log("Ganaste!");
            Destroy(gameObject);
        }
    }
}
