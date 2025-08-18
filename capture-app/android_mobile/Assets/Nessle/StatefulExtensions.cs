using ObserveThing;
using FofX.Stateful;
using System;

namespace Nessle
{
    public static class StatefulExtensions
    {
        public static TControl BindValue<TControl, TView, TState>(this TControl valueObservable, ObservablePrimitive<TState> primitive, Func<TView, TState> toState, Func<TState, TView> toView)
            where TControl : IValueControl<TView>
        {
            valueObservable.AddBinding(primitive.Subscribe(x => valueObservable.value = toView(x.currentValue)));
            valueObservable.AddBinding(valueObservable.Subscribe(x => primitive.ExecuteSetOrDelay(toState(x.currentValue))));

            return valueObservable;
        }

        public static TControl BindValue<TControl, TValue>(this TControl valueObservable, ObservablePrimitive<TValue> primitive)
            where TControl : IValueControl<TValue>
        {
            valueObservable.AddBinding(primitive.Subscribe(x => valueObservable.value = x.currentValue));
            valueObservable.AddBinding(valueObservable.Subscribe(x => primitive.ExecuteSetOrDelay(x.currentValue)));

            return valueObservable;
        }
    }
}