using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Utilities;

// All of Rowdy's controls, on the new Input System. Every gamepad the Input System knows (Xbox, PlayStation 3/4/5,
// Switch Pro, 8BitDo, Logitech...) uses the same button positions: south = jump, west = attack, north = interact, etc.
// Generic DirectInput joysticks get the stick / hat for moving and their trigger for jumping.
//
// Reads every key / button on its own, exactly like the old Input.GetKeyDown(...) || Input.GetKeyDown(...) checks:
// a press on one key counts even while another key bound to the same thing is held, and letting go of either one
// counts as a release.
//
//   GameInput.Down(GameInput.Act.Jump)   true on the frame the button goes down (Held / Up too)
//   GameInput.PadDown(...)               the gamepad button only (where the old code had its own keyboard key)
//   GameInput.MoveX / MoveY              -1, 0 or 1 (keys, d-pad, or the stick pushed past half way)
//   GameInput.Icon(Act.Interact)         the button icon (E keycap, yellow Y, PlayStation triangle, Switch X) of
//                                        whatever was used last, as a character PixelText draws (ButtonIcons)
//   GameInput.Format("PRESS {INTERACT}") same, inside a text; GameInput.Label(...) = the button's name as plain text
//
// Bindings are in Bindings below; the prompt names for each device are in Label().
public static class GameInput
{
    public enum Act
    {
        Jump, Attack, SurfDash, SwitchWeapon, Interact, Stats, Map, Notes, Pause,
        Submit, Back, PrevTab, NextTab, MapCenter, DevMenu,
        Werewolf // Call of the Moon boon: B / Circle, K on the keyboard
    }

    // Which buttons the prompts show: what Rowdy was last controlled with
    public enum Device { Keyboard, Xbox, PlayStation, Switch }

    private class Binding
    {
        public Key[] keys = Array.Empty<Key>();
        public bool mouseLeft;
        public Func<Gamepad, ButtonControl> pad;
        public Func<Joystick, ButtonControl> joystick;
    }

    private static readonly Binding[] Bindings = BuildBindings();

    private static Binding[] BuildBindings()
    {
        var b = new Binding[Enum.GetValues(typeof(Act)).Length];
        b[(int)Act.Jump] = new Binding { keys = new[] { Key.Space }, pad = p => p.buttonSouth, joystick = j => j.trigger };
        b[(int)Act.Attack] = new Binding { keys = new[] { Key.J }, mouseLeft = true, pad = p => p.buttonWest };
        b[(int)Act.SurfDash] = new Binding { keys = new[] { Key.LeftShift, Key.L }, pad = p => p.rightShoulder };
        b[(int)Act.SwitchWeapon] = new Binding { keys = new[] { Key.Q }, pad = p => p.leftShoulder };
        b[(int)Act.Interact] = new Binding { keys = new[] { Key.E }, pad = p => p.buttonNorth };
        b[(int)Act.Stats] = new Binding { keys = new[] { Key.C }, pad = p => p.leftTrigger };
        b[(int)Act.Map] = new Binding { keys = new[] { Key.M }, pad = p => p.rightTrigger };
        b[(int)Act.Notes] = new Binding { keys = new[] { Key.Tab }, pad = p => p.selectButton };
        b[(int)Act.Pause] = new Binding { keys = new[] { Key.Escape }, pad = p => p.startButton };
        // Menus
        b[(int)Act.Submit] = new Binding { keys = new[] { Key.Enter, Key.NumpadEnter, Key.Space }, pad = p => p.buttonSouth };
        b[(int)Act.Back] = new Binding { keys = new[] { Key.Backspace }, pad = p => p.buttonEast };
        b[(int)Act.PrevTab] = new Binding { keys = new[] { Key.Q }, pad = p => p.leftShoulder };
        b[(int)Act.NextTab] = new Binding { keys = new[] { Key.E }, pad = p => p.rightShoulder };
        b[(int)Act.MapCenter] = new Binding { keys = new[] { Key.Space }, pad = p => p.buttonNorth };
        b[(int)Act.DevMenu] = new Binding { keys = new[] { Key.F1, Key.Backquote } };
        b[(int)Act.Werewolf] = new Binding { keys = new[] { Key.K }, pad = p => p.buttonEast };
        return b;
    }

    private static bool listening;
    private static IDisposable anyButtonListener;

    private static Device device = Device.Keyboard;
    private static bool dualSense; // PS5 pad: "CREATE" instead of "SHARE"
    private static int deviceVersion;

