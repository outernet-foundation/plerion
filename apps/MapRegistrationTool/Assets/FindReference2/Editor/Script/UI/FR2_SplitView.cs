//#define FR2_DEBUG

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
namespace vietlabs.fr2
{

    internal class FR2_SplitView
    {
        private const float SPLIT_SIZE = 2f;


        private readonly GUILayoutOption[] expandWH =
        {
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)
        };


        private readonly IWindow window;
        private Rect _rect;

        private int _visibleCount;
        internal bool dirty;

        public bool isHorz;

        private int resizeIndex = -1;
        public List<Info> splits = new List<Info>();
        
        public System.Action OnSplitterChanged;

        public FR2_SplitView(IWindow w)
        {
            window = w;
        }
        
        public void SetupSplitParents()
        {
            for (int i = 0; i < splits.Count; i++)
            {
                splits[i].SetParent(this);
            }
        }
        
        public void SetSplitVisible(int index, bool visible)
        {
            if (index < 0 || index >= splits.Count) return;
            splits[index].visible = visible;
        }

        public bool isVisible => _visibleCount > 0;

        public void CalculateWeight()
        {
            _visibleCount = 0;
            var _totalWeight = 0f;

            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                info.stIndex = _visibleCount;
                _totalWeight += info.weight;

                _visibleCount++;
            }

            if (_visibleCount == 0 || _totalWeight == 0)
            {
                Debug.LogWarning("Nothing visible!");
                return;
            }

