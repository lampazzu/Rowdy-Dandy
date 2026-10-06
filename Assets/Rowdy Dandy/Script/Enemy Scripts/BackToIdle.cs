using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackToIdle : MonoBehaviour
{
    
   private Animator anim;

         private void Awake()
    {
        anim = GetComponent<Animator>();
    }



    private void Update()
    {
        anim.ResetTrigger("meleeAttack");
        anim.SetTrigger("back to idle");

    }


}