    // Bumped every time the prompt device changes, so built-once UI texts know to refresh
    public static int DeviceVersion { get { Poll(); return deviceVersion; } }
    public static Device Current { get { Poll(); return device; } }
    public static bool UsingGamepad => Current != Device.Keyboard;
    // The pad family used most recently (Xbox until a pad is used), for pages that always show gamepad buttons
    public static Device LastGamepad { get; private set; } = Device.Xbox;

    // START / MENU / OPTIONS / + held on any gamepad (pause menu: hold it to reach Dev Tools)
    public static bool GamepadStartHeld
    {
        get
        {
            foreach (Gamepad pad in Gamepad.all) if (pad.startButton.isPressed) return true;
            return false;
        }
    }

    // ---------------------------------------------------------------- buttons

    private enum Check { Down, Held, Up }

    private static bool Test(ButtonControl b, Check check) =>
        b != null && (check == Check.Down ? b.wasPressedThisFrame : check == Check.Held ? b.isPressed : b.wasReleasedThisFrame);

    private static bool KeysTest(Binding b, Check check)
    {
        Keyboard k = Keyboard.current;
        if (k != null) foreach (Key key in b.keys) if (Test(k[key], check)) return true;
        Mouse m = Mouse.current;
        return b.mouseLeft && m != null && Test(m.leftButton, check);
    }

    private static bool PadTest(Binding b, Check check)
    {
        if (b.pad != null) foreach (Gamepad p in Gamepad.all) if (Test(b.pad(p), check)) return true;
        if (b.joystick != null) foreach (Joystick j in Joystick.all) if (Test(b.joystick(j), check)) return true;
        return false;
    }

    public static bool Down(Act a) { Binding b = Bindings[(int)a]; return KeysTest(b, Check.Down) || PadTest(b, Check.Down); }
    public static bool Held(Act a) { Binding b = Bindings[(int)a]; return KeysTest(b, Check.Held) || PadTest(b, Check.Held); }
    public static bool Up(Act a) { Binding b = Bindings[(int)a]; return KeysTest(b, Check.Up) || PadTest(b, Check.Up); }
    public static bool PadDown(Act a) => PadTest(Bindings[(int)a], Check.Down);

    // ---------------------------------------------------------------- movement

    private const float StickPress = 0.5f;
    private static float Digital(float v) => v >= StickPress ? 1f : v <= -StickPress ? -1f : 0f;

    // Keyboard axes like the old Input Manager ones: right / D minus left / A (both = 0)
    private static float KeyAxis(Key pos, Key posAlt, Key neg, Key negAlt)
    {
        Keyboard k = Keyboard.current;
        if (k == null) return 0f;
        float v = 0f;
        if (k[pos].isPressed || k[posAlt].isPressed) v += 1f;
        if (k[neg].isPressed || k[negAlt].isPressed) v -= 1f;
        return v;
    }

    private static float KeysX => KeyAxis(Key.RightArrow, Key.D, Key.LeftArrow, Key.A);
    private static float KeysY => KeyAxis(Key.UpArrow, Key.W, Key.DownArrow, Key.S);

    // The pads' d-pad (digital) and stick (analog, after its dead zone): the biggest push on each axis wins
    private static Vector2 PadVector(bool digital)
    {
        Vector2 best = Vector2.zero;
        void Take(Vector2 v)
        {
            if (digital) v = new Vector2(Digital(v.x), Digital(v.y));
            if (Mathf.Abs(v.x) > Mathf.Abs(best.x)) best.x = v.x;
            if (Mathf.Abs(v.y) > Mathf.Abs(best.y)) best.y = v.y;
        }
        foreach (Gamepad p in Gamepad.all) { Take(p.dpad.ReadValue()); Take(p.leftStick.ReadValue()); }
        foreach (Joystick j in Joystick.all)
        {
            if (j.stick != null) Take(j.stick.ReadValue());
            if (j.TryGetChildControl("hat") is Vector2Control hat) Take(hat.ReadValue());
        }
        return best;
    }

    // Two sources on one axis (keys and pad): the bigger one wins, the keys on a tie (same as the old Input Manager)
    private static float Combine(float keys, float pad) => Mathf.Abs(pad) > Mathf.Abs(keys) ? pad : keys;

    // Digital, like the old axes: keys / d-pad give -1, 0, 1 and so does the stick once past half way
    public static float MoveX => Combine(KeysX, PadVector(true).x);
    public static float MoveY => Combine(KeysY, PadVector(true).y); // menus: up / W, down / S, d-pad, stick

