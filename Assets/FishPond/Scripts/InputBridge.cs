using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishPond
{
    /// <summary>
    /// 옛 Input Manager와 새 Input System 중 프로젝트에서 켜진 쪽을 자동으로 씁니다.
    /// (Project Settings > Player > Active Input Handling)
    /// </summary>
    public static class InputBridge
    {
        public struct Pointer
        {
            public int id;
            public Vector2 screen;
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static readonly Dictionary<KeyCode, Key> map = new Dictionary<KeyCode, Key>();

        static Key ToKey(KeyCode k)
        {
            Key key;
            if (map.TryGetValue(k, out key)) return key;
            switch (k)
            {
                case KeyCode.Return: key = Key.Enter; break;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; break;
                default:
                    if (!Enum.TryParse(k.ToString(), out key)) key = Key.None;
                    break;
            }
            map[k] = key;
            return key;
        }

        public static bool Held(KeyCode k)
        {
            var kb = Keyboard.current;
            var key = ToKey(k);
            return kb != null && key != Key.None && kb[key].isPressed;
        }

        public static bool Down(KeyCode k)
        {
            var kb = Keyboard.current;
            var key = ToKey(k);
            return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
        }

        public static void GetPointers(List<Pointer> list)
        {
            list.Clear();
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                    if (t.press.isPressed)
                        list.Add(new Pointer { id = t.touchId.ReadValue(), screen = t.position.ReadValue() });
            }
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed)
                list.Add(new Pointer { id = -1, screen = m.position.ReadValue() });
        }
#else
        public static bool Held(KeyCode k) { return Input.GetKey(k); }

        public static bool Down(KeyCode k) { return Input.GetKeyDown(k); }

        public static void GetPointers(List<Pointer> list)
        {
            list.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                    list.Add(new Pointer { id = t.fingerId, screen = t.position });
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                list.Add(new Pointer { id = -1, screen = Input.mousePosition });
        }
#endif
    }
}
