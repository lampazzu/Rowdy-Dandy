using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Controller rumble (XInput, player 1). One shared runner owns the motors and always switches them off when
// the pulse ends, so a rumble can't get stuck on when the enemy that started it is hit again, dies or despawns.
// Also stops while the game is paused or loses focus.
public class GamepadRumble : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState1_4(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState9_1_0(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    private const float MaxPulse = 0.5f; // no single pulse longer than this, whatever the caller asks

    private static bool useXInput14 = true;
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

    private static void Set(float leftMotor, float rightMotor)
    {
        var vibration = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(Mathf.Clamp01(leftMotor) * 65535),
            wRightMotorSpeed = (ushort)(Mathf.Clamp01(rightMotor) * 65535)
        };

        try
        {
            if (useXInput14) XInputSetState1_4(0, ref vibration);
            else XInputSetState9_1_0(0, ref vibration);
        }
        catch (DllNotFoundException)
        {
            try
            {
                useXInput14 = false;
                XInputSetState9_1_0(0, ref vibration);
            }
            catch { }
        }
        catch { }
    }
}
