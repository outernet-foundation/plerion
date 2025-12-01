
using System;
using UnityEditor;
using UnityEditor.Compilation;

namespace vietlabs.fr2
{
    [InitializeOnLoad]
    public static class FR2_WindowFocus
    {
        public static event Action<EditorWindow> FocusedWindowChanged = delegate { };

        public static string CurrentWindowType => EditorWindow.focusedWindow?.GetType().Name;
        public static string PreviousWindowType { get; private set; }

#if UNITY_6000_0_OR_NEWER
        // ---------- Native implementation ----------
        static FR2_WindowFocus()
        {
            EditorWindow.windowFocusChanged += Raise;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static void Raise()
        {
            var current = EditorWindow.focusedWindow;
            PreviousWindowType = _lastWindowType;
            _lastWindowType = current?.GetType().Name;
            if (current != null) FocusedWindowChanged(current);
        }

        private static void Cleanup() =>
            EditorWindow.windowFocusChanged -= Raise;
#elif UNITY_2023_1_OR_NEWER
        // ---------- Legacy implementation for Unity 2023-2024 ----------
        static FR2_WindowFocus()
        {
            EditorWindow.focusedWindowChanged += Raise;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static void Raise()
        {
            var current = EditorWindow.focusedWindow;
            PreviousWindowType = _lastWindowType;
            _lastWindowType = current?.GetType().Name;
            if (current != null) FocusedWindowChanged(current);
        }

        private static void Cleanup() =>
            EditorWindow.focusedWindowChanged -= Raise;

#else
        // ---------- Fallback: poll each editor-tick ----------
        private static EditorWindow _last;

        static FR2_WindowFocus()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static void Tick()
        {
            var current = EditorWindow.focusedWindow;
            if (current == _last) return;
            
            PreviousWindowType = _lastWindowType;
            _last = current;
            _lastWindowType = current?.GetType().Name;
            if (current != null) FocusedWindowChanged(current);
        }

        private static void Cleanup() =>
            EditorApplication.update -= Tick;
#endif

        private static string _lastWindowType;
    }
}