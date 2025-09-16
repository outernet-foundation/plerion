using System;

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

    public interface IValueControl<T> : IControl
    {
        T value { get; set; }
        public event Action<T> onChange;
    }
}
