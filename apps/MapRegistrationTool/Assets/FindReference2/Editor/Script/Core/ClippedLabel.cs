#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal class ClippedLabel
    {
        private static readonly Dictionary<GUIStyle, ClippedLabel> _instances = new Dictionary<GUIStyle, ClippedLabel>();
        private static ClippedLabel Get(GUIStyle style, bool autoNew = true)
        {
            if (_instances.TryGetValue(style, out var result)) return result;
            if (!autoNew) return null;

            result = new ClippedLabel(style);
            _instances[style] = result;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Draw(Rect rect, string value, GUIStyle style = null, float minW = 50f)
        {
            return Get(style ?? EditorStyles.label, true).Draw(rect, value, minW);
        }
        
        private readonly GUIStyle _style;
        private readonly GUIContent _ellipsis;
        private readonly float _ellipsisW;
        private readonly Dictionary<string, (GUIContent content, float w)> _cache 
            = new Dictionary<string, (GUIContent, float)>();
        
        private ClippedLabel(GUIStyle style)
        { 
            _style = style;
            _ellipsis = new GUIContent("...");
            _ellipsisW = _style.CalcSize(_ellipsis).x + 2f;
        }

        private float Draw(Rect rect, string value, float minW = 100f)
        {
            if (!_cache.TryGetValue(value, out var item))
            {
                item.content = new GUIContent(value);
                item.w = _style.CalcSize(item.content).x;
                _cache[value] = item;
            }

            var (content, width) = item;
            var clip = rect.width < width && (width > minW);
            rect.width = clip ? Mathf.Max(minW, rect.width - _ellipsisW) : width;
            if (Event.current.type == EventType.Repaint) GUI.Label(rect, content, _style);
            if (!clip) return rect.width;
            
            // Draw ellipsis
            var eRect = new Rect(rect.xMax, rect.y, _ellipsisW, rect.height);
            if (Event.current.type == EventType.Repaint) GUI.Label(eRect, _ellipsis, _style);
            return rect.width + _ellipsisW;
        }
    }
}
#endif
