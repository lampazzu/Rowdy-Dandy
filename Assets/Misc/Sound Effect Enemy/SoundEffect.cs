using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class SoundEffect : MonoBehaviour
{
    // Serialized UnityEvent
    [SerializeField] public UnityEvent onWolfAttack;

    private void OnEnable()
    {
        // Invoking the UnityEvent when the script is enabled
        onWolfAttack.Invoke();
        PlayUnboundSounds(onWolfAttack);
    }

    // Enemy prefabs can't point at the SoundManager (it lives inside Rowdy), so their "SoundManager.PlaySound(name)"
    // calls are saved with an empty target and do nothing on spawned enemies. Play those names here instead.
    // Also covers a call that was wired to an AudioClip's set_name by mistake (Transform Wolf's "WolfAttack").
    // Used by EnemyHealth's hurt / kill events too.
    public static void PlayUnboundSounds(UnityEventBase unityEvent)
    {
        if (SoundManager.Instance == null || unityEvent == null) return;
        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            Object target = unityEvent.GetPersistentTarget(i);
            string method = unityEvent.GetPersistentMethodName(i);
            bool unboundPlay = target == null && method == "PlaySound";
            bool misWired = target is AudioClip && method == "set_name";
            if (!unboundPlay && !misWired) continue;

            string soundName = StringArgument(unityEvent, i);
            if (string.IsNullOrEmpty(soundName) || SceneOnly.Contains(soundName)) continue;
            SoundManager.Instance.PlaySound(soundName);
        }
    }

    // Never revived on spawned enemies: "CriticalHit" is the old man's scream, the prefabs only reference it by accident
    private static readonly HashSet<string> SceneOnly = new HashSet<string> { "CriticalHit" };

    private static FieldInfo callsField, callListField, argumentsField, stringField;

    private static string StringArgument(UnityEventBase unityEvent, int index)
    {
        try
        {
            if (callsField == null)
            {
                callsField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);
                callListField = callsField.FieldType.GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            object group = callsField.GetValue(unityEvent);
            var list = callListField.GetValue(group) as IList;
            object call = list[index];
            if (argumentsField == null)
            {
                argumentsField = call.GetType().GetField("m_Arguments", BindingFlags.Instance | BindingFlags.NonPublic);
                stringField = argumentsField.FieldType.GetField("m_StringArgument", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            return stringField.GetValue(argumentsField.GetValue(call)) as string;
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