    // Rowdy's "down" (duck, down + jump drops through), as before: the S key (not the down arrow), or down on a pad.
    // The d-pad counts with diagonals; the stick has to point mostly downward, so a stick held a bit low while
    // running or surfing doesn't turn jumps into drop-throughs / ducks.
    public static bool HoldingDown
    {
        get
        {
            if (KeyHeld(Key.S)) return true;
            foreach (Gamepad p in Gamepad.all)
            {
                if (p.dpad.ReadValue().y < 0f) return true;
                Vector2 s = p.leftStick.ReadValue();
                if (s.y <= -StickPress && -s.y >= Mathf.Abs(s.x)) return true;
            }
            foreach (Joystick j in Joystick.all)
            {
                    if (j.TryGetChildControl("hat") is Vector2Control hat && hat.ReadValue().y < 0f) return true;
                Vector2 s = j.stick != null ? j.stick.ReadValue() : Vector2.zero;
                if (s.y <= -StickPress && -s.y >= Mathf.Abs(s.x)) return true;
            }
            return false;
        }
    }

    // Keys (digital) or the stick (analog): map panning
    public static Vector2 MoveVector
    {
        get
        {
            var keys = new Vector2(KeysX, KeysY);
            Vector2 pad = PadVector(false);
            return new Vector2(Combine(keys.x, pad.x), Combine(keys.y, pad.y));
        }
    }

    // MoveX eased in / out the way Input.GetAxis("Horizontal") did on the keyboard (sensitivity 3, gravity 3, snap):
    // used for Rowdy's controls in the water. Gamepads were never smoothed, so they still aren't.
    private const float KeySensitivity = 3f, KeyGravity = 3f;
    private static float smoothX, smoothTime;
    private static int smoothFrame = -1;

    public static float MoveXSmoothed
    {
        get
        {
            float keys = KeysX;
            if (smoothFrame != Time.frameCount)
            {
                float dt = smoothFrame < 0 ? 0f : Mathf.Min(Time.unscaledTime - smoothTime, 1f);
                smoothFrame = Time.frameCount;
                smoothTime = Time.unscaledTime;
                if (keys != 0f)
                {
                    if (smoothX != 0f && Mathf.Sign(smoothX) != Mathf.Sign(keys)) smoothX = 0f; // snap
                    smoothX = Mathf.MoveTowards(smoothX, keys, KeySensitivity * dt);
                }
                else smoothX = Mathf.MoveTowards(smoothX, 0f, KeyGravity * dt);
            }
            return Combine(smoothX, PadVector(true).x);
        }
    }

    // ---------------------------------------------------------------- raw keys (dev hotkeys, inspector KeyCodes)

    public static bool KeyDown(Key key) { Keyboard k = Keyboard.current; return k != null && k[key].wasPressedThisFrame; }
    public static bool KeyHeld(Key key) { Keyboard k = Keyboard.current; return k != null && k[key].isPressed; }
    public static bool KeyUp(Key key) { Keyboard k = Keyboard.current; return k != null && k[key].wasReleasedThisFrame; }

    // For old KeyCode fields set in the Inspector (letters, digits, keypad, F keys, common symbols)
    public static bool KeyDown(KeyCode code) { Key key = ToKey(code); return key != Key.None && KeyDown(key); }

