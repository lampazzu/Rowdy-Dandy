using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackToIdle : MonoBehaviour
{

   private Animator anim;
   // Not every Animator using this has both triggers, so only touch the ones that exist
   private bool hasMeleeAttack;
   private bool hasBackToIdle;

         private void Awake()
    {
        anim = GetComponent<Animator>();

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in anim.parameters)
            {
                if (parameter.name == "meleeAttack") hasMeleeAttack = true;
                if (parameter.name == "back to idle") hasBackToIdle = true;
            }
        }
    }



    private void Update()
    {
        if (hasMeleeAttack) anim.ResetTrigger("meleeAttack");
        if (hasBackToIdle) anim.SetTrigger("back to idle");

    }


}
