using System;
using System.Linq;
using FofX.Serialization;
using ObserveThing;
using SimpleJSON;

namespace FofX.Stateful
{
    public interface IObservablePrimitive : IObservableNode
    {
        Type primitiveType { get; }
        object GetValue();
        void SetValue(object value);
    }

    public sealed class ObservablePrimitive<T> : ObservableNode, IObservablePrimitive, IValueObservable<T>
    {
        private T _value;
        public T value
        {
            get => _value;
            set
            {
                if (!object.Equals(_value, value))
                {
                    var prev = _value;
                    _value = value;
                    context.RegisterNodeChange(this, ChangeType.Set, currentValue: value, previousValue: prev);
                }
            }
        }

        Type IObservablePrimitive.primitiveType => typeof(T);
        public override bool isDefault => Equals(value, default(T));

        public ObservablePrimitive() { }

        public ObservablePrimitive(T value)
        {
            _value = value;
        }

        public override void ApplyChange(NodeChangeData change)
        {
            if (change.changeType == ChangeType.Dispose)
                return;

            if (change.changeType == ChangeType.Set)
            {
                value = (T)change.currentValue;
            }
            else
            {
                throw new Exception($"{nameof(ObservablePrimitive<T>)} does not support change type {change.changeType}");
            }
        }

        public override void CopyTo(IObservableNode copyTo)
        {
            if (copyTo is not ObservablePrimitive<T> copyToPrimitive)
                throw new Exception($"{copyTo.nodePath} is not a {nameof(ObservablePrimitive<T>)}");

            copyToPrimitive.value = value;
        }

        public override void Reset()
        {
            value = default;
        }

        object IObservablePrimitive.GetValue()
        {
            return value;
        }

        void IObservablePrimitive.SetValue(object value)
        {
            this.value = (T)value;
        }

        public override JSONNode ToJSON(Func<IObservableNode, bool> filter)
        {
            return JSONSerialization.ToJSON(value);
        }

        public override void FromJSON(JSONNode json)
        {
            if (json == null)
            {
                Reset();
                return;
            }

            value = JSONSerialization.FromJSON<T>(json);
        }

        IDisposable IValueObservable<T>.Subscribe(ObserveThing.IObserver<IValueEventArgs<T>> observer)
            => new Instance(this, observer);

        private class Instance : IDisposable
        {
            private ObservablePrimitive<T> _primitive;
            private ObserveThing.IObserver<IValueEventArgs<T>> _observer;
            private ValueEventArgs<T> _args = new ValueEventArgs<T>();

            public Instance(ObservablePrimitive<T> primitive, ObserveThing.IObserver<IValueEventArgs<T>> observer)
            {
                _primitive = primitive;
                _observer = observer;

                _primitive.context.RegisterObserver(HandlePrimitiveChanged, _primitive);
            }

            private void HandlePrimitiveChanged(NodeChangeEventArgs args)
            {
                if (!args.initialize && args.changes.Any(x => x.changeType == ChangeType.Dispose))
                {
                    _primitive.context.DeregisterObserver(HandlePrimitiveChanged);
                    _observer.OnDispose();
                    return;
                }

                _args.previousValue = _args.currentValue;
                _args.currentValue = _primitive.value;
                _observer.OnNext(_args);
            }

            public void Dispose()
            {
                _primitive.context.DeregisterObserver(HandlePrimitiveChanged);
                _observer.OnDispose();
            }
        }
    }
}
