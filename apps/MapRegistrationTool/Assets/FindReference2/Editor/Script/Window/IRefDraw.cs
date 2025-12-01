using UnityEngine;
namespace vietlabs.fr2
{
    internal interface IRefDraw
    {
        IWindow window { get; }
        int ElementCount();
        bool DrawLayout();
        bool Draw(Rect rect);
    }
}
