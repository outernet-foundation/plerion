using System;

namespace Nessle
{
    public class ControlWithMetadata<TControl, TData> : IControl where TControl : IControl
    {
        public TControl control;
        public TData metadata;

        public IControl parent => control.parent;

        public void AddBinding(IDisposable binding)
            => control.AddBinding(binding);

        public void Dispose()
            => control.Dispose();

        public void RemoveBinding(IDisposable binding)
            => control.RemoveBinding(binding);

        public void SetSiblingIndex(int index)
            => control.SetSiblingIndex(index);

        public void SetParent(IControl parent)
            => control.SetParent(parent);
    }
}
