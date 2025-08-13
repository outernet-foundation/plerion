using System;
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
                if (_settingValue || Equals(_value, value))
                    return;

                _value = value;
                _settingValue = true;
                HandleValueChanged();
                onChange?.Invoke(value);
                _settingValue = false;
            }
        }

        public event Action<T> onChange;
        private T _value;
        private bool _settingValue;

        public UnityValueControl(string name = null) : this(new GameObject(name)) { }
        public UnityValueControl(GameObject gameObject) : base(gameObject) { }

        protected virtual void HandleValueChanged() { }
    }
}
