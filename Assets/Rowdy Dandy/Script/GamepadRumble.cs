using UnityEngine;
using UnityEngine.InputSystem;

// Controller rumble (Input System: Xbox, PlayStation, Switch... the pad played with last). One shared runner owns the motors and always switches them off when
// the pulse ends, so a rumble can't get stuck on when the enemy that started it is hit again, dies or despawns.
// Also stops while the game is paused or loses focus.
public class GamepadRumble : MonoBehaviour
{
    private const float MaxPulse = 0.5f; // no single pulse longer than this, whatever the caller asks

    private static GamepadRumble instance;
    private float stopAt;
    private bool running;

    public static void Pulse(float lowMotor, float highMotor, float duration)
    {
        if (!GameSettings.Vibration || duration <= 0f) return;
        if (instance == null)
        {
            var go = new GameObject("GamepadRumble (auto)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GamepadRumble>();
        }
        instance.stopAt = Mathf.Max(instance.running ? instance.stopAt : 0f, Time.unscaledTime + Mathf.Min(duration, MaxPulse));
        instance.running = true;
        Set(lowMotor, highMotor);
    }

    public static void StopAll()
    {
        if (instance != null) instance.running = false;
        Set(0f, 0f);
    }

    private void Update()
    {
        if (!running) return;
        if (Time.unscaledTime >= stopAt || PauseMenu.IsPaused || !GameSettings.Vibration) StopAll();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) StopAll();
    }

    private void OnApplicationQuit() => Set(0f, 0f);
    private void OnDestroy() => Set(0f, 0f);

    // Low-frequency (left, heavy) and high-frequency (right, buzzy) motors, 0-1. Stopping switches every pad off.
    private static void Set(float leftMotor, float rightMotor)
    {
        leftMotor = Mathf.Clamp01(leftMotor);
        rightMotor = Mathf.Clamp01(rightMotor);
        if (leftMotor <= 0f && rightMotor <= 0f)
        {
            foreach (Gamepad pad in Gamepad.all) pad.SetMotorSpeeds(0f, 0f);
            return;
        }
        Gamepad.current?.SetMotorSpeeds(leftMotor, rightMotor);
    }
}
