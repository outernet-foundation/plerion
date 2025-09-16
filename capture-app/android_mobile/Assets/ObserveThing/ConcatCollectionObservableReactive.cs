using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class ConcatCollectionObservableReactive<T> : ICollectionObservable<T>
    {
        public ICollectionObservable<T> source1;
        public ICollectionObservable<T> source2;

        public ConcatCollectionObservableReactive(ICollectionObservable<T> source1, ICollectionObservable<T> source2)
        {
            this.source1 = source1;
            this.source2 = source2;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer)
            => new Instance(source1, source2, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source1;
            private IDisposable _source2;
            private IObserver<ICollectionEventArgs<T>> _observer;
            private CollectionEventArgs<T> _args = new CollectionEventArgs<T>();
            private bool _disposed = false;

            public Instance(ICollectionObservable<T> source1, ICollectionObservable<T> source2, IObserver<ICollectionEventArgs<T>> observer)
            {
                _observer = observer;

                _source1 = source1.Subscribe(
                    HandleSourceChanged,
                    HandleSourceError,
                    HandleSourceDisposed
                );

                _source2 = source2.Subscribe(
                    HandleSourceChanged,
                    HandleSourceError,
                    HandleSourceDisposed
                );
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

                _source1.Dispose();
                _source2.Dispose();
                _observer.OnDispose();
            }
        }
    }
}