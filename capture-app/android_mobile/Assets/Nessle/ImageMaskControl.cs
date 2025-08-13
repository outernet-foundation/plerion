using System;
using UnityEngine.UI;

namespace Nessle
{
    public class ImageMaskControl : UnityComponentControl<Mask>
    {
        public UnityComponentControl<Image> image { get; private set; }

        public ImageMaskControl() : base(UIBuilder.GameObject<Mask>())
        {
            image = new UnityComponentControl<Image>(gameObject.AddComponent<Image>());
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
