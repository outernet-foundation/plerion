using System;

namespace ObserveThing
{
    public class TrackValueObservable<TKey, TValue> : IValueObservable<(bool keyPresent, TValue value)>
    {
        public IDictionaryObservable<TKey, TValue> source;
        public TKey key;

        public TrackValueObservable(IDictionaryObservable<TKey, TValue> source, TKey key)
        {
            this.source = source;
            this.key = key;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> observer)
            => new Instance(source, key, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private TKey _key;
            private IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> _observer;
            private ValueEventArgs<(bool keyPresent, TValue value)> _args = new ValueEventArgs<(bool keyPresent, TValue value)>();
            private bool _disposed = false;

            public Instance(IDictionaryObservable<TKey, TValue> source, TKey key, IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> observer)
            {
                _key = key;
                _observer = observer;
                _source = source.Subscribe(
                    HandleSourceChanged,
                    HandleSourceError,
                    HandleSourceDisposed
                );
            }

            private void HandleSourceChanged(IDictionaryEventArgs<TKey, TValue> args)
            {
                switch (args.operationType)
                {
                    case OpType.Add:

                        if (Equals(args.key, _key))
                        {
                            _args.previousValue = _args.currentValue;
                            _args.currentValue = new(true, args.value);
                            _observer.OnNext(_args);
                        }

                        break;

                    case OpType.Remove:

                        if (Equals(args.key, _key))
                        {
                            _args.previousValue = _args.currentValue;
                            _args.currentValue = new(false, default);
                            _observer.OnNext(_args);
                        }

                        break;
                }
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