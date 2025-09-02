using System;

namespace ObserveThing
{
    public class ContainsCollectionObservable<T> : IValueObservable<bool>
    {
        private ICollectionObservable<T> _collection;
        private T _contains;

        public ContainsCollectionObservable(ICollectionObservable<T> collection, T contains)
        {
            _collection = collection;
            _contains = contains;
        }

        public IDisposable Subscribe(IObserver<IValueEventArgs<bool>> observer)
            => new Instance(_collection, _contains, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private T _contains;
            private IObserver<IValueEventArgs<bool>> _observer;
            private ValueEventArgs<bool> _args = new ValueEventArgs<bool>();
            private bool _disposed = false;
            private int _count;

            public Instance(ICollectionObservable<T> source, T contains, IObserver<IValueEventArgs<bool>> observer)
            {
                _contains = contains;
                _observer = observer;
                _source = source.Subscribe(HandleSourceChanged, HandleSourceError, HandleSourceDisposed);
            }

            private void HandleSourceChanged(ICollectionEventArgs<T> args)
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

                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}