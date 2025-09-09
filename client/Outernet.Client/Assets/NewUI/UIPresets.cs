using UnityEngine;
using UnityEngine.UI;
using Nessle;
using TMPro;
using Outernet.Client;

public static class UIPresets
{
    public static ScrollRectControl VerticalScrollRect()
        => UIBuilder.ScrollRect().Style(StyleVerticalScrollRect);

    public static void StyleVerticalScrollRect(ScrollRectControl scrollRect)
    {
        scrollRect.EnableHorizontalScroll(false);

        scrollRect.content
            .ControlChildSize(true)
            .ChildForceExpandWidth(true)
            .FitContentVertical(ContentSizeFitter.FitMode.PreferredSize)
            .SetPivot(new Vector2(0, 1))
            .AnchorToTop()
            .Spacing(10)
            .FillParentWidth()
            .Padding(new RectOffset(10, 10, 10, 10));

        scrollRect.viewport.component.showMaskGraphic = false;
    }

    public static ButtonControl PillButton()
        => UIBuilder.Button().Style(StylePillButton);

    public static void StylePillButton(ButtonControl button)
    {
        StylePill(button.background);
        button.contentLayout.Padding(new RectOffset(30, 30, 13, 13));
    }

    public static InputFieldControl PillInputField()
        => UIBuilder.InputField().Style(StylePillInputField);

    public static void StylePillInputField(InputFieldControl inputField)
    {
        StylePill(inputField.background);
        inputField.background.PixelsPerUnitMultiplier(1.4f);
        inputField.inputText.Margin(new Vector4(25, 0, 0, 0));
        inputField.placeholderText.Margin(new Vector4(25, 0, 0, 0));
    }

    public static ScrollbarControl RoundedScrollBar()
        => UIBuilder.Scrollbar().Style(StyleRoundedScrollBar);

    public static void StyleRoundedScrollBar(ScrollbarControl scrollbar)
    {
        StylePill(scrollbar.handle);
        StylePill(scrollbar.background);

        scrollbar.handle.PixelsPerUnitMultiplier(8);
        scrollbar.background.PixelsPerUnitMultiplier(8);
    }

    public static IUnityComponentControl<Image> Pill()
        => UIBuilder.Image().Style(StylePill);

    public static void StylePill(IUnityComponentControl<Image> image)
        => image.Sprite(UIResources.RoundedBox).ImageType(Image.Type.Sliced).PixelsPerUnitMultiplier(1.2f);

    public static UnityComponentControl<HorizontalLayoutGroup> Row()
        => UIBuilder.HorizontalLayout().Style(StyleRow);

    public static void StyleRow(UnityComponentControl<HorizontalLayoutGroup> horizontalLayout)
        => horizontalLayout.ControlChildSize(true).Alignment(TextAnchor.MiddleLeft).Spacing(10).ChildForceExpand(false);

    public static UnityComponentControl<TextMeshProUGUI> PropertyLabel()
        => UIBuilder.Label().Style(StylePropertyLabel);

    public static void StylePropertyLabel(UnityComponentControl<TextMeshProUGUI> label)
        => label.MinWidth(100).PreferredWidth(300).TextWrappingMode(TextWrappingModes.NoWrap).OverflowMode(TextOverflowModes.Ellipsis);

    public static UnityComponentControl<TextMeshProUGUI> Title()
        => UIBuilder.Label().Style(StyleTitle);

    public static void StyleTitle(UnityComponentControl<TextMeshProUGUI> label)
        => label.FontSize(64).Alignment(TextAlignmentOptions.MidlineGeoAligned);
}
