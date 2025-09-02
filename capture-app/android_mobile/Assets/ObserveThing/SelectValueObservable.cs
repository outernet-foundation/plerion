using System;

namespace ObserveThing
{
    public class SelectValueObservable<T, U> : IValueObservable<U>
    {
        public IValueObservable<T> source;
        public Func<T, U> select;

        public SelectValueObservable(IValueObservable<T> source, Func<T, U> select)
        {
            this.source = source;
            this.select = select;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<U>> observer)
            => new Instance(source, select, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private Func<T, U> _select;
            private IObserver<IValueEventArgs<U>> _observer;
            private ValueEventArgs<U> _args = new ValueEventArgs<U>();
            private bool _initializeCalled = false;
            private bool _disposed = false;

            public Instance(IValueObservable<T> source, Func<T, U> select, IObserver<IValueEventArgs<U>> observer)
            {
                _select = select;
                _observer = observer;
                _source = source.Subscribe(HandleSourceChanged, HandleSourceError, HandleSourceDisposed);

                if (!_initializeCalled)
                    _observer.OnNext(_args); // we should always send an initial call, even if there's no change
            }

            private void HandleSourceChanged(IValueEventArgs<T> args)
            {
                _args.previousValue = _args.currentValue;
                _args.currentValue = _select(args.currentValue);

                if (Equals(_args.currentValue, _args.previousValue))
                    return;

                _initializeCalled = true;
                _observer.OnNext(_args);
            }

            private void HandleSourceError(Exception error)
            {
                _observer.OnError(error);
            }

            private void HandleSourceDisposed()
            {
                Dispose();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}