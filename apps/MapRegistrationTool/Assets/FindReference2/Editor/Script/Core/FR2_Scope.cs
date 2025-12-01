using System;
using UnityEngine;
using UnityEditor;

namespace vietlabs.fr2
{
    /// <summary>
    /// Consolidated scoped disposable system for Find Reference 2.
    /// Provides automatic cleanup for various Unity editor operations.
    /// All inner structs implement IDisposable for zero-allocation RAII pattern.
    /// </summary>
    internal static class FR2_Scope
    {
        /// <summary>
        /// Profiler scope - automatic BeginSample/EndSample with exception safety
        /// </summary>
        internal readonly struct ProfilerScope : IDisposable
        {
            private readonly bool _isActive;
            
            public ProfilerScope(string sampleName)
            {
                _isActive = !string.IsNullOrEmpty(sampleName);
                if (_isActive) UnityEngine.Profiling.Profiler.BeginSample(sampleName);
            }
            
            public void Dispose()
            {
                if (_isActive) UnityEngine.Profiling.Profiler.EndSample();
            }
        }
        
        /// <summary>
        /// GUI Color scope - automatic color restoration
        /// </summary>
        internal readonly struct GUIColorScope : IDisposable
        {
            private readonly Color _originalColor;
            private readonly bool _isActive;
            
            public GUIColorScope(Color? newColor)
            {
                _isActive = newColor.HasValue;
                _originalColor = _isActive ? GUI.color : default;
                if (_isActive) GUI.color = newColor.Value;
            }
            
            public void Dispose()
            {
                if (_isActive) GUI.color = _originalColor;
            }
        }
        
        /// <summary>
        /// GUI Content Color scope - automatic content color restoration
        /// </summary>
        internal readonly struct GUIContentColorScope : IDisposable
        {
            private readonly Color _originalColor;
            private readonly bool _isActive;
            
            public GUIContentColorScope(Color? newColor)
            {
                _isActive = newColor.HasValue;
                _originalColor = _isActive ? GUI.contentColor : default;
                if (_isActive) GUI.contentColor = newColor.Value;
            }
            
            public void Dispose()
            {
                if (_isActive) GUI.contentColor = _originalColor;
            }
        }
        
        /// <summary>
        /// GUI Background Color scope - automatic background color restoration
        /// </summary>
        internal readonly struct GUIBackgroundColorScope : IDisposable
        {
            private readonly Color _originalColor;
            private readonly bool _isActive;
            
            public GUIBackgroundColorScope(Color? newColor)
            {
                _isActive = newColor.HasValue;
                _originalColor = _isActive ? GUI.backgroundColor : default;
                if (_isActive) GUI.backgroundColor = newColor.Value;
            }
            
            public void Dispose()
            {
                if (_isActive) GUI.backgroundColor = _originalColor;
            }
        }
        
        /// <summary>
        /// GUI Enabled scope - automatic enabled state restoration
        /// </summary>
        internal readonly struct GUIEnabledScope : IDisposable
        {
            private readonly bool _originalEnabled;
            private readonly bool _isActive;
            
            public GUIEnabledScope(bool? newEnabled)
            {
                _isActive = newEnabled.HasValue;
                _originalEnabled = _isActive && GUI.enabled;
                if (_isActive) GUI.enabled = newEnabled.Value;
            }
            
            public void Dispose()
            {
                if (_isActive) GUI.enabled = _originalEnabled;
            }
        }
        
        
        /// <summary>
        /// EditorGUILayout Horizontal scope - automatic Begin/End horizontal layout
        /// </summary>
        internal struct HorizontalLayoutScope : IDisposable
        {
            public readonly Rect rect;
            
            public HorizontalLayoutScope(params GUILayoutOption[] options)
            {
                rect = EditorGUILayout.BeginHorizontal(options);
            }
            
            public HorizontalLayoutScope(GUIStyle style, params GUILayoutOption[] options)
            {
                rect = EditorGUILayout.BeginHorizontal(style, options);
            }
            
            public void Dispose()
            {
                EditorGUILayout.EndHorizontal();
            }
        }
        
        /// <summary>
        /// EditorGUILayout Vertical scope - automatic Begin/End vertical layout
        /// </summary>
        internal struct VerticalLayoutScope : IDisposable
        {
            public readonly Rect rect;
            
            public VerticalLayoutScope(params GUILayoutOption[] options)
            {
                rect = EditorGUILayout.BeginVertical(options);
            }
            
            public VerticalLayoutScope(GUIStyle style, params GUILayoutOption[] options)
            {
                rect = EditorGUILayout.BeginVertical(style, options);
            }
            
            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }
        
        /// <summary>
        /// EditorGUILayout ScrollView scope - automatic Begin/End scroll view
        /// </summary>
        internal struct ScrollViewScope : IDisposable
        {
            public ScrollViewScope(Vector2 scrollPosition, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginScrollView(scrollPosition, options);
            }
            
            public ScrollViewScope(Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginScrollView(scrollPosition, style, options);
            }
            
            public void Dispose()
            {
                EditorGUILayout.EndScrollView();
            }
        }
        
        // Factory methods - short, concise names
        public static ProfilerScope ProfileMethod(string methodName) => new ProfilerScope(methodName);
        public static GUIColorScope GUIColor(Color? color) => new GUIColorScope(color);
        public static GUIContentColorScope ContentColor(Color? color) => new GUIContentColorScope(color);
        public static GUIBackgroundColorScope BGColor(Color? color) => new GUIBackgroundColorScope(color);
        public static GUIEnabledScope GUIEnable(bool? enabled) => new GUIEnabledScope(enabled);
        public static HorizontalLayoutScope HzLayout(params GUILayoutOption[] options) => new HorizontalLayoutScope(options);
        public static HorizontalLayoutScope HzLayout(GUIStyle style, params GUILayoutOption[] options) => new HorizontalLayoutScope(style, options);
        public static VerticalLayoutScope VtLayout(params GUILayoutOption[] options) => new VerticalLayoutScope(options);
        public static VerticalLayoutScope VtLayout(GUIStyle style, params GUILayoutOption[] options) => new VerticalLayoutScope(style, options);
        public static ScrollViewScope ScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => new ScrollViewScope(scrollPosition, options);
        public static ScrollViewScope ScrollView(Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options) => new ScrollViewScope(scrollPosition, style, options);
        
        /// <summary>
        /// Dynamic layout scope wrapper - provides rect and automatic cleanup
        /// </summary>
        internal struct LayoutScope : IDisposable
        {
            public readonly Rect rect;
            private readonly bool _isHorz;
            
            public LayoutScope(bool isHorz, params GUILayoutOption[] options)
            {
                _isHorz = isHorz;
                rect = isHorz 
                    ? EditorGUILayout.BeginHorizontal(options)
                    : EditorGUILayout.BeginVertical(options);
            }
            
            public void Dispose()
            {
                if (_isHorz)
                    EditorGUILayout.EndHorizontal();
                else
                    EditorGUILayout.EndVertical();
            }
        }
        
        /// <summary>
        /// Dynamic layout scope - returns horizontal or vertical layout based on isHorz parameter
        /// </summary>
        public static LayoutScope Layout(bool isHorz, params GUILayoutOption[] options)
        {
            return new LayoutScope(isHorz, options);
        }
    }
}