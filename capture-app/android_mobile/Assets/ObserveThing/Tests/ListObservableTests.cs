using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObserveThing.Tests
{
    public class ManualListObservable<T> : ICollectionObservable<T>
    {
        private List<T> _mostRecentList = new List<T>();
        private ListEventArgs<T> _args = new ListEventArgs<T>();
        private List<Instance> _instances = new List<Instance>();
        private bool _disposing;

        public void OnAdd(T added)
            => OnInsert(_mostRecentList.Count, added);

        public void OnRemove(T removed)
            => OnRemoveAt(_mostRecentList.IndexOf(removed));

        public void OnRemoveAt(int index)
        {
            T element = _mostRecentList[index];
            _mostRecentList.RemoveAt(index);
            _args.index = index;
            _args.element = element;
            _args.operationType = OpType.Remove;
            foreach (var instance in _instances)
                instance.OnNext(_args);
        }

        public void OnInsert(int index, T inserted)
        {
            _mostRecentList.Insert(index, inserted);
            _args.index = index;
            _args.element = inserted;
            _args.operationType = OpType.Add;
            foreach (var instance in _instances)
                instance.OnNext(_args);
        }

        public void OnError(Exception exception)
        {
            foreach (var instance in _instances)
                instance.OnError(exception);
        }

        public void DisposeAll()
        {
            _disposing = true;

            foreach (var instance in _instances)
                instance.Dispose();

            _instances.Clear();

            _disposing = false;
        }

        public IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer)
        {
            var instance = new Instance(observer, x =>
            {
                if (!_disposing)
                    _instances.Remove(x);
            });

            _instances.Add(instance);

            for (int i = 0; i < _mostRecentList.Count; i++)
            {
                _args.index = i;
                _args.element = _mostRecentList[i];
                _args.operationType = OpType.Add;
                instance.OnNext(_args);
            }

            return instance;
        }

        private class Instance : IDisposable
        {
            private IObserver<IListEventArgs<T>> _observer;
            private Action<Instance> _onDispose;

            public Instance(IObserver<IListEventArgs<T>> observer, Action<Instance> onDispose)
            {
                _observer = observer;
                _onDispose = onDispose;
            }

            public void OnNext(IListEventArgs<T> args)
            {
                _observer?.OnNext(args);
            }

            public void OnError(Exception error)
            {
                _observer?.OnError(error);
            }

            public void Dispose()
            {
                if (_observer == null)
                    throw new Exception("ALREADY DISPOSED");

                _observer.OnDispose();
                _observer = null;

                _onDispose(this);
            }
        }
    }

    public class ListObservableTests
    {

    }
}