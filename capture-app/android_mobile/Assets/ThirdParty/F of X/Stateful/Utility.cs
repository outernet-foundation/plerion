using System;
using System.Collections.Generic;
using System.Linq;

namespace FofX.Stateful
{
    public static class Utility
    {
        public static void ExecuteAction<T, TData>(this T node, TData data, Action<TData, T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
                    where T : IObservableNode => node.ExecuteAction(x => action(data, x), logLevel, postObserverCallback);

        public static void ExecuteAction<T>(this T node, Action<T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where T : IObservableNode => node.context.ExecuteAction(node, logLevel, postObserverCallback, new DirectAction<T>(action));

        public static void ExecuteActionOrDelay<T, TData>(this T node, TData data, Action<TData, T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where T : IObservableNode => node.ExecuteActionOrDelay(x => action(data, x), logLevel, postObserverCallback);

        public static void ExecuteActionOrDelay<T>(this T node, Action<T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where T : IObservableNode => node.context.ExecuteActionOrDelay(node, logLevel, postObserverCallback, new DirectAction<T>(action));

        public static void ScheduleAction<T, TData>(this T node, TData data, Action<TData, T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where T : IObservableNode => node.ScheduleAction(x => action(data, x), logLevel, postObserverCallback);

        public static void ScheduleAction<T>(this T node, Action<T> action, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where T : IObservableNode => node.context.ScheduleAction(node, logLevel, postObserverCallback, new DirectAction<T>(action));

        public class DirectAction<T> : ObservableNodeAction<T> where T : IObservableNode
        {
            private Action<T> _action;

            public DirectAction(Action<T> action)
            {
                _action = action;
            }

            public override void Execute(T target)
            {
                _action(target);
            }
        }

        public static void ExecuteAction<T>(this T node, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteAction(node, default, default, actions);

        public static void ExecuteAction<T>(this T node, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteAction(node, default, postObserverCallback, actions);

        public static void ExecuteAction<T>(this T node, LogLevel logLevel, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteAction(node, logLevel, default, actions);

        public static void ExecuteAction<T>(this T node, LogLevel logLevel, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteAction(node, logLevel, postObserverCallback, actions);

        public static void ExecuteActionOrDelay<T>(this T node, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteActionOrDelay(node, default, default, actions);

        public static void ExecuteActionOrDelay<T>(this T node, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteActionOrDelay(node, default, postObserverCallback, actions);

        public static void ExecuteActionOrDelay<T>(this T node, LogLevel logLevel, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteActionOrDelay(node, logLevel, default, actions);

        public static void ExecuteActionOrDelay<T>(this T node, LogLevel logLevel, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ExecuteActionOrDelay(node, logLevel, postObserverCallback, actions);

        public static void ScheduleAction<T>(this T node, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ScheduleAction(node, default, default, actions);

        public static void ScheduleAction<T>(this T node, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ScheduleAction(node, default, postObserverCallback, actions);

        public static void ScheduleAction<T>(this T node, LogLevel logLevel, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ScheduleAction(node, logLevel, default, actions);

        public static void ScheduleAction<T>(this T node, LogLevel logLevel, Action postObserverCallback, params ObservableNodeAction<T>[] actions)
            where T : IObservableNode => node.context.ScheduleAction(node, logLevel, postObserverCallback, actions);

        public static void ExecuteSet<T>(this ObservablePrimitive<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteAction(logLevel, postObserverCallback, new SetPrimitiveValueAction<T>(value));

        public static void ExecuteSetOrDelay<T>(this ObservablePrimitive<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteActionOrDelay(logLevel, postObserverCallback, new SetPrimitiveValueAction<T>(value));

        public static void ScheduleSet<T>(this ObservablePrimitive<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ScheduleAction(logLevel, postObserverCallback, new SetPrimitiveValueAction<T>(value));

        public static void ExecuteAdd<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteAction(logLevel, postObserverCallback, new AddValueToSetAction<T>(value));

        public static void ExecuteAddOrDelay<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteActionOrDelay(logLevel, postObserverCallback, new AddValueToSetAction<T>(value));

        public static void ScheduleAdd<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ScheduleAction(logLevel, postObserverCallback, new AddValueToSetAction<T>(value));

        public static void ExecuteRemove<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteAction(logLevel, postObserverCallback, new RemoveValueFromSetAction<T>(value));

        public static void ExecuteRemoveOrDelay<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteActionOrDelay(logLevel, postObserverCallback, new RemoveValueFromSetAction<T>(value));

        public static void ScheduleRemove<T>(this ObservableSet<T> primitive, T value, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ScheduleAction(logLevel, postObserverCallback, new RemoveValueFromSetAction<T>(value));

        public static void ExecuteSet<T>(this ObservableSet<T> primitive, T[] values, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteAction(logLevel, postObserverCallback, new SetValuesInSetAction<T>(values));

        public static void ExecuteSetOrDelay<T>(this ObservableSet<T> primitive, T[] values, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ExecuteActionOrDelay(logLevel, postObserverCallback, new SetValuesInSetAction<T>(values));

        public static void ScheduleSet<T>(this ObservableSet<T> primitive, T[] values, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            => primitive.ScheduleAction(logLevel, postObserverCallback, new SetValuesInSetAction<T>(values));

        public static void ExecuteAdd<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ExecuteAction(logLevel, postObserverCallback, new AddKeyToDictionaryAction<TKey, TValue>(key));

        public static void ExecuteAddOrDelay<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ExecuteActionOrDelay(logLevel, postObserverCallback, new AddKeyToDictionaryAction<TKey, TValue>(key));

        public static void ScheduleAdd<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ScheduleAction(logLevel, postObserverCallback, new AddKeyToDictionaryAction<TKey, TValue>(key));

        public static void ExecuteRemove<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ExecuteAction(logLevel, postObserverCallback, new RemoveKeyFromDictionaryAction<TKey, TValue>(key));

        public static void ExecuteRemoveOrDelay<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ExecuteActionOrDelay(logLevel, postObserverCallback, new RemoveKeyFromDictionaryAction<TKey, TValue>(key));

        public static void ScheduleRemove<TKey, TValue>(this ObservableDictionary<TKey, TValue> dictionary, TKey key, LogLevel logLevel = LogLevel.Debug, Action postObserverCallback = default)
            where TValue : IObservableNode, new() => dictionary.ScheduleAction(logLevel, postObserverCallback, new RemoveKeyFromDictionaryAction<TKey, TValue>(key));

        public static void SetFrom<T>(this ObservableSet<T> set, IEnumerable<T> from)
        {
            if (from == null)
            {
                set.Clear();
                return;
            }

            var missing = set.Except(from).ToArray();
            var added = from.Except(set).ToArray();

            foreach (var toRemove in missing)
                set.Remove(toRemove);

            foreach (var toAdd in added)
                set.Add(toAdd);
        }

        public static void SetFrom<TKey, TValue>(this ObservableDictionary<TKey, TValue> dict, TKey[] keys, Action<KeyValuePair<TKey, TValue>> onAdd = default)
            where TValue : IObservableNode, new()
        {
            if (keys == null)
            {
                dict.Clear();
                return;
            }

            var added = keys.Except(dict.keys).ToArray();
            var removed = dict.keys.Except(keys).ToArray();

            foreach (var keyToAdd in added)
            {
                var newValue = dict.Add(keyToAdd);
                onAdd?.Invoke(new KeyValuePair<TKey, TValue>(keyToAdd, newValue));
            }

            foreach (var keyToRemove in removed)
                dict.Remove(keyToRemove);
        }

        public static void SetFrom<TKey, TState, TSource>(this ObservableDictionary<TKey, TState> dict, Dictionary<TKey, TSource> source, bool refreshOldEntries = false, Action<TKey, TSource, TState> copy = default)
            where TState : IObservableNode, new()
        {
            if (source == null)
            {
                dict.Clear();
                return;
            }

            var removed = dict.keys.Except(source.Keys).ToArray();

            if (refreshOldEntries)
            {
                foreach (var kvp in source)
                    copy?.Invoke(kvp.Key, kvp.Value, dict.GetOrAdd(kvp.Key));
            }
            else
            {
                foreach (var kvp in source)
                {
                    if (dict.TryGetValue(kvp.Key, out var dictValue))
                        continue;

                    copy?.Invoke(kvp.Key, kvp.Value, dict.Add(kvp.Key));
                }
            }

            foreach (var toRemove in removed)
                dict.Remove(toRemove);
        }

        public class SetPrimitiveValueAction<T> : ObservableNodeAction<ObservablePrimitive<T>>
        {
            private T _value;

            public SetPrimitiveValueAction(T value)
            {
                _value = value;
            }

            public override void Execute(ObservablePrimitive<T> target)
            {
                target.value = _value;
            }
        }

        public class AddValueToSetAction<T> : ObservableNodeAction<ObservableSet<T>>
        {
            private T _value;

            public AddValueToSetAction(T value)
            {
                _value = value;
            }

            public override void Execute(ObservableSet<T> target)
            {
                target.Add(_value);
            }
        }

        public class RemoveValueFromSetAction<T> : ObservableNodeAction<ObservableSet<T>>
        {
            private T _value;

            public RemoveValueFromSetAction(T value)
            {
                _value = value;
            }

            public override void Execute(ObservableSet<T> target)
            {
                target.Remove(_value);
            }
        }

        public class SetValuesInSetAction<T> : ObservableNodeAction<ObservableSet<T>>
        {
            private T[] _values;

            public SetValuesInSetAction(T[] values)
            {
                _values = values;
            }

            public override void Execute(ObservableSet<T> target)
            {
                target.SetFrom(_values);
            }
        }

        public class AddKeyToDictionaryAction<TKey, TValue> : ObservableNodeAction<ObservableDictionary<TKey, TValue>>
            where TValue : IObservableNode, new()
        {
            private TKey _key;

            public AddKeyToDictionaryAction(TKey key)
            {
                _key = key;
            }

            public override void Execute(ObservableDictionary<TKey, TValue> target)
            {
                target.Add(_key);
            }
        }

        public class RemoveKeyFromDictionaryAction<TKey, TValue> : ObservableNodeAction<ObservableDictionary<TKey, TValue>>
            where TValue : IObservableNode, new()
        {
            private TKey _key;

            public RemoveKeyFromDictionaryAction(TKey key)
            {
                _key = key;
            }

            public override void Execute(ObservableDictionary<TKey, TValue> target)
            {
                target.Remove(_key);
            }
        }
    }
}
