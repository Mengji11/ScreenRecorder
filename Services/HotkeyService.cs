using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ScreenRecorder.Services
{
    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private IntPtr _windowHandle;
        private Dictionary<int, Action> _hotkeyActions = new();
        private int _nextId = 9000;

        public event EventHandler<int>? HotkeyPressed;

        public void Register(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public int RegisterHotkey(Key key, bool ctrl, bool alt, bool shift, Action action)
        {
            int id = _nextId++;
            uint modifiers = 0;
            if (ctrl) modifiers |= 0x0002;
            if (alt) modifiers |= 0x0001;
            if (shift) modifiers |= 0x0004;

            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

            if (RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
            {
                _hotkeyActions[id] = action;
                return id;
            }

            return -1;
        }

        public void UnregisterAll()
        {
            foreach (var id in _hotkeyActions.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _hotkeyActions.Clear();
        }

        public bool ProcessMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    action?.Invoke();
                    HotkeyPressed?.Invoke(this, id);
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            UnregisterAll();
            GC.SuppressFinalize(this);
        }
    }
}
