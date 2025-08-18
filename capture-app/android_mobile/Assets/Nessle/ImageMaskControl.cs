using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class ImageMaskControl : UnityControl, IUnityComponentControl<Mask>
    {
        public Mask component { get; }
        public UnityComponentControl<Image> image { get; private set; }

        public ImageMaskControl() : base(new GameObject(nameof(ImageMaskControl), typeof(Mask), typeof(Image)))
        {
            component = gameObject.GetComponent<Mask>();
            image = gameObject.ComponentControl<Image>();
        }
    }

    public static class ImageMaskControlExtensions
    {
        public static T Image<T>(this T view, Action<UnityComponentControl<Image>> accessImage)
            where T : ImageMaskControl
        {
            accessImage(view.image);
            return view;
        }
    }
}
