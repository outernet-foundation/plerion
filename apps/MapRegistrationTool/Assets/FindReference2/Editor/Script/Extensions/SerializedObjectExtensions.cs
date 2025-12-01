using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    /// <summary>
    /// Extension methods for SerializedObject, Component, and GameObject to extract object references
    /// with both synchronous and asynchronous (frame-split) processing options
    /// </summary>
    [InitializeOnLoad]
    internal static class SerializedObjectExtensions
    {
        private static bool _isDirty = false;
        
        // Reusable collections to reduce GC allocations
        private static readonly List<UnityObject> _reusableResultList = new List<UnityObject>();
        private static readonly List<SerializedProperty> _reusablePropertyList = new List<SerializedProperty>();
        
        // SerializedObject cache to avoid repeated creation
        private static readonly Dictionary<int, SerializedObject> _serializedObjectCache = new Dictionary<int, SerializedObject>();
        private static readonly Dictionary<int, List<UnityObject>> _objectReferencesCache = new Dictionary<int, List<UnityObject>>();
        
        // Cache cleanup counter
        private static int _cacheCleanupCounter = 0;
        private const int CACHE_CLEANUP_INTERVAL = 100;
        
        static SerializedObjectExtensions()
        {
            // Clear caches on domain reload to prevent memory leaks
#if UNITY_2019_1_OR_NEWER
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearAllCaches;
#endif
        }
        
        /// <summary>
        /// Returns true if any async operations have marked data as dirty
        /// </summary>
        internal static bool IsDirty { get { return _isDirty; } }
        
        /// <summary>
        /// Clears the dirty flag - call this after handling dirty state
        /// </summary>
        internal static void ClearDirtyFlag() { _isDirty = false; }
        
        /// <summary>
        /// Clears all caches - call when memory pressure is high or on domain reload
        /// </summary>
        internal static void ClearAllCaches()
        {
            _serializedObjectCache.Clear();
            _objectReferencesCache.Clear();
            _cacheCleanupCounter = 0;
        }
        
        /// <summary>
        /// Gets or creates a cached SerializedObject for the given component
        /// </summary>
        private static SerializedObject GetOrCreateSerializedObject(Component component)
        {
            if (component == null) return null;
            
            int instanceId = component.GetInstanceID();
            if (_serializedObjectCache.TryGetValue(instanceId, out var cached))
            {
                if (cached != null && cached.targetObject != null) return cached;
                _serializedObjectCache.Remove(instanceId);
            }
            
            var serializedObject = new SerializedObject(component);
            _serializedObjectCache[instanceId] = serializedObject;
            
            // Periodic cache cleanup to prevent memory leaks
            if (++_cacheCleanupCounter >= CACHE_CLEANUP_INTERVAL)
            {
                CleanupCache();
                _cacheCleanupCounter = 0;
            }
            
            return serializedObject;
        }
        
        /// <summary>
        /// Removes null or destroyed objects from cache
        /// </summary>
        private static void CleanupCache()
        {
            var keysToRemove = new List<int>();
            
            foreach (var kvp in _serializedObjectCache)
            {
                if (kvp.Value == null || kvp.Value.targetObject == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (int key in keysToRemove)
            {
                _serializedObjectCache.Remove(key);
                _objectReferencesCache.Remove(key);
            }
        }
        
        /// <summary>
        /// Synchronously extracts all object references from a SerializedObject
        /// </summary>
        internal static IEnumerable<UnityObject> GetAllObjectReferences(this SerializedObject serializedObject)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (iterator.objectReferenceValue == null) continue;
                
                yield return iterator.objectReferenceValue;
            }
        }
        
        /// <summary>
        /// Asynchronously extracts all object references from a SerializedObject using frame-splitting
        /// </summary>
        internal static void GetAllObjectReferencesAsync(this SerializedObject serializedObject, Action<List<UnityObject>> onComplete)
        {
            var properties = CollectObjectReferenceProperties(serializedObject);
            if (properties.Count == 0)
            {
                _reusableResultList.Clear();
                if (onComplete != null) onComplete.Invoke(_reusableResultList);
                return;
            }
            
            _reusableResultList.Clear();
            var timeSlice = new FR2_TimeSlice(
                () => properties.Count,
                (index) => {
                    var prop = properties[index];
                    if (prop.objectReferenceValue != null)
                    {
                        _reusableResultList.Add(prop.objectReferenceValue);
                    }
                    _isDirty = true;
                },
                () => { if (onComplete != null) onComplete.Invoke(_reusableResultList); }
            );
            
            timeSlice.Start();
        }
        
        private static List<SerializedProperty> CollectObjectReferenceProperties(SerializedObject serializedObject)
        {
            using (ProfileMethod("FR2.CollectObjectReferenceProperties"))
            {
                _reusablePropertyList.Clear();
                var iterator = serializedObject.GetIterator();
                
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        _reusablePropertyList.Add(iterator.Copy());
                    }
                }
                
                return _reusablePropertyList;
            }
        }
        
        internal static IEnumerable<UnityObject> GetAllObjectReferences(this Component component)
        {
            if (component == null) yield break;
            
            int instanceId = component.GetInstanceID();
            
            // Check cache first
            if (_objectReferencesCache.TryGetValue(instanceId, out var cachedRefs))
            {
                foreach (var obj in cachedRefs)
                {
                    if (obj != null) yield return obj;
                }
                yield break;
            }
            
            // Use cached SerializedObject
            var serializedObject = GetOrCreateSerializedObject(component);
            if (serializedObject == null) yield break;
            
            var refs = new List<UnityObject>();
            foreach (var obj in serializedObject.GetAllObjectReferences())
            {
                refs.Add(obj);
                yield return obj;
            }
            
            // Cache the results
            _objectReferencesCache[instanceId] = refs;
        }
        
        internal static void GetAllObjectReferencesAsync(this Component component, Action<List<UnityObject>> onComplete)
        {
            if (!ValidateAndInvokeIfNull(component, onComplete)) return;
            
            var serializedObject = new SerializedObject(component);
            serializedObject.GetAllObjectReferencesAsync(onComplete);
        }
        
        internal static IEnumerable<UnityObject> GetAllObjectReferences(this GameObject gameObject)
        {
            if (gameObject == null) yield break;
            
            var components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;
                
                foreach (var obj in component.GetAllObjectReferences())
                {
                    yield return obj;
                }
            }
        }
        
        internal static void GetAllObjectReferencesAsync(this GameObject gameObject, Action<List<UnityObject>> onComplete)
        {
            if (!ValidateAndInvokeIfNull(gameObject, onComplete)) return;
            
            var components = gameObject.GetComponents<Component>();
            if (components.Length == 0)
            {
                _reusableResultList.Clear();
                if (onComplete != null) onComplete.Invoke(_reusableResultList);
                return;
            }
            
            ProcessComponentsAsync(components, onComplete);
        }
        
        private static void ProcessComponentsAsync(Component[] components, Action<List<UnityObject>> onComplete)
        {
            _reusableResultList.Clear();
            var completedCount = 0;
            
            foreach (var component in components)
            {
                component.GetAllObjectReferencesAsync(results => {
                    _reusableResultList.AddRange(results);
                    completedCount++;
                    _isDirty = true;
                    
                    if (completedCount >= components.Length)
                    {
                        if (onComplete != null) onComplete.Invoke(_reusableResultList);
                    }
                });
            }
        }
        
        private static bool ValidateAndInvokeIfNull<T>(T obj, Action<List<UnityObject>> onComplete) where T : UnityObject
        {
            if (obj == null)
            {
                _reusableResultList.Clear();
                if (onComplete != null) onComplete.Invoke(_reusableResultList);
                return false;
            }
            return true;
        }
        
        internal static SerializedProperty[] GetAllProperties(this SerializedObject serializedObject, bool processArrays = false)
        {
            serializedObject.Update();
            var result = new List<SerializedProperty>();
            
            SerializedProperty iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                SerializedProperty copy = iterator.Copy();
                
                if (processArrays && iterator.isArray)
                {
                    result.AddRange(GetArrayProperties(copy));
                }
                else
                {
                    result.Add(copy);
                }
            }
            
            return result.ToArray();
        }
        
        private static List<SerializedProperty> GetArrayProperties(SerializedProperty arrayProperty)
        {
            var result = new List<SerializedProperty>();
            int size = arrayProperty.arraySize;
            
            for (int i = 0; i < size; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                
                if (element.isArray)
                {
                    result.AddRange(GetArrayProperties(element.Copy()));
                }
                else
                {
                    result.Add(element.Copy());
                }
            }
            
            return result;
        }
    }
} 