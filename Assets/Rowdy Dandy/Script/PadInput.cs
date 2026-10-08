using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Gamepad buttons the old Input Manager can't give directly:
//  - L2 / LT: a trigger axis on Xbox-style pads. Read from XInput (any of the 4 pad slots, same DLL as GamepadRumble),
//    and as a fallback from the raw joystick axes (Input Manager): Unity on Windows reports LT as "RD Axis 9" and RT as
//    "RD Axis 10" (checked on an 8BitDo Ultimate 2C); DS4 in DirectInput puts L2 on axis 4. The shared 3rd axis isn't
//    used - its sign differs between drivers.
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
    private const float AxisOn = 0.5f, AxisOff = 0.25f;

    private static bool useXInput14 = true, xinputMissing;
    private static int polledFrame = -1;
    private static bool l2Held, l2Down, r2Held, r2Down;
    private static float namesCheckedAt = -10f;
    private static bool playStationDirect;

    // Raw axes: resting values are remembered so an axis that rests at -1 (some drivers) never reads as "pressed"
    // Only the axes that are L2 on that family: axis 3 is the right stick on a DS4, axis 4 on an Xbox pad
    private static readonly int[] XboxAxes = { 0 }, PlayStationAxes = { 2 }, XboxR2Axes = { 3 };
    private static readonly string[] RawAxes = { "RD Axis 9", "RD Axis 3", "RD Axis 4", "RD Axis 10" };
    private static readonly float[] axisRest = new float[RawAxes.Length];
    private static readonly bool[] axisSeenRest = new bool[RawAxes.Length];
    private static readonly bool[] axisMissing = new bool[RawAxes.Length];

    public static bool L2Down { get { Poll(); return l2Down; } }
    public static bool L2Held { get { Poll(); return l2Held; } }
    // R2 / RT (the map). DS4 in DirectInput: button 7.
    public static bool R2Down { get { Poll(); return r2Down; } }

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
                // XInput pads (Xbox, 8BitDo "Controller (8BitDo Ultimate 2C Wireless Controller)"...) are named "Controller (...)".
                // A DS4 without Steam / DS4Windows is called exactly "Wireless Controller".
                if (lower.Contains("xbox") || lower.Contains("xinput") || lower.StartsWith("controller (")) { playStationDirect = false; break; }
                if (lower.Trim() == "wireless controller" || lower.Contains("dualsense") || lower.Contains("dualshock") || lower.Contains("playstation"))
                    playStationDirect = true;
            }
        }

        bool held, rHeld;
        if (playStationDirect)
        {
            held = Input.GetKey(KeyCode.JoystickButton6) || RawAxisHeld(PlayStationAxes, l2Held);
            rHeld = Input.GetKey(KeyCode.JoystickButton7);
        }
        else
        {
            held = rHeld = false;
            if (TryReadXInputTrigger(out byte lt, out byte rtrig))
            {
                held = l2Held ? lt > TriggerOff : lt > TriggerOn;
                rHeld = r2Held ? rtrig > TriggerOff : rtrig > TriggerOn;
            }
            if (!held) held = RawAxisHeld(XboxAxes, l2Held);
            if (!rHeld) rHeld = RawAxisHeld(XboxR2Axes, r2Held);
        }

        l2Down = held && !l2Held;
        l2Held = held;
        r2Down = rHeld && !r2Held;
        r2Held = rHeld;
    }

    private static bool RawAxisHeld(int[] which, bool wasHeld)
    {
        float threshold = wasHeld ? AxisOff : AxisOn;
        foreach (int i in which)
        {
            if (axisMissing[i]) continue;
            float v;
            try { v = Input.GetAxisRaw(RawAxes[i]); }
            catch (ArgumentException) { axisMissing[i] = true; continue; }

            // The first reading is taken as the axis' rest position (0 for most pads, -1 for a few DirectInput ones)
            if (!axisSeenRest[i]) { axisSeenRest[i] = true; axisRest[i] = v < -0.9f ? -1f : 0f; }
            float pressed = axisRest[i] < 0f ? (v + 1f) * 0.5f : v; // 0..1
            if (pressed > threshold) return true;
        }
        return false;
    }

    // Left trigger of the connected XInput pad (0-255). Polling empty slots is slow, so the
    // other slots are only searched every couple of seconds while no pad answers.
    private static int xinputPad = 0;
    private static float padSearchedAt = -10f;

    private static bool TryReadXInputTrigger(out byte trigger, out byte right)
    {
        trigger = right = 0;
        if (TryReadXInput(xinputPad, out XInputState state)) { trigger = state.Gamepad.bLeftTrigger; right = state.Gamepad.bRightTrigger; return true; }
        if (xinputMissing || Time.unscaledTime - padSearchedAt < 2f) return false;
        padSearchedAt = Time.unscaledTime;
        for (int pad = 0; pad < 4; pad++)
        {
            if (pad == xinputPad || !TryReadXInput(pad, out state)) continue;
            xinputPad = pad;
            trigger = state.Gamepad.bLeftTrigger;
            right = state.Gamepad.bRightTrigger;
            return true;
        }
        return false;
    }

    private static bool TryReadXInput(int pad, out XInputState state)
    {
        state = default;
        if (xinputMissing) return false;
        try
        {
            int result = useXInput14 ? XInputGetState1_4(pad, out state) : XInputGetState9_1_0(pad, out state);
            return result == 0;
        }
        catch (DllNotFoundException)
        {
            if (!useXInput14) { xinputMissing = true; return false; }
            useXInput14 = false;
            return TryReadXInput(pad, out state);
        }
        catch (Exception)
        {
            xinputMissing = true;
            return false;
        }
    }
}
