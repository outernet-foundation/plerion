using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using FofX.Serialization;
using ObserveThing;

namespace FofX.Stateful
{
    public interface IObservableDictionary : IObservableNode, IEnumerable
    {
        Type keyType { get; }
        Type valueType { get; }
        IEnumerable<object> keysAsObjects { get; }
        IObservableNode this[object key] { get; }
        bool ContainsKey(object key);
        bool ContainsValue(IObservableNode value);
        bool TryGetValue(object key, out IObservableNode value);
        bool TryGetKey(IObservableNode value, out object key);
        object GetKey(IObservableNode value);
        IObservableNode Add(object key);
        bool Remove(object key);
        void Clear();
    }

    public interface IObservableDictionary<T> : IObservableDictionary
    {
        IEnumerable<T> keys { get; }
        IObservableNode this[T key] { get; }
        bool ContainsKey(T key);
        bool TryGetValue(T key, out IObservableNode value);
        IObservableNode Add(T key);
        bool Remove(T key);
    }

    public sealed class ObservableDictionary<TKey, TValue> :
        ObservableNode,
        IObservableDictionary<TKey>,
        IDictionaryObservable<TKey, TValue>,
        IEnumerable<KeyValuePair<TKey, TValue>>
        where TValue : IObservableNode, new()
    {
        public TValue this[TKey key] => _dictionary[key];
        public int count => _dictionary.Count;
        public override bool isDefault => _dictionary.Count == 0;
        public IEnumerable<TKey> keys => _dictionary.Keys;
        public IEnumerable<TValue> values => _dictionary.Values;

        public Type keyType => typeof(TKey);
        public Type valueType => typeof(TValue);
        IEnumerable<object> IObservableDictionary.keysAsObjects => keys.Cast<object>();

        IObservableNode IObservableDictionary<TKey>.this[TKey key] => throw new NotImplementedException();

        IObservableNode IObservableDictionary.this[object key] => this[(TKey)key];

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

        private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private Dictionary<TValue, TKey> _keyByValue = new Dictionary<TValue, TKey>();

        public ObservableDictionary() : base() { }

        public ObservableDictionary(params KeyValuePair<TKey, TValue>[] values)
            : this((IEnumerable<KeyValuePair<TKey, TValue>>)values) { }

        public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> values) : base()
        {
            if (values == null)
                return;

            foreach (var element in values)
            {
                _dictionary.Add(element.Key, element.Value);
                _keyByValue.Add(element.Value, element.Key);

                if (element.Value is IKeyedObservableNode<TKey> keyedValue)
                    keyedValue.AssignKey(element.Key);
            }
        }

        protected override void InitializeInternal()
        {
            base.InitializeInternal();

            foreach (var element in _dictionary)
                AddChildInternal(element.Key.ToString(), element.Value);
        }

        public TValue Add(TKey key)
        {
            TValue value = new TValue();

            _dictionary.Add(key, value);
            _keyByValue.Add(value, key);

            if (value is IKeyedObservableNode<TKey> keyedValue)
                keyedValue.AssignKey(key);

            AddChildInternal(key.ToString(), value);
            context.RegisterNodeChange(
                this,
                ChangeType.Add,
                key: key,
                child: value,
                collectionElement: new KeyValuePair<TKey, TValue>(key, value)
            );

            return value;
        }

        public bool Remove(TKey key)
        {
            if (!_dictionary.TryGetValue(key, out TValue value))
                return false;

            _dictionary.Remove(key);
            _keyByValue.Remove(value);

            RemoveChildInternal(value);
            context.RegisterNodeChange(
                this,
                ChangeType.Remove,
                key: key,
                child: value,
                collectionElement: new KeyValuePair<TKey, TValue>(key, value)
            );

            return true;
        }

