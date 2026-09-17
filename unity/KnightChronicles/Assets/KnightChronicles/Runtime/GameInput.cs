using System;
using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    public static class GameInput
    {
        public static readonly string[] Actions = { "up", "down", "left", "right", "fire", "dodge", "interact", "inventory", "map", "pause", "quick1", "quick2", "quick3", "skill1", "skill2" };
        public static readonly string[] Labels = { "向上", "向下", "向左", "向右", "攻击", "翻滚", "交互", "背包", "地图", "暂停", "快捷栏 1", "快捷栏 2", "快捷栏 3", "技能 I", "技能 II" };
        private static readonly KeyCode[] Defaults = { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Mouse0, KeyCode.Space, KeyCode.E, KeyCode.I,
            KeyCode.M, KeyCode.Escape, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Q, KeyCode.R };
        public static KeyCode Key(string action)
        {
            var binding = GameSession.State.Bindings.Find(b => b.Action == action); KeyCode key;
            return binding != null && Enum.TryParse(binding.Key, out key) ? key : Defaults[Array.IndexOf(Actions, action)];
        }
        public static bool Held(string action) { return Input.GetKey(Key(action)); }
        public static bool Down(string action) { return Input.GetKeyDown(Key(action)); }
        public static bool Up(string action) { return Input.GetKeyUp(Key(action)); }
        public static Vector2 Move { get { return new Vector2((Held("right") ? 1 : 0) - (Held("left") ? 1 : 0), (Held("up") ? 1 : 0) - (Held("down") ? 1 : 0)); } }
        public static bool Rebind(string action, KeyCode key, out string error)
        {
            error = null;
            if (key == KeyCode.None) { error = "无效按键"; return false; }
            foreach (var other in Actions) if (other != action && Key(other) == key) { error = "按键已被 " + Labels[Array.IndexOf(Actions, other)] + " 使用"; return false; }
            var binding = GameSession.State.Bindings.Find(b => b.Action == action);
            if (binding == null) { binding = new KeyBinding { Action = action }; GameSession.State.Bindings.Add(binding); } binding.Key = key.ToString(); return true;
        }
    }
}
