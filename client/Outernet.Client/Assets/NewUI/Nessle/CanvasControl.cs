using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nessle
{
    public class CanvasControl : UnityComponentControl<Canvas>
    {
        public UnityComponentControl<CanvasScaler> canvasScalar { get; private set; }
        public UnityComponentControl<GraphicRaycaster> graphicRaycaster { get; private set; }

        public CanvasControl() : base(UIBuilder.GameObject<Canvas>(typeof(CanvasScaler), typeof(GraphicRaycaster)))
        {
            canvasScalar = new UnityComponentControl<CanvasScaler>(gameObject.GetComponent<CanvasScaler>());
            graphicRaycaster = new UnityComponentControl<GraphicRaycaster>(gameObject.GetComponent<GraphicRaycaster>());
        }
    }
}