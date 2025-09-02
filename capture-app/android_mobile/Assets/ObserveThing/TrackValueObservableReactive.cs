using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class TrackValueObservableReactive<TKey, TValue> : IValueObservable<(bool keyPresent, TValue value)>
    {
        public IDictionaryObservable<TKey, TValue> source;
        public IValueObservable<TKey> key;

        public TrackValueObservableReactive(IDictionaryObservable<TKey, TValue> source, IValueObservable<TKey> key)
        {
            this.source = source;
            this.key = key;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> observer)
            => new Instance(source, key, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private IDisposable _key;
            private IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> _observer;
            private ValueEventArgs<(bool keyPresent, TValue value)> _args = new ValueEventArgs<(bool keyPresent, TValue value)>();
            private bool _awaitingInit = true;
            private bool _disposed = false;

            private Dictionary<TKey, TValue> _currentDict = new Dictionary<TKey, TValue>();
            private TKey _currentKey;

            public Instance(IDictionaryObservable<TKey, TValue> source, IValueObservable<TKey> key, IObserver<IValueEventArgs<(bool keyPresent, TValue value)>> observer)
            {
                _observer = observer;

                _source = source.Subscribe(
                    HandleSourceChanged,
                    HandleSourceOrKeyError,
                    HandleSourceOrKeyDisposed
                );

                _key = key.Subscribe(
                    HandleKeyChanged,
                    HandleSourceOrKeyError,
                    HandleSourceOrKeyDisposed
                );
            }

            private void HandleSourceChanged(IDictionaryEventArgs<TKey, TValue> args)
            {
                switch (args.operationType)
                {
                    case OpType.Add:

                        _currentDict.Add(args.key, args.value);

                        if (!_awaitingInit && Equals(args.key, _currentKey))
                        {
                            _args.previousValue = _args.currentValue;
                            _args.currentValue = new(true, args.value);
                            _observer.OnNext(_args);
                        }

                        break;

                    case OpType.Remove:

                        _currentDict.Remove(args.key);

                        if (!_awaitingInit && Equals(args.key, _currentKey))
                        {
                            _args.previousValue = _args.currentValue;
                            _args.currentValue = new(false, default);
                            _observer.OnNext(_args);
                        }

                        break;
                }
            }

            private void HandleKeyChanged(IValueEventArgs<TKey> args)
            {
                _currentKey = args.currentValue;
                _awaitingInit = false;

                if (_currentDict.TryGetValue(args.currentValue, out var value))
                {
                    _args.previousValue = _args.currentValue;
                    _args.currentValue = new(true, value);
                    _observer.OnNext(_args);
                }
                else if (_args.currentValue.keyPresent)
                {
                    _args.previousValue = _args.currentValue;
                    _args.currentValue = new(false, default);
                    _observer.OnNext(_args);
                }
            }

            private void HandleSourceOrKeyError(Exception error)
            {
                _observer.OnError(error);
            }

            private void HandleSourceOrKeyDisposed()
            {
                Dispose();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                _source.Dispose();
                _key.Dispose();
                _observer.OnDispose();
            }
        }
    }
}