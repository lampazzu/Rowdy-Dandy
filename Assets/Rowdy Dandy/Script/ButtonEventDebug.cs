using System;
using System.Collections.Generic;
using UnityEngine;

public class ButtonEventDebug : MonoBehaviour
{
    [Serializable]
    public struct ButtonEventPair
    {
        public KeyCode button;
        public UnityEventWrapper.UnityEventWithKeyCode unityEvent;
    }

    [Serializable]
    public class UnityEventWrapper
    {
        [Serializable]
        public class UnityEventWithKeyCode : UnityEngine.Events.UnityEvent<KeyCode> { }

        public UnityEventWithKeyCode[] events;
    }

    public List<ButtonEventPair> buttonEventPairs = new List<ButtonEventPair>();

    private void Update()
    {
        foreach (var pair in buttonEventPairs)
        {
            if (GameInput.KeyDown(pair.button))
            {
                pair.unityEvent.Invoke(pair.button);
                Debug.Log("Button " + pair.button + " pressed!");
            }
        }
    }
}
