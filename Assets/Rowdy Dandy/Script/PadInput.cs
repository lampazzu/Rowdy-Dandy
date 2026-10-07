using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Gamepad buttons the old Input Manager can't give directly:
//  - L2 / LT: a trigger axis on Xbox-style pads, read straight from XInput (same DLL as GamepadRumble).
//  - PlayStation pads without Steam/DS4Windows (DirectInput) number things differently: L2 = button 6, Share = 8.
// Select = Back / View / Share. All "Down" checks are true on the frame the button goes down.
public static class PadInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState1_4(int dwUserIndex, out XInputState state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState9_1_0(int dwUserIndex, out XInputState state);

    private const byte TriggerOn = 120, TriggerOff = 60; // a bit of hysteresis on the analog trigger

    private static bool useXInput14 = true, xinputMissing;
    private static int polledFrame = -1;
    private static bool l2Held, l2Down;
    private static float namesCheckedAt = -10f;
    private static bool playStationDirect;

    public static bool L2Down { get { Poll(); return l2Down; } }
    public static bool L2Held { get { Poll(); return l2Held; } }

    public static bool SelectDown => Input.GetKeyDown(SelectKey);
    public static bool SelectUp => Input.GetKeyUp(SelectKey);
    public static KeyCode SelectKey { get { Poll(); return playStationDirect ? KeyCode.JoystickButton8 : KeyCode.JoystickButton6; } }

    private static void Poll()
    {
        if (polledFrame == Time.frameCount) return;
        polledFrame = Time.frameCount;

        if (Time.unscaledTime - namesCheckedAt > 2f)
        {
            namesCheckedAt = Time.unscaledTime;
            playStationDirect = false;
            foreach (string n in Input.GetJoystickNames())
            {
                if (string.IsNullOrEmpty(n)) continue;
                string lower = n.ToLowerInvariant();
                if (lower.Contains("xbox") || lower.Contains("xinput")) { playStationDirect = false; break; }
                if (lower.Contains("wireless controller") || lower.Contains("dualsense") || lower.Contains("dualshock") || lower.Contains("playstation"))
                    playStationDirect = true;
            }
        }

        bool held = l2Held;
        if (playStationDirect)
        {
            held = Input.GetKey(KeyCode.JoystickButton6);
        }
        else if (TryReadXInput(out XInputState state))
        {
            byte t = state.Gamepad.bLeftTrigger;
            held = l2Held ? t > TriggerOff : t > TriggerOn;
        }
        else held = false;

        l2Down = held && !l2Held;
        l2Held = held;
    }

    private static bool TryReadXInput(out XInputState state)
    {
        state = default;
        if (xinputMissing) return false;
        try
        {
            int result = useXInput14 ? XInputGetState1_4(0, out state) : XInputGetState9_1_0(0, out state);
            return result == 0;
        }
        catch (DllNotFoundException)
        {
            if (!useXInput14) { xinputMissing = true; return false; }
            useXInput14 = false;
            return TryReadXInput(out state);
        }
        catch (Exception)
        {
            xinputMissing = true;
            return false;
        }
    }
}