            var cWeight = 0f;
            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                cWeight += info.weight;
                info.normWeight = info.weight / _totalWeight;
            }
        }

        public void DrawLayout()
        {
            using (var layoutScope = FR2_Scope.Layout(isHorz, expandWH))
            {
                Draw(layoutScope.rect);
            }
        }

        public void Draw(Rect rect)
        {
            bool rectChanged = false;
            if (rect.width > 0 || rect.height > 0) 
            {
                rectChanged = !_rect.Equals(rect);
                _rect = rect;
            }

            bool wasDirty = dirty;
            if (dirty)
            {
                dirty = false;
                CalculateWeight();
            }

            // Apply size policies when rect changes, was dirty, or during resize operations
            if (resizeIndex == -1 && (rectChanged || wasDirty)) ApplySizePolicies();

            float sz = (_visibleCount - 1) * SPLIT_SIZE;
            float dx = _rect.x;
            float dy = _rect.y;

            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                var rr = new Rect
                (
                    dx, dy,
                    isHorz ? (_rect.width - sz) * info.normWeight : _rect.width,
                    isHorz ? _rect.height : (_rect.height - sz) * info.normWeight
                );

                if ((rr.width > 0) && (rr.height > 0)) info.rect = rr;

                if (info.draw != null) info.DoDraw();

                // Only update preferredPixel during user resize operations, not every frame
                if (info.sizePolicy == Info.SizePolicy.KeepPixel && resizeIndex != -1)
                {
                    float current = isHorz ? info.rect.width : info.rect.height;
                    float oldPreferred = info.preferredPixel;
                    info.preferredPixel = Mathf.Max(info.minPixel, current);
                    
                    // Log significant changes (avoid spam during normal drawing)
                    if (Mathf.Abs(oldPreferred - info.preferredPixel) > 1f)
                    {
                        string name = info.title?.text ?? "Unknown";
                        // Debug.Log($"[RESIZE_DEBUG] User resize - {name}: {oldPreferred:F1} -> {info.preferredPixel:F1}");
                    }
                }

                if (info.stIndex < _visibleCount - 1) DrawSpliter(i, isHorz ? info.rect.xMax : info.rect.yMax);

                if (isHorz)
                {
                    dx += info.rect.width + SPLIT_SIZE;
                } else
                {
                    dy += info.rect.height + SPLIT_SIZE;
                }
            }
        }

        public void ApplySizePolicies()
        {
            int visible = 0;
            for (int i = 0; i < splits.Count; i++) if (splits[i].visible) visible++;
            if (visible == 0) return;

            float totalGaps = (visible - 1) * SPLIT_SIZE;
            float available = isHorz ? _rect.width : _rect.height;
            float content = Mathf.Max(0f, available - totalGaps);
            

            float fixedPixels = 0f;
            float flexibleBasis = 0f;
            for (int i = 0; i < splits.Count; i++)
            {
                var sp = splits[i];
                if (!sp.visible) continue;
                if (sp.sizePolicy == Info.SizePolicy.KeepPixel)
                {
                    float pref = sp.preferredPixel > 0f ? sp.preferredPixel : sp.defaultPixel;
                    pref = Mathf.Max(sp.minPixel, pref);
                    fixedPixels += Mathf.Max(0f, pref);
                }
                else
                {
                    flexibleBasis += Mathf.Max(0.0001f, sp.weight);
                }
            }

            float scale = 1f;
            if (fixedPixels > content && fixedPixels > 0f) scale = content / fixedPixels;
            float remaining = Mathf.Max(0f, content - Mathf.Min(fixedPixels, content));
            

            for (int i = 0; i < splits.Count; i++)
            {
                var sp = splits[i];
                if (!sp.visible) continue;
                if (sp.sizePolicy == Info.SizePolicy.KeepPixel)
                {
                    float pref = sp.preferredPixel > 0f ? sp.preferredPixel : sp.defaultPixel;
                    float originalPref = pref;
                    pref = Mathf.Max(sp.minPixel, pref);
                    
                    // Apply scaling but ensure we never go below minPixel
                    float scaledSize = pref * scale;
                    sp.weight = Mathf.Max(sp.minPixel, scaledSize);
                    
                    string name = sp.title?.text ?? $"Panel{i}";
                }
            }

            if (remaining > 0f)
            {
                // First, calculate minimum space needed for flexible panels
                float minFlexibleSpace = 0f;
                int flexibleCount = 0;
                for (int i = 0; i < splits.Count; i++)
                {
                    var sp = splits[i];
                    if (!sp.visible) continue;
                    if (sp.sizePolicy == Info.SizePolicy.Flexible)
                    {
                        minFlexibleSpace += sp.minPixel;
                        flexibleCount++;
                    }
                }

                // If we have enough space for all minPixel requirements, distribute normally
                if (remaining >= minFlexibleSpace)
                {
                    float basis = Mathf.Max(0.0001f, flexibleBasis);
                    for (int i = 0; i < splits.Count; i++)
                    {
                        var sp = splits[i];
                        if (!sp.visible) continue;
                        if (sp.sizePolicy == Info.SizePolicy.Flexible)
                        {
                            float portion = sp.weight / basis;
                            sp.weight = Mathf.Max(sp.minPixel, remaining * portion);
                        }
                    }
                }
                else
                {
                    // Not enough space - distribute minPixel proportionally
                    for (int i = 0; i < splits.Count; i++)
                    {
                        var sp = splits[i];
                        if (!sp.visible) continue;
                        if (sp.sizePolicy == Info.SizePolicy.Flexible)
                        {
                            if (minFlexibleSpace > 0f)
                            {
                                float portion = sp.minPixel / minFlexibleSpace;
                                sp.weight = remaining * portion;
                            }
                            else
                            {
                                sp.weight = remaining / flexibleCount;
                            }
                        }
                    }
                }
            }

            CalculateWeight();
        }

        private void RefreshSpliterPos(int index, float px)
        {
			Info sp1 = splits[index];
			int rightIndex = -1;
            for (int j = index + 1; j < splits.Count; j++)
            {
                if (splits[j].visible)
                {
                    rightIndex = j;
                    break;
                }
            }
            if (rightIndex < 0) return;
			Info sp2 = splits[rightIndex];

            Rect r1 = sp1.rect;
            Rect r2 = sp2.rect;

            float w1 = sp1.weight;
            float w2 = sp2.weight;
            float tt = w1 + w2;

			float dd = isHorz ? r2.xMax - r1.xMin - SPLIT_SIZE : r2.yMax - r1.yMin - SPLIT_SIZE;
			float m = isHorz ? Event.current.mousePosition.x - r1.x : Event.current.mousePosition.y - r1.y;

			// Enforce minimum pixel sizes for panels that prefer keeping pixel size
			float leftMin = 0f;
			float rightMin = 0f;
			if (sp1.sizePolicy == Info.SizePolicy.KeepPixel) leftMin = Mathf.Max(0f, sp1.minPixel);
			if (sp2.sizePolicy == Info.SizePolicy.KeepPixel) rightMin = Mathf.Max(0f, sp2.minPixel);
			float lower = Mathf.Min(dd - rightMin, leftMin);
			float upper = Mathf.Max(leftMin, dd - rightMin);
			m = Mathf.Clamp(m, lower, upper);
            float pct = Mathf.Min(0.9f, Mathf.Max(0.1f, m / dd));

            sp1.weight = tt * pct;
            sp2.weight = tt * (1 - pct);

            dirty = true;
            if (window != null) window.WillRepaint = true;
            
            // Notify that splitter position changed
            OnSplitterChanged?.Invoke();
        }

        private void DrawSpliter(int index, float px)
        {
            Rect dRect = _rect;

            if (isHorz)
            {
                dRect.x = px;
                dRect.width = SPLIT_SIZE;
            } else
            {
                dRect.y = px;
                dRect.height = SPLIT_SIZE;
            }

            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseMove) GUI2.Rect(dRect, Color.black, 0.4f);

            Rect dRect2 = GUI2.Padding(dRect, -2f, -2f);

            EditorGUIUtility.AddCursorRect(dRect2, isHorz ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
            if ((Event.current.type == EventType.MouseDown) && dRect2.Contains(Event.current.mousePosition))
            {
                resizeIndex = index;
                RefreshSpliterPos(index, px);
            }

            if (resizeIndex == index) RefreshSpliterPos(index, px);

            if (Event.current.type == EventType.MouseUp) resizeIndex = -1;
        }
        
        [Serializable]
        internal class Info
        {
            public GUIContent title;
            public Rect rect;
            public float normWeight;
            public int stIndex;

            private bool _visible = true;
            private FR2_SplitView _parent;
            
            public bool visible
            {
                get => _visible;
                set
                {
                    if (_visible != value)
                    {
                        _visible = value;
                        if (_parent != null)
                        {
                            _parent.dirty = true;
                            if (_parent.window != null) _parent.window.WillRepaint = true;
                        }
                    }
                }
            }
            
            public float weight = 1f;
            public Action<Rect> draw;

            public enum SizePolicy { Flexible, KeepPixel }
            public SizePolicy sizePolicy = SizePolicy.Flexible;
            public float preferredPixel;
            public float defaultPixel = 200f;
            public float minPixel = 50f;

            // Dynamic title support
            public Func<GUIContent> GetDynamicTitle;

            // Drawer dirty state support
            public Func<bool> GetDrawerDirtyState;
            
            // Refresh action support
            public Action OnRefresh;
            
            internal void SetParent(FR2_SplitView parent)
            {
                _parent = parent;
            }

            public void DoDraw()
            {
                Rect drawRect = rect;

                // Use dynamic title if available, otherwise use static title
                GUIContent baseTitle = GetDynamicTitle?.Invoke() ?? title;

                if (baseTitle != null)
                {
                    var titleRect = new Rect(rect.x, rect.y, rect.width, 20f);
                    GUI2.Rect(titleRect, Color.black, 0.2f);

                    titleRect.xMin += 4f;

                    // Check dirty state and modify title accordingly
                    bool isDirty = GetDrawerDirtyState?.Invoke() ?? false;
                    
                    // Create title with asterisk if dirty
                    string titleText = baseTitle.text;
                    Color? titleColor = null;
                    if (isDirty && !titleText.EndsWith("*"))
                    {
                        titleText += "*";
                        // Use theme-appropriate dirty indicator color instead of hardcoded yellow
                        titleColor = EditorGUIUtility.isProSkin 
                            ? FR2_Theme.Dark.DirtyIndicator 
                            : FR2_Theme.Light.DirtyIndicator;
                    }

                    // Calculate available space for refresh button only if OnRefresh is available
                    // Increased button width from 50f to 55f, adjusted status area accordingly
                    float statusAreaWidth = OnRefresh != null ? (isDirty ? 205f : 55f) : 0f;
                    
                    using (FR2_Scope.ContentColor(titleColor))
                    {
                        // Draw main title
                        var mainTitleRect = new Rect(titleRect.x, titleRect.y, titleRect.width - statusAreaWidth, titleRect.height);
                        GUI.Label(mainTitleRect, FR2_GUIContent.From(titleText, baseTitle.image, baseTitle.tooltip), EditorStyles.label);

                        // Draw status message and refresh button only if OnRefresh is available
                        if (OnRefresh != null)
                        {
                            if (isDirty)
                            {
                                // Status message - shorter text to fit
                                var statusRect = new Rect(mainTitleRect.xMax + 5f, titleRect.y + 2f, 145f, titleRect.height);
                                using (FR2_Scope.ContentColor(new Color(0.7f, 0.7f, 0.7f, 0.8f))) // Very dim color
                                {
                                    GUI.Label(statusRect, "* possibly incomplete result", EditorStyles.miniLabel);
                                }
                            }
                            
                            // Increased refresh button width from 50f to 55f
                            var refreshRect = new Rect(titleRect.xMax - 55f, titleRect.y + 1f, 53f, 14f);
                            if (GUI.Button(refreshRect, "Refresh", EditorStyles.miniButtonRight))
                            {
                                OnRefresh.Invoke();
                            }
                        }
                    }
                    drawRect.yMin += 20f;
                }
                
                try
                {
                    draw(drawRect);
                }
                catch (System.Exception e)
                {
                    // Log the exception but don't let it break the GUI layout system
                    UnityEngine.Debug.LogException(e);
                    
                    // Draw error message in the panel instead
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.HelpBox(drawRect, $"Drawing error: {e.Message}", MessageType.Error);
                    }
                }
            }
        }
    }
}
