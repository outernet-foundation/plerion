using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ObserveThing
{
    public class OrderByCollectionObservableReactive<T, U> : IListObservable<T>
    {
        public ICollectionObservable<T> source;
        public Func<T, IValueObservable<U>> orderBy;

        public OrderByCollectionObservableReactive(ICollectionObservable<T> source, Func<T, IValueObservable<U>> orderBy)
        {
            this.source = source;
            this.orderBy = orderBy;
        }

        public IDisposable Subscribe(IObserver<IListEventArgs<T>> observer)
            => new Instance(source, orderBy, observer);

        private class Instance : IDisposable
        {
            private IDisposable _source;
            private Func<T, IValueObservable<U>> _orderBy;
            private IObserver<IListEventArgs<T>> _observer;
            private ListEventArgs<T> _args = new ListEventArgs<T>();
            private bool _disposed = false;

            private class OrderByData : IDisposable
            {
                public T element { get; private set; }
                public U orderedBy { get; private set; }
                public int count = 1;
                private IDisposable _observer;
                private Action<OrderByData> _requestResort;

                public OrderByData(T element, IValueObservable<U> orderByObservable, Action<OrderByData> requestResort)
                {
                    this.element = element;
                    _requestResort = requestResort;
                    _observer = orderByObservable.Subscribe(HandleOrderbyChanged);
                }

                private void HandleOrderbyChanged(IValueEventArgs<U> args)
                {
                    orderedBy = args.currentValue;
                    _requestResort(this);
                }

                public void Dispose()
                {
                    _observer.Dispose();
                }
            }

            private Dictionary<T, OrderByData> _dataByElement = new Dictionary<T, OrderByData>();
            private List<OrderByData> _elementsInOrder = new List<OrderByData>();

            public Instance(ICollectionObservable<T> source, Func<T, IValueObservable<U>> orderBy, IObserver<IListEventArgs<T>> observer)
            {
                _orderBy = orderBy;
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
                            if (!_dataByElement.TryGetValue(args.element, out var data))
                            {
                                data = new OrderByData(args.element, _orderBy(args.element), HandleResortRequested);
                                _dataByElement.Add(args.element, data);
                                return;
                            }

                            _args.element = args.element;
                            _args.index = GetSortedIndex(data, _elementsInOrder);
                            _args.operationType = OpType.Add;

                            data.count++; // be sure to do this after calling GetSortedIndex because the old value is used in that call
                            _elementsInOrder.Insert(_args.index, data);

                            _observer.OnNext(_args);
                        }

                        break;

                    case OpType.Remove:
                        {
                            var data = _dataByElement[args.element];
                            data.count--;

                            _args.element = args.element;
                            _args.index = _elementsInOrder.IndexOf(data);
                            _args.operationType = OpType.Remove;

                            _elementsInOrder.RemoveAt(_args.index);

                            if (data.count == 0)
                            {
                                _dataByElement.Remove(args.element);
                                data.Dispose();
                            }

                            _observer.OnNext(_args);
                        }

                        break;
                }
            }

            private int GetSortedIndex(OrderByData element, List<OrderByData> elementsInOrder)
            {
                int? sorted = default;

                for (int i = 0; i < _elementsInOrder.Count; i++)
                {
                    var compareTo = elementsInOrder[i];

                    if (element == compareTo)
                    {
                        i += element.count - 1;
                        continue;
                    }

                    if (Comparer<U>.Default.Compare(element.orderedBy, compareTo.orderedBy) <= 0)
                    {
                        sorted = i;
                        break;
                    }
                }

                return sorted ?? elementsInOrder.Count;
            }

            private void GetOriginalAndSortedIndex(OrderByData element, List<OrderByData> elementsInOrder, out int? originalIndex, out int sortedIndex)
            {
                int? original = default;
                int? sorted = default;

                for (int i = 0; i < _elementsInOrder.Count; i++)
                {
                    var compareTo = elementsInOrder[i];

                    if (original == null && element == compareTo)
                    {
                        original = i;

                        if (original != null && sorted != null)
                            break;

                        i += element.count - 1;
                        continue;
                    }

                    if (sorted == null && Comparer<U>.Default.Compare(element.orderedBy, compareTo.orderedBy) <= 0)
                    {
                        sorted = i;

                        if (original != null && sorted != null)
                            break;
                    }
                }

                originalIndex = original;
                sortedIndex = sorted ?? elementsInOrder.Count;
            }

            private void HandleResortRequested(OrderByData data)
            {
                GetOriginalAndSortedIndex(data, _elementsInOrder, out int? originalIndex, out int sortedIndex);

                _args.element = data.element;

                if (originalIndex.HasValue)
                {
                    if (sortedIndex == originalIndex.Value)
                        return;

                    if (sortedIndex > originalIndex.Value)
                        sortedIndex -= data.count;

                    _args.operationType = OpType.Remove;
                    _args.index = originalIndex.Value;

                    for (int i = 0; i < data.count; i++)
                    {
                        _elementsInOrder.RemoveAt(originalIndex.Value);
                        _observer.OnNext(_args);
                    }
                }

                _args.operationType = OpType.Add;
                _args.index = sortedIndex;

                for (int i = 0; i < data.count; i++)
                {
                    _elementsInOrder.Insert(sortedIndex, data);
                    _observer.OnNext(_args);
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

                foreach (var data in _dataByElement.Values)
                    data.Dispose();

                _source.Dispose();
                _observer.OnDispose();
            }
        }
    }
}