// AnimatorHelper.cs
using UnityEngine;

public class AnimatorHelper : MonoBehaviour
{
    // Check if the parameter exists in the Animator
    public static bool HasParameter(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }
}
