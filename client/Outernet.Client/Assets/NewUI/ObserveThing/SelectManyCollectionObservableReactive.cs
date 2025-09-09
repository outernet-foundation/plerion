using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public class SelectManyCollectionObservableReactive<T, U> : ICollectionObservable<U>
    {
        public ICollectionObservable<T> source;
        public Func<T, ICollectionObservable<U>> selectMany;

        public SelectManyCollectionObservableReactive(ICollectionObservable<T> source, Func<T, ICollectionObservable<U>> selectMany)
        {
            this.source = source;
            this.selectMany = selectMany;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<U>> observer)
            => new Instance(source, selectMany, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private Func<T, ICollectionObservable<U>> _selectMany;
            private IObserver<ICollectionEventArgs<U>> _observer;
            private CollectionEventArgs<U> _args = new CollectionEventArgs<U>();
            private bool _disposed = false;

            private Dictionary<T, SelectManyData> _selectData = new Dictionary<T, SelectManyData>();
            private class SelectManyData
            {
                public T value;
                public int count = 1;
                public List<U> addedElements = new List<U>();
                public IDisposable selectMany;
            }

            public Instance(ICollectionObservable<T> source, Func<T, ICollectionObservable<U>> selectMany, IObserver<ICollectionEventArgs<U>> observer)
            {
                _observer = observer;
                _selectMany = selectMany;
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

                        if (!_selectData.TryGetValue(args.element, out var added))
                        {
                            added = new SelectManyData();
                            _selectData.Add(args.element, added);
                            added.value = args.element;
                            added.selectMany = _selectMany(args.element).Subscribe(x => HandleSelectManyUpdated(added, x));
                            return;
                        }

                        added.count++;

                        foreach (var element in added.addedElements)
                        {
                            _args.element = element;
                            _observer.OnNext(_args);
                        }

                        break;

                    case OpType.Remove:

                        var removed = _selectData[args.element];

                        foreach (var element in removed.addedElements)
                        {
                            _args.element = element;
                            _observer.OnNext(_args);
                        }

                        removed.count--;

                        if (removed.count == 0)
                        {
                            removed.selectMany.Dispose();
                            _selectData.Remove(args.element);
                        }

                        break;
                }
            }

            private void HandleSelectManyUpdated(SelectManyData selectManyData, ICollectionEventArgs<U> args)
            {
                switch (args.operationType)
                {
                    case OpType.Add:

                        selectManyData.addedElements.Add(args.element);
                        _args.operationType = OpType.Add;
                        _args.element = args.element;

                        for (int i = 0; i < selectManyData.count; i++)
                            _observer.OnNext(_args);

                        break;

                    case OpType.Remove:

                        selectManyData.addedElements.Remove(args.element);
                        _args.operationType = OpType.Remove;
                        _args.element = args.element;

                        for (int i = 0; i < selectManyData.count; i++)
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

                foreach (var data in _selectData.Values)
                    data.selectMany.Dispose();

                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}