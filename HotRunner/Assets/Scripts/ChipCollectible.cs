using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChipCollectible : MonoBehaviour
{
    [Header("Otorga")]
    public int cantidad = 1;

    [Header("Refs")]
    public ControladorJuego controlador;

    [Header("Pickup")]
    public bool destruirAlRecoger = true;

    bool recogido;

    void Awake()
    {
        if (!controlador) controlador = FindObjectOfType<ControladorJuego>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (recogido) return;

        // Soporta que el collider que entra sea un hijo del Player
        if (!other.CompareTag("Player") && other.GetComponentInParent<Transform>()?.CompareTag("Player") != true)
            return;

        recogido = true;

        if (controlador) controlador.SumarChips(cantidad);

        if (destruirAlRecoger)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}