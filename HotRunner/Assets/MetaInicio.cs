using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetaInicio : MonoBehaviour
{
    [SerializeField] private ControladorJuego controladorJuego;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            controladorJuego.ActivarTemporizador();
            Destroy(gameObject);
        }
    }
}
