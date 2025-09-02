using System;
using System.Collections.Generic;
using System.Linq;

namespace ObserveThing
{
    public class ContainsCollectionObservableReactive<T> : IValueObservable<bool>
    {
        private ICollectionObservable<T> _collection;
        private IValueObservable<T> _contains;

        public ContainsCollectionObservableReactive(ICollectionObservable<T> collection, IValueObservable<T> contains)
        {
            _collection = collection;
            _contains = contains;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<bool>> observer)
            => new Instance(_collection, _contains, observer);

        private class Instance : IDisposable
        {
            private IDisposable _collectionSource;
            private IDisposable _containsSource;
            private IObserver<IValueEventArgs<bool>> _observer;
            private ValueEventArgs<bool> _args = new ValueEventArgs<bool>();
            private bool _disposed = false;

            private T _contains;
            private int _count;
            private List<T> _collection = new List<T>();

            public Instance(ICollectionObservable<T> source, IValueObservable<T> contains, IObserver<IValueEventArgs<bool>> observer)
            {
                _observer = observer;
                _containsSource = contains.Subscribe(HandleContainsSourceChanged, HandleSourceError, HandleSourceDisposed);
                _collectionSource = source.Subscribe(HandleCollectionSourceChanged, HandleSourceError, HandleSourceDisposed);
            }

            private void HandleContainsSourceChanged(IValueEventArgs<T> args)
            {
                bool didContain = _count > 0;

                _contains = args.currentValue;
                _count = _collection.Count(x => Equals(x, _contains));

                bool currentlyContains = _count > 0;

                if (didContain == currentlyContains)
                    return;

                _args.previousValue = didContain;
                _args.currentValue = currentlyContains;
                _observer.OnNext(_args);
            }

            private void HandleCollectionSourceChanged(ICollectionEventArgs<T> args)
            {
                switch (args.operationType)
                {
                    case OpType.Add:

                        if (Equals(args.element, _contains))
                        {
                            _count++;
                            if (_count == 1)
                            {
                                _args.currentValue = true;
                                _args.previousValue = false;
                                _observer.OnNext(_args);
                            }
                        }

                        break;

                    case OpType.Remove:

                        _count--;
                        if (_count == 0)
                        {
                            _args.currentValue = false;
                            _args.previousValue = true;
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

                _containsSource.Dispose();
                _collectionSource.Dispose();
                _observer.OnDispose();
            }
        }
    }
}