using System;

namespace ObserveThing
{
    public class WithValueObservable<T1, T2> : IValueObservable<(T1 value1, T2 value2)>
    {
        private IValueObservable<T1> _source1;
        private IValueObservable<T2> _source2;

        public WithValueObservable(IValueObservable<T1> source1, IValueObservable<T2> source2)
        {
            _source1 = source1;
            _source2 = source2;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<(T1 value1, T2 value2)>> observer)
            => new Instance(_source1, _source2, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source1;
            private IDisposable _source2;
            private IObserver<IValueEventArgs<(T1 value1, T2 value2)>> _observer;
            private ValueEventArgs<(T1 value1, T2 value2)> _args = new ValueEventArgs<(T1 value1, T2 value2)>();
            private bool _awaitingInit = true;
            private bool _disposed = false;

            public Instance(IValueObservable<T1> source1, IValueObservable<T2> source2, IObserver<IValueEventArgs<(T1 value1, T2 value2)>> observer)
            {
                _observer = observer;
                _source1 = source1.Subscribe(HandleSource1Changed, HandleSourceError, HandleSourceDisposed);
                _source2 = source2.Subscribe(HandleSource2Changed, HandleSourceError, HandleSourceDisposed);
            }

            private void HandleSource1Changed(IValueEventArgs<T1> args)
            {
                if (_awaitingInit)
                {
                    _args.currentValue = new(args.currentValue, default);
                    return;
                }

                _args.previousValue = _args.currentValue;
                _args.currentValue = new(args.currentValue, _args.previousValue.value2);
                _observer.OnNext(_args);
            }

            private void HandleSource2Changed(IValueEventArgs<T2> args)
            {
                if (_awaitingInit)
                {
                    _args.currentValue = new(_args.currentValue.value1, args.currentValue);
                    _awaitingInit = false;
                    _observer.OnNext(_args);
                    return;
                }

                _args.previousValue = _args.currentValue;
                _args.currentValue = new(_args.previousValue.value1, args.currentValue);
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

                _source1.Dispose();
                _source2.Dispose();
                _observer.OnDispose();
            }
        }
    }
}
