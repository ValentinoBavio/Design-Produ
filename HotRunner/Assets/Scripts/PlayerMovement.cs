using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerMovement : MonoBehaviour
{
  


    private float moveSpeed;
    public float walkSpeed;
  

    public float groundDrag;

  
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

   

    /*

    private void Update()
    {
               
        if (grounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = 0;
        }

    }




    private void FixedUpdate()
    {
        MovePlayer();
    }








    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

       
        // en el pisoo
        if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        // en el aire
        else if (!grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        //gravedad en pendiente
        rb.useGravity = !OnSlope();
    }






    */












}
