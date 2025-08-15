using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nessle
{
    public interface IUnityControl : IControl
    {
        GameObject gameObject { get; }
        RectTransform rectTransform { get; }
        void AddChild(IUnityControl child);
        void RemoveChild(IUnityControl child);
    }

    public interface IUnityComponentControl<out T> : IUnityControl where T : Component
    {
        T component { get; }
    }

    public class UnityComponentControl<T> : UnityControl, IUnityComponentControl<T> where T : Component
    {
        public T component { get; private set; }

        public UnityComponentControl(string name = null) : this(new GameObject(name)) { }
        public UnityComponentControl(GameObject gameObject) : this(gameObject.AddComponent<T>()) { }
        public UnityComponentControl(T component) : base(component.gameObject)
        {
            this.component = component;
        }
    }

    public class UnityControl : IUnityControl
    {
        public IControl parent { get; private set; }
        public GameObject gameObject { get; private set; }
        public RectTransform rectTransform => (RectTransform)gameObject.transform;

        private HashSet<IControl> _children = new HashSet<IControl>();
        private List<IDisposable> _bindings = new List<IDisposable>();

        public UnityControl(string name = null) : this(new GameObject(name)) { }
        public UnityControl(GameObject gameObject)
        {
            if (gameObject == null)
                gameObject = new GameObject();

            this.gameObject = gameObject;
        }

        public void Dispose()
        {
            foreach (var child in _children)
                child.Dispose();

            foreach (var binding in _bindings)
                binding.Dispose();

            UnityEngine.Object.Destroy(gameObject);
        }

        public void AddBinding(IDisposable binding)
        {
            _bindings.Add(binding);
        }

        public void RemoveBinding(IDisposable binding)
        {
            _bindings.Remove(binding);
        }

        void IUnityControl.AddChild(IUnityControl child)
        {
            if (child.parent != this)
            {
                child.SetParent(this);
                return;
            }

            _children.Add(child);
            child.gameObject.transform.SetParent(gameObject.transform, false);
        }

        void IUnityControl.RemoveChild(IUnityControl child)
        {
            if (child.parent == this)
            {
                child.SetParent(null);
                return;
            }

            _children.Remove(child);
            child.gameObject.transform.SetParent(null, false);
        }

        public void SetParent(IControl parent)
        {
            if (parent == null)
            {
                if (this.parent != null)
                {
                    var prevParent = (IUnityControl)this.parent;
                    this.parent = null;
                    prevParent.RemoveChild(this);
                }

                return;
            }

            if (parent is not IUnityControl parentControl)
                throw new Exception(parent + " is not a unity control");

            if (this.parent != null)
            {
                var prevParent = (IUnityControl)this.parent;
                this.parent = null;
                prevParent.RemoveChild(this);
            }

            this.parent = parentControl;

            if (this.parent != null)
                parentControl.AddChild(this);
        }

        public void SetSiblingIndex(int index)
        {
            gameObject.transform.SetSiblingIndex(index);
        }
    }
}