    public static bool MouseLeftDown { get { Mouse m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; } }
    public static float MouseScroll { get { Mouse m = Mouse.current; return m != null ? m.scroll.ReadValue().y : 0f; } }
    public static Key ToKey(KeyCode code)
    {
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (code - KeyCode.A);
        if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9) return Key.Digit1 + (code - KeyCode.Alpha1);
        if (code == KeyCode.Alpha0) return Key.Digit0;
        if (code >= KeyCode.Keypad1 && code <= KeyCode.Keypad9) return Key.Numpad1 + (code - KeyCode.Keypad1);
        if (code == KeyCode.Keypad0) return Key.Numpad0;
        if (code >= KeyCode.F1 && code <= KeyCode.F12) return Key.F1 + (code - KeyCode.F1);
        switch (code)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.KeypadEnter: return Key.NumpadEnter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Tab: return Key.Tab;
            case KeyCode.Backspace: return Key.Backspace;
            case KeyCode.LeftShift: return Key.LeftShift;
            case KeyCode.RightShift: return Key.RightShift;
            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.LeftAlt: return Key.LeftAlt;
            case KeyCode.RightAlt: return Key.RightAlt;
            case KeyCode.UpArrow: return Key.UpArrow;
            case KeyCode.DownArrow: return Key.DownArrow;
            case KeyCode.LeftArrow: return Key.LeftArrow;
            case KeyCode.RightArrow: return Key.RightArrow;
            case KeyCode.Comma: return Key.Comma;
            case KeyCode.Period: return Key.Period;
            case KeyCode.Semicolon: return Key.Semicolon;
            case KeyCode.Quote: return Key.Quote;
            case KeyCode.Slash: return Key.Slash;
            case KeyCode.Backslash: return Key.Backslash;
            case KeyCode.Minus: return Key.Minus;
            case KeyCode.Equals: return Key.Equals;
            case KeyCode.LeftBracket: return Key.LeftBracket;
            case KeyCode.RightBracket: return Key.RightBracket;
            case KeyCode.BackQuote: return Key.Backquote;
            case KeyCode.Delete: return Key.Delete;
            case KeyCode.Insert: return Key.Insert;
            case KeyCode.Home: return Key.Home;
            case KeyCode.End: return Key.End;
            case KeyCode.PageUp: return Key.PageUp;
            case KeyCode.PageDown: return Key.PageDown;
        }
        return Key.None;
    }

    // ---------------------------------------------------------------- which device

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        anyButtonListener?.Dispose();
        anyButtonListener = null;
        listening = false;
        device = Device.Keyboard;
        LastGamepad = Device.Xbox;
        smoothFrame = -1;
        smoothX = 0f;
        pollFrame = -1;
    }

    // XInput pads are polled at 60 Hz by default (the old Input Manager read them every frame): poll faster so a
    // press never waits up to a frame before the game sees it
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void FastPolling() => InputSystem.pollingFrequency = 250f;

    private static Vector2 lastMouse;
    private static int pollFrame = -1;

    // Once a frame: any button press (listener), a stick pushed past half way, or the mouse moved (= keyboard)
    private static void Poll()
    {
        if (!listening)
        {
            listening = true;
            anyButtonListener = InputSystem.onAnyButtonPress.Call(control => Used(control.device));
            if (Gamepad.current != null) Used(Gamepad.current); // start with the pad's buttons if one is plugged in
        }
        if (pollFrame == Time.frameCount) return;
        pollFrame = Time.frameCount;

        foreach (Gamepad p in Gamepad.all)
            if (p.leftStick.ReadValue().sqrMagnitude > StickPress * StickPress || p.dpad.ReadValue().sqrMagnitude > 0f) { Used(p); break; }

        Mouse m = Mouse.current;
        if (m == null || device == Device.Keyboard) return;
        Vector2 pos = m.position.ReadValue();
        if ((pos - lastMouse).sqrMagnitude > 16f && m.delta.ReadValue().sqrMagnitude > 4f) Used(m);
        lastMouse = pos;
    }
    private static void Used(InputDevice d)
    {
        if (d == null) return;
        Device kind;
        if (d is Keyboard || d is Mouse) kind = Device.Keyboard;
        else if (d is DualShockGamepad) kind = Device.PlayStation;
        else if (IsSwitch(d)) kind = Device.Switch;
        else if (d is Gamepad || d is Joystick) kind = Device.Xbox;
        else return; // touch, sensors...

        bool ps5 = d.GetType().Name.Contains("DualSense");
        if (kind == device && (kind != Device.PlayStation || ps5 == dualSense)) return;
        device = kind;
        if (kind == Device.PlayStation) dualSense = ps5;
        if (kind != Device.Keyboard) LastGamepad = kind;
        deviceVersion++;
    }

    private static bool IsSwitch(InputDevice d)
    {
        string layout = d.layout ?? "";
        string maker = d.description.manufacturer ?? "";
        return layout.Contains("Switch") || maker.Contains("Nintendo");
    }

    // ---------------------------------------------------------------- prompt names

    // PlayStation face buttons, drawn by PixelFont / HudTag
    public const char Cross = '✕', Circle = '○', Square = '□', Triangle = '△';

    public static string Label(Act a) => Label(a, Current);

    public static string Label(Act a, Device d)
    {
        switch (d)
        {
            case Device.Xbox:
                switch (a)
                {
                    case Act.Jump: case Act.Submit: return "A";
                    case Act.Back: case Act.Werewolf: return "B";
                    case Act.Attack: return "X";
                    case Act.Interact: case Act.MapCenter: return "Y";
                    case Act.SwitchWeapon: case Act.PrevTab: return "LB";
                    case Act.SurfDash: case Act.NextTab: return "RB";
                    case Act.Stats: return "LT";
                    case Act.Map: return "RT";
                    case Act.Notes: return "VIEW";
                    case Act.Pause: return "MENU";
                }
                break;
            case Device.PlayStation:
                switch (a)
                {
                    case Act.Jump: case Act.Submit: return Cross.ToString();
                    case Act.Back: case Act.Werewolf: return Circle.ToString();
                    case Act.Attack: return Square.ToString();
                    case Act.Interact: case Act.MapCenter: return Triangle.ToString();
                    case Act.SwitchWeapon: case Act.PrevTab: return "L1";
                    case Act.SurfDash: case Act.NextTab: return "R1";
                    case Act.Stats: return "L2";
                    case Act.Map: return "R2";
                    case Act.Notes: return dualSense ? "CREATE" : "SHARE";
                    case Act.Pause: return "OPTIONS";
                }
                break;
            case Device.Switch: // Nintendo letters sit on other spots: the bottom button is B, the top one X
                switch (a)
                {
                    case Act.Jump: case Act.Submit: return "B";
                    case Act.Back: case Act.Werewolf: return "A";
                    case Act.Attack: return "Y";
                    case Act.Interact: case Act.MapCenter: return "X";
                    case Act.SwitchWeapon: case Act.PrevTab: return "L";
                    case Act.SurfDash: case Act.NextTab: return "R";
                    case Act.Stats: return "ZL";
                    case Act.Map: return "ZR";
                    case Act.Notes: return "-";
                    case Act.Pause: return "+";
                }
                break;
        }
        switch (a)
        {
            case Act.Jump: case Act.MapCenter: return "SPACE";
            case Act.Submit: return "ENTER";
            case Act.Back: return "ESC";
            case Act.Attack: return "J";
            case Act.Interact: case Act.NextTab: return "E";
            case Act.SwitchWeapon: case Act.PrevTab: return "Q";
            case Act.SurfDash: return "SHIFT";
            case Act.Stats: return "C";
            case Act.Map: return "M";
            case Act.Notes: return "TAB";
            case Act.Pause: return "ESC";
            case Act.DevMenu: return "F1";
            case Act.Werewolf: return "K";
        }
        return "?";
    }

    public static string MoveLabel => UsingGamepad ? "STICK" : "ARROWS";

    // ---------------------------------------------------------------- button icons

    // ButtonIcons id for a button on a device: "KB:SPACE", "XB:A", "PS:CROSS", "SW:ZL"...
    public static string IconId(Act a, Device d)
    {
        string label = Label(a, d);
        switch (d)
        {
            case Device.Xbox: return "XB:" + label;
            case Device.Switch: return "SW:" + label;
            case Device.PlayStation:
                if (label.Length == 1)
                {
                    char c = label[0];
                    return c == Cross ? "PS:CROSS" : c == Circle ? "PS:CIRCLE" : c == Square ? "PS:SQUARE" : "PS:TRIANGLE";
                }
                return "PS:" + label;
        }
        return "KB:" + label;
    }

    public static string IconId(Act a) => IconId(a, Current);

    // The icon as a character to put in a text (PixelText / PixelFont draw it)
    public static string Icon(Act a) => ButtonIcons.Text(IconId(a, Current));
    public static string Icon(Act a, Device d) => ButtonIcons.Text(IconId(a, d));
    public static string MoveIcon(Device d) => ButtonIcons.Text(d == Device.Keyboard ? "ARROWS" : "STICK");
    public static string DownIcon(Device d) => ButtonIcons.Text(d == Device.Keyboard ? "KB:S" : "DPAD:DOWN");

    // Replaces {JUMP} {ATTACK} {SURF} {SWITCH} {INTERACT} {STATS} {MAP} {NOTES} {PAUSE} {OK} {BACK}
    // {PREV} {NEXT} {CENTER} {MOVE} {DOWN} with the current device's button icons
    public static string Format(string text) => Format(text, Current);

    public static string Format(string text, Device d)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        foreach (var (token, act) in Tokens) text = text.Replace(token, Icon(act, d));
        return text.Replace("{MOVE}", MoveIcon(d)).Replace("{DOWN}", DownIcon(d));
    }

    private static readonly List<(string, Act)> Tokens = new List<(string, Act)>
    {
        ("{JUMP}", Act.Jump), ("{ATTACK}", Act.Attack), ("{SURF}", Act.SurfDash), ("{SWITCH}", Act.SwitchWeapon),
        ("{INTERACT}", Act.Interact), ("{STATS}", Act.Stats), ("{MAP}", Act.Map), ("{NOTES}", Act.Notes),
        ("{PAUSE}", Act.Pause), ("{OK}", Act.Submit), ("{BACK}", Act.Back), ("{PREV}", Act.PrevTab),
        ("{NEXT}", Act.NextTab), ("{CENTER}", Act.MapCenter), ("{WOLF}", Act.Werewolf),
    };
}