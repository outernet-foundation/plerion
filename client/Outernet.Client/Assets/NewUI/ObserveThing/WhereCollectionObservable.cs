using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class WhereCollectionObservable<T> : ICollectionObservable<T>
    {
        public ICollectionObservable<T> source;
        public Func<T, bool> select;

        public WhereCollectionObservable(ICollectionObservable<T> source, Func<T, bool> select)
        {
            this.source = source;
            this.select = select;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer)
            => new Instance(source, select, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private Func<T, bool> _select;
            private IObserver<ICollectionEventArgs<T>> _observer;
            private CollectionEventArgs<T> _args = new CollectionEventArgs<T>();
            private bool _disposed = false;

            private List<T> _elements = new List<T>();

            public Instance(ICollectionObservable<T> source, Func<T, bool> select, IObserver<ICollectionEventArgs<T>> observer)
            {
                _select = select;
                _observer = observer;
                _source = source.Subscribe(
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

                        if (!_select(args.element))
                            return;

                        _elements.Add(args.element);
                        _args.element = args.element;
                        _observer.OnNext(_args);

                        break;

                    case OpType.Remove:

                        if (!_elements.Remove(args.element))
                            return;

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