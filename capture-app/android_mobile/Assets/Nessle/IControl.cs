using System;
using ObserveThing;

namespace Nessle
{
    public interface IControl : IDisposable
    {
        IControl parent { get; }
        void SetParent(IControl parent);
        void SetSiblingIndex(int index);
        void AddBinding(IDisposable binding);
        void RemoveBinding(IDisposable binding);
    }

    public interface IValueControl<T> : IControl, IValueObservable<T>
    {
        T value { get; set; }
    }
}