        public void Clear()
        {
            foreach (TKey key in _dictionary.Keys.ToArray())
                Remove(key);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public bool ContainsValue(TValue value)
        {
            return _dictionary.ContainsValue(value);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public TKey GetKey(TValue value)
        {
            return _keyByValue[value];
        }

        public bool TryGetKey(TValue value, out TKey key)
        {
            return _keyByValue.TryGetValue(value, out key);
        }

        public override void ApplyChange(NodeChangeData change)
        {
            if (change.changeType == ChangeType.Dispose)
                return;

            if (change.changeType == ChangeType.Add)
            {
                Add((TKey)change.key);
            }
            else if (change.changeType == ChangeType.Remove)
            {
                Remove((TKey)change.key);
            }
            else
            {
                throw new Exception($"{nameof(ObservableDictionary<TKey, TValue>)} does not support change type {change.changeType}");
            }
        }

        public override void CopyTo(IObservableNode copyTo)
        {
            if (copyTo is not ObservableDictionary<TKey, TValue> copyToDict)
                throw new Exception($"{copyTo.nodePath} is not a {nameof(ObservableDictionary<TKey, TValue>)}");

            var toRemove = copyToDict.keys.Except(keys).ToArray();

            foreach (var keyToRemove in toRemove)
                Remove(keyToRemove);

            foreach (var kvpToCopy in _dictionary)
                kvpToCopy.Value.CopyTo(copyToDict.GetOrAdd(kvpToCopy.Key));
        }

        public override void Reset()
        {
            Clear();
        }

        public TValue GetOrAdd(TKey key)
        {
            if (TryGetValue(key, out TValue value))
                return value;

            return Add(key);
        }

        bool IObservableDictionary.ContainsKey(object key)
        {
            return ContainsKey((TKey)key);
        }

        bool IObservableDictionary.ContainsValue(IObservableNode value)
        {
            return ContainsValue((TValue)value);
        }

        bool IObservableDictionary.TryGetValue(object key, out IObservableNode value)
        {
            if (_dictionary.TryGetValue((TKey)key, out TValue v))
            {
                value = v;
                return true;
            }

            value = null;
            return false;
        }

        IObservableNode IObservableDictionary.Add(object key)
        {
            return Add((TKey)key);
        }

        bool IObservableDictionary.Remove(object key)
        {
            return Remove((TKey)key);
        }

        public override JSONNode ToJSON(Func<IObservableNode, bool> filter)
        {
            JSONObject dict = new JSONObject();
            SerializationPair<TKey> serializer = JSONSerialization.GetSerializer<TKey>();

            foreach (var kvp in _dictionary.Where(x => filter(x.Value)))
                dict.Add(serializer.toJSON(kvp.Key), kvp.Value.ToJSON(filter));

            return dict;
        }

        public override void FromJSON(JSONNode json)
        {
            if (json == null)
            {
                Reset();
                return;
            }

            JSONObject dict = (JSONObject)json;
            SerializationPair<TKey> serializer = JSONSerialization.GetSerializer<TKey>();

            Clear();

            foreach (var value in dict)
                Add(serializer.fromJSON(value.Key)).FromJSON(value.Value);
        }

        bool IObservableDictionary<TKey>.TryGetValue(TKey key, out IObservableNode value)
        {
            if (TryGetValue(key, out var result))
            {
                value = result;
                return true;
            }

            value = default;
            return false;
        }

        bool IObservableDictionary.TryGetKey(IObservableNode value, out object key)
        {
            if (TryGetKey((TValue)value, out var result))
            {
                key = result;
                return true;
            }

            key = default;
            return false;
        }

        IObservableNode IObservableDictionary<TKey>.Add(TKey key)
        {
            return Add(key);
        }

        object IObservableDictionary.GetKey(IObservableNode value)
            => GetKey((TValue)value);

        IDisposable IDictionaryObservable<TKey, TValue>.Subscribe(ObserveThing.IObserver<IDictionaryEventArgs<TKey, TValue>> observer)
            => new Instance(this, observer);

        private class Instance : IDisposable
        {
            private ObservableDictionary<TKey, TValue> _dictionary;
            private ObserveThing.IObserver<IDictionaryEventArgs<TKey, TValue>> _observer;
            private DictionaryEventArgs<TKey, TValue> _args = new DictionaryEventArgs<TKey, TValue>();

            public Instance(ObservableDictionary<TKey, TValue> dictionary, ObserveThing.IObserver<IDictionaryEventArgs<TKey, TValue>> observer)
            {
                _dictionary = dictionary;
                _observer = observer;

                _dictionary.context.RegisterObserver(HandleDictionaryChanged, new ObserverParameters() { scope = ObservationScope.Self }, _dictionary);
            }

            private void HandleDictionaryChanged(NodeChangeEventArgs args)
            {
                if (args.initialize)
                {
                    _args.operationType = OpType.Add;
                    foreach (var kvp in _dictionary)
                    {
                        _args.element = new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value);
                        _observer.OnNext(_args);
                    }

                    return;
                }

                foreach (var change in args.changes)
                {
                    if (change.changeType == ChangeType.Dispose)
                    {
                        _dictionary.context.DeregisterObserver(HandleDictionaryChanged);
                        _observer.OnDispose();
                        break;
                    }

                    _args.operationType = change.changeType == ChangeType.Add ?
                        OpType.Add : OpType.Remove;

                    _args.element = new KeyValuePair<TKey, TValue>((TKey)change.key, (TValue)change.child);

                    _observer.OnNext(_args);
                }
            }

            public void Dispose()
            {
                _dictionary.context.DeregisterObserver(HandleDictionaryChanged);
                _observer.OnDispose();
            }
        }
    }
}
