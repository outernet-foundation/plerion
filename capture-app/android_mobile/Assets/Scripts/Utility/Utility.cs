using Nessle;
using ObserveThing;
using UnityEngine;

using static Nessle.UIBuilder;

using ImageType = UnityEngine.UI.Image.Type;
using ImageFillMethod = UnityEngine.UI.Image.FillMethod;
using System;
using System.Collections.Generic;

namespace PlerionClient.Client
{
    public static class Utility
    {
        public static T SingleChild<T>(this T control, IValueObservable<IControl> child)
            where T : IControl
        {
            control.AddBinding(child.Subscribe(x =>
            {
                x.previousValue?.Dispose();
                x.currentValue?.parent.From(control);
            }));

            return control;
        }

        public static IDisposable SubscribeEach<T>(this ICollectionObservable<T> collection, Func<T, IDisposable> subscribe)
        {
            Dictionary<T, IDisposable> subscriptions = new Dictionary<T, IDisposable>();
            return collection.Subscribe(
                args =>
                {
                    if (args.operationType == OpType.Add)
                    {
                        subscriptions.Add(args.element, subscribe(args.element));
                    }
                    else if (args.operationType == OpType.Remove)
                    {
                        subscriptions[args.element].Dispose();
                        subscriptions.Remove(args.element);
                    }
                },
                onDispose: () =>
                {
                    foreach (var subscription in subscriptions)
                        subscription.Value.Dispose();

                    subscriptions.Clear();
                }
            );
        }

        public static IValueObservable<int> FollowIndexDynamic<T>(this IListObservable<T> list, int index)
            => new FollowIndexObservable<T>(list, index);
    }
}