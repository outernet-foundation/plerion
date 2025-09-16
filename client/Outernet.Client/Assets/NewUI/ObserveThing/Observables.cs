using System;
using System.Collections.Generic;

namespace ObserveThing
{
    public interface IObserver<in T>
    {
        void OnNext(T args);
        void OnError(Exception exception);
        void OnDispose();
    }

    public sealed class Observer<T> : IObserver<T>
    {
        public Action<T> onNext;
        public Action<Exception> onError;
        public Action onDispose;

        public void OnNext(T args)
        {
            try
            {
                onNext?.Invoke(args);
            }
            catch (Exception exc)
            {
                OnError(exc);
            }
        }

        public void OnError(Exception exception)
            => onError?.Invoke(exception);

        public void OnDispose()
            => onDispose?.Invoke();

        public void Dispose()
            => OnDispose();
    }

    public interface IValueObservable<T>
    {
        IDisposable Subscribe(IObserver<IValueEventArgs<T>> observer);
    }

    public interface ICollectionObservable<T>
    {
        IDisposable Subscribe(IObserver<ICollectionEventArgs<T>> observer);
    }

    public interface IDictionaryObservable<TKey, TValue> : ICollectionObservable<KeyValuePair<TKey, TValue>>
    {
        IDisposable Subscribe(IObserver<IDictionaryEventArgs<TKey, TValue>> observer);

        IDisposable ICollectionObservable<KeyValuePair<TKey, TValue>>.Subscribe(IObserver<ICollectionEventArgs<KeyValuePair<TKey, TValue>>> observer)
            => Subscribe(observer);
    }

    public interface IListObservable<T> : ICollectionObservable<T>
    {
        IDisposable Subscribe(IObserver<IListEventArgs<T>> observer);

        IDisposable ICollectionObservable<T>.Subscribe(IObserver<ICollectionEventArgs<T>> observer)
            => Subscribe(observer);
    }

    public interface IValueEventArgs<T>
    {
        T currentValue { get; }
        T previousValue { get; }
    }

    public enum OpType
    {
        Add,
        Remove
    }

    public interface ICollectionEventArgs<T>
    {
        T element { get; }
        OpType operationType { get; }
    }

    public interface IListEventArgs<T> : ICollectionEventArgs<T>
    {
        int index { get; }
    }

    public interface IDictionaryEventArgs<TKey, TValue> : ICollectionEventArgs<KeyValuePair<TKey, TValue>>
    {
        TKey key { get; }
        TValue value { get; }
    }

    public class ValueEventArgs<T> : IValueEventArgs<T>
    {
        public T currentValue { get; set; }
        public T previousValue { get; set; }
    }

    public class CollectionEventArgs<T> : ICollectionEventArgs<T>
    {
        public T element { get; set; }
        public OpType operationType { get; set; }
    }

    public class ListEventArgs<T> : IListEventArgs<T>
    {
        public int index { get; set; }
        public T element { get; set; }
        public OpType operationType { get; set; }
    }

    public class DictionaryEventArgs<TKey, TValue> : IDictionaryEventArgs<TKey, TValue>
    {
        public TKey key => element.Key;
        public TValue value => element.Value;
        public KeyValuePair<TKey, TValue> element { get; set; }
        public OpType operationType { set; get; }
    }

    public static class ObservableExtensions
    {
        public static IValueObservable<U> Select<T, U>(this IValueObservable<T> source, Func<T, U> select)
            => new SelectValueObservable<T, U>(source, select);

        public static IValueObservable<U> Select<T, U>(this IValueObservable<T> source, Func<T, IValueObservable<U>> select)
            => new SelectValueObservableReactive<T, U>(source, select);

        public static IValueObservable<U> Create<T, U>(this IValueObservable<T> source, Func<T, U> select)
            where U : IDisposable => new CreateValueObservable<T, U>(source, select);

        public static IValueObservable<U> Create<T, U>(this IValueObservable<T> source, Func<T, IValueObservable<U>> select)
            where U : IDisposable => new CreateValueObservableReactive<T, U>(source, select);

        public static ICollectionObservable<U> Select<T, U>(this ICollectionObservable<T> source, Func<T, U> select)
            => new SelectCollectionObservable<T, U>(source, select);

        public static ICollectionObservable<U> Select<T, U>(this ICollectionObservable<T> source, Func<T, IValueObservable<U>> select)
            => new SelectCollectionObservableReactive<T, U>(source, select);

        public static ICollectionObservable<U> Create<T, U>(this ICollectionObservable<T> source, Func<T, U> select)
            where U : IDisposable => new CreateCollectionObservable<T, U>(source, select);

        public static ICollectionObservable<U> Create<T, U>(this ICollectionObservable<T> source, Func<T, IValueObservable<U>> select)
            where U : IDisposable => new CreateCollectionObservableReactive<T, U>(source, select);

        public static ICollectionObservable<T> Where<T>(this ICollectionObservable<T> source, Func<T, bool> where)
            => new WhereCollectionObservable<T>(source, where);

        public static ICollectionObservable<T> Where<T>(this ICollectionObservable<T> source, Func<T, IValueObservable<bool>> where)
            => new WhereCollectionObservableReactive<T>(source, where);

        public static ICollectionObservable<T> Concat<T>(this ICollectionObservable<T> source1, IEnumerable<T> source2)
            => new ConcatCollectionObservable<T>(source1, source2);

        public static ICollectionObservable<T> Concat<T>(this ICollectionObservable<T> source1, ICollectionObservable<T> source2)
            => new ConcatCollectionObservableReactive<T>(source1, source2);

        public static ICollectionObservable<U> SelectMany<T, U>(this ICollectionObservable<T> source, Func<T, IEnumerable<U>> selectMany)
            => new SelectManyCollectionObservable<T, U>(source, selectMany);

        public static ICollectionObservable<U> SelectMany<T, U>(this ICollectionObservable<T> source, Func<T, ICollectionObservable<U>> selectMany)
            => new SelectManyCollectionObservableReactive<T, U>(source, selectMany);

        public static ICollectionObservable<T> Distinct<T>(this ICollectionObservable<T> source)
            => new DistinctCollectionObservable<T>(source);

        public static IValueObservable<int> Count<T>(this ICollectionObservable<T> source)
            => new CountCollectionObservable<T>(source);

        public static IValueObservable<(T1 value1, T2 value2)> With<T1, T2>(this IValueObservable<T1> source1, IValueObservable<T2> source2)
            => new WithValueObservable<T1, T2>(source1, source2);

        public static IValueObservable<(bool keyPresent, TValue value)> Track<TKey, TValue>(this IDictionaryObservable<TKey, TValue> source, TKey key)
            => new TrackValueObservable<TKey, TValue>(source, key);

        public static IValueObservable<(bool keyPresent, TValue value)> Track<TKey, TValue>(this IDictionaryObservable<TKey, TValue> source, IValueObservable<TKey> key)
            => new TrackValueObservableReactive<TKey, TValue>(source, key);

        public static ICollectionObservable<TValue> Track<TKey, TValue>(this IDictionaryObservable<TKey, TValue> source, ICollectionObservable<TKey> keys)
            => keys.Select(x => source.Track(x)).Where(x => x.keyPresent).Select(x => x.value);

        public static IListObservable<T> OrderBy<T, U>(this ICollectionObservable<T> source, Func<T, U> orderBy)
            => new OrderByCollectionObservable<T, U>(source, orderBy);

        public static IListObservable<T> OrderBy<T, U>(this ICollectionObservable<T> source, Func<T, IValueObservable<U>> orderBy)
            => new OrderByCollectionObservableReactive<T, U>(source, orderBy);

        public static IValueObservable<bool> Contains<T>(this ICollectionObservable<T> source, T contains)
            => new ContainsCollectionObservable<T>(source, contains);

        public static IValueObservable<bool> Contains<T>(this ICollectionObservable<T> source, IValueObservable<T> contains)
            => new ContainsCollectionObservableReactive<T>(source, contains);

        public static IDisposable Subscribe<T>(this IValueObservable<T> source, Action<IValueEventArgs<T>> observer = default, Action<Exception> onError = default, Action onDipose = default)
            => source.Subscribe(new Observer<IValueEventArgs<T>>() { onNext = observer, onError = onError, onDispose = onDipose });

        public static IDisposable Subscribe<T>(this ICollectionObservable<T> source, Action<ICollectionEventArgs<T>> observer = default, Action<Exception> onError = default, Action onDipose = default)
            => source.Subscribe(new Observer<ICollectionEventArgs<T>>() { onNext = observer, onError = onError, onDispose = onDipose });

        public static IDisposable Subscribe<T>(this IListObservable<T> source, Action<IListEventArgs<T>> observer = default, Action<Exception> onError = default, Action onDipose = default)
            => source.Subscribe(new Observer<IListEventArgs<T>>() { onNext = observer, onError = onError, onDispose = onDipose });

        public static IDisposable Subscribe<TKey, TValue>(this IDictionaryObservable<TKey, TValue> source, Action<IDictionaryEventArgs<TKey, TValue>> observer = default, Action<Exception> onError = default, Action onDipose = default)
            => source.Subscribe(new Observer<IDictionaryEventArgs<TKey, TValue>>() { onNext = observer, onError = onError, onDispose = onDipose });

        public static IValueObservable<T> AsObservable<T>(this IValueObservable<T> observable)
            => observable;

        public static ICollectionObservable<T> AsObservable<T>(this ICollectionObservable<T> observable)
            => observable;

        public static IListObservable<T> AsObservable<T>(this IListObservable<T> observable)
            => observable;

        public static IDictionaryObservable<TKey, TValue> AsObservable<TKey, TValue>(this IDictionaryObservable<TKey, TValue> observable)
            => observable;
    }
}