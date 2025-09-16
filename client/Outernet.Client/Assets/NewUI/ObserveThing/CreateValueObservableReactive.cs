using System;

namespace ObserveThing
{
    public class CreateValueObservableReactive<T, U> : IValueObservable<U> where U : IDisposable
    {
        public IValueObservable<T> source;
        public Func<T, IValueObservable<U>> select;

        public CreateValueObservableReactive(IValueObservable<T> source, Func<T, IValueObservable<U>> select)
        {
            this.source = source;
            this.select = select;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<U>> observer)
            => new Instance(source, select, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private IDisposable _nestedSource;
            private Func<T, IValueObservable<U>> _select;
            private IObserver<IValueEventArgs<U>> _observer;
            private ValueEventArgs<U> _args = new ValueEventArgs<U>();
            private bool _initializeCalled = false;
            private bool _disposed = false;

            public Instance(IValueObservable<T> source, Func<T, IValueObservable<U>> select, IObserver<IValueEventArgs<U>> observer)
            {
                _select = select;
                _observer = observer;
                _source = source.Subscribe(HandleSourceChanged, HandleSourceError, HandleSourceDisposed);

                if (!_initializeCalled)
                    _observer.OnNext(_args); // we should always send an initial call, even if there's no change
            }

            private void HandleSourceChanged(IValueEventArgs<T> args)
            {
                _nestedSource?.Dispose();
                _nestedSource = _select(args.currentValue).Subscribe(HandleNestedSourceChanged, HandleSourceError);
            }

            private void HandleSourceError(Exception exception)
            {
                _observer.OnError(exception);
            }

            private void HandleSourceDisposed()
            {
                Dispose();
            }

            private void HandleNestedSourceChanged(IValueEventArgs<U> args)
            {
                _args.previousValue = _args.currentValue;
                _args.currentValue = args.currentValue;

                if (Equals(_args.currentValue, _args.previousValue))
                    return;

                _initializeCalled = true;
                _observer.OnNext(_args);
                _args.previousValue?.Dispose();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                _args.previousValue?.Dispose();
                _args.currentValue?.Dispose();
                _nestedSource?.Dispose();
                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}