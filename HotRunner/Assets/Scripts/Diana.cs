using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Diana : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // tuve que hacer publico la funcion StopGrapple porque sino se queda enganchado a un objeto destruido pero asi quedo de momento
            GrappleGun gancho = FindObjectOfType<GrappleGun>();
            if (gancho != null)
            {                
                if (gancho && gancho.enabled)
                {
                    gancho.StopGrapple();
                }
            }
            
            foreach (Transform hijo in transform)
            {
                Destroy(hijo.gameObject);
            }
            
            Destroy(gameObject);
        }
    }
}
