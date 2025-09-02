using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class ConcatCollectionObservable<T> : ICollectionObservable<T>
    {
        public ICollectionObservable<T> source;
        public IEnumerable<T> concat;

        public ConcatCollectionObservable(ICollectionObservable<T> source, IEnumerable<T> concat)
        {
            this.source = source;
            this.concat = concat;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer)
            => new Instance(source, concat, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private IObserver<ICollectionEventArgs<T>> _observer;
            private CollectionEventArgs<T> _args = new CollectionEventArgs<T>();
            private bool _disposed = false;

            public Instance(ICollectionObservable<T> source, IEnumerable<T> concat, IObserver<ICollectionEventArgs<T>> observer)
            {
                _observer = observer;
                _source = source.Subscribe(
                    HandleSourceChanged,
                    HandleSourceError,
                    HandleSourceDisposed
                );

                _args.operationType = OpType.Add;

                foreach (var element in concat)
                {
                    _args.element = element;
                    _observer.OnNext(_args);
                }
            }

            private void HandleSourceChanged(ICollectionEventArgs<T> args)
            {
                _args.operationType = args.operationType;

                switch (args.operationType)
                {
                    case OpType.Add:

                        _args.element = args.element;
                        _observer.OnNext(_args);

                        break;

                    case OpType.Remove:

                        _args.element = args.element;
                        _observer.OnNext(_args);

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