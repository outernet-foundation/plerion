using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class CreateCollectionObservable<T, U> : ICollectionObservable<U> where U : IDisposable
    {
        public ICollectionObservable<T> source;
        public Func<T, U> select;

        public CreateCollectionObservable(ICollectionObservable<T> source, Func<T, U> select)
        {
            this.source = source;
            this.select = select;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<U>> observer)
            => new Instance(source, select, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private Func<T, U> _select;
            private IObserver<ICollectionEventArgs<U>> _observer;
            private CollectionEventArgs<U> _args = new CollectionEventArgs<U>();
            private bool _disposed = false;

            private Dictionary<T, SelectData> _selectedData = new Dictionary<T, SelectData>();

            private class SelectData
            {
                public T element;
                public U selected;
                public int count;
            }

            public Instance(ICollectionObservable<T> source, Func<T, U> select, IObserver<ICollectionEventArgs<U>> observer)
            {
                _select = select;
                _observer = observer;
                _source = source.Subscribe(HandleSourceChanged, HandleSourceError, HandleSourceDisposed);
            }

            private void HandleSourceChanged(ICollectionEventArgs<T> args)
            {
                _args.operationType = args.operationType;

                switch (args.operationType)
                {
                    case OpType.Add:

                        if (!_selectedData.TryGetValue(args.element, out var added))
                        {
                            added = new SelectData() { element = args.element, selected = _select(args.element) };
                            _selectedData.Add(args.element, added);
                        }

                        added.count++;

                        _args.element = added.selected;
                        _observer.OnNext(_args);

                        break;

                    case OpType.Remove:

                        var removed = _selectedData[args.element];
                        removed.count--;

                        if (removed.count == 0)
                            _selectedData.Remove(args.element);

                        _args.element = removed.selected;
                        _observer.OnNext(_args);

                        removed.selected.Dispose();

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

                foreach (var element in _selectedData.Values)
                    element.selected.Dispose();

                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}