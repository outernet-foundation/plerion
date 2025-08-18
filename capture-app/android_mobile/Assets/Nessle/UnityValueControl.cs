using System;
using System.Collections.Generic;
using ObserveThing;
using UnityEngine;

namespace Nessle
{
    public class UnityValueControl<T> : UnityControl, IValueControl<T>
    {
        public T value
        {
            get => _value;
            set
            {
                if (Equals(_value, value))
                    return;

                _value = value;
                HandleValueChanged();

                _args.previousValue = _args.currentValue;
                _args.currentValue = value;

                foreach (var observer in _observers)
                    observer.OnNext(_args);
            }
        }

        private T _value;
        private ValueEventArgs<T> _args = new ValueEventArgs<T>();
        private List<ObserveThing.IObserver<IValueEventArgs<T>>> _observers = new List<ObserveThing.IObserver<IValueEventArgs<T>>>();

        public UnityValueControl(string name = null) : this(new GameObject(name)) { }
        public UnityValueControl(GameObject gameObject) : base(gameObject) { }

        protected virtual void HandleValueChanged() { }

        public IDisposable Subscribe(ObserveThing.IObserver<IValueEventArgs<T>> observer)
        {
            _observers.Add(observer);
            observer.OnNext(_args);
            return new Disposable(() => _observers.Remove(observer));
        }
    }
}
