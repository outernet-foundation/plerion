using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class DistinctCollectionObservable<T> : ICollectionObservable<T>
    {
        public ICollectionObservable<T> source;

        public DistinctCollectionObservable(ICollectionObservable<T> source)
        {
            this.source = source;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer)
            => new Instance(source, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private IObserver<ICollectionEventArgs<T>> _observer;
            private CollectionEventArgs<T> _args = new CollectionEventArgs<T>();
            private bool _disposed = false;

            private Dictionary<T, int> _elements = new Dictionary<T, int>();

            public Instance(ICollectionObservable<T> source, IObserver<ICollectionEventArgs<T>> observer)
            {
                _observer = observer;
                _source = source.Subscribe(
                    HandleSourceChanged,
                    HandleSourceError,
                    HandleSourceDisposed
                );
            }

            private void HandleSourceChanged(ICollectionEventArgs<T> args)
            {
                switch (args.operationType)
                {
                    case OpType.Add:
                        {

                            _elements.TryGetValue(args.element, out var count);

                            count++;
                            _elements[args.element] = count;

                            if (count == 1)
                            {
                                _args.operationType = OpType.Add;
                                _args.element = args.element;
                                _observer.OnNext(_args);
                            }
                        }

                        break;

                    case OpType.Remove:
                        {
                            int count = _elements[args.element];

                            count--;
                            _elements[args.element] = count;

                            if (count == 0)
                            {
                                _elements.Remove(args.element);
                                _args.operationType = OpType.Remove;
                                _args.element = args.element;
                                _observer.OnNext(_args);
                            }
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