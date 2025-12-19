using System;
using Nessle;
using ObserveThing;
using UnityEngine;
using UnityEngine.UI;
using static Nessle.UIBuilder;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public class TabbedMenuProps
        {
            public IListObservable<string> tabs;
            public ImageProps background;
            public ImageProps deselectedBackground;
            public ImageProps selectedBackground;
            public IValueObservable<float> tabSpacing;
            public IValueObservable<int> value;
            public Action<int> onValueChanged;
        }

        public static IControl TabbedMenu(TabbedMenuProps props = default)
        {
            props.selectedBackground.color = props.selectedBackground.color ?? Props.From(elements.foregroundColor);
            props.selectedBackground.pixelsPerUnitMultiplier = props.selectedBackground.pixelsPerUnitMultiplier ?? Props.From(1.3f);
            props.selectedBackground.imageType = props.selectedBackground.imageType ?? Props.From(UnityEngine.UI.Image.Type.Sliced);
            props.selectedBackground.sprite = props.selectedBackground.sprite ?? Props.From(elements.roundedRect);
            props.selectedBackground.fillCenter = props.selectedBackground.fillCenter ?? Props.From(true);

            props.background.color = props.background.color ?? Props.From(elements.backgroundColor);
            props.background.pixelsPerUnitMultiplier = props.background.pixelsPerUnitMultiplier ?? Props.From(1f);
            props.background.imageType = props.background.imageType ?? Props.From(UnityEngine.UI.Image.Type.Sliced);
            props.background.sprite = props.background.sprite ?? Props.From(elements.roundedRect);
            props.background.fillCenter = props.background.fillCenter ?? Props.From(true);

            ValueObservable<int> selectedTabIndex = new ValueObservable<int>(-1);

            var control = Control(new GameObject("Tabbed Menu")).Children(
                Image(props.background).FillParent(),
                Columns(props.tabSpacing)
                    .FillParent()
                    .OffsetMin(new Vector2(10, 10))
                    .OffsetMax(new Vector2(-10, -10))
                    .Style(columns => columns.Children(
                        props.tabs.CreateDynamic(tabLabel =>
                        {
                            var tab = Control(new GameObject("tab"));
                            var tabIndex = columns.children.IndexOfDynamic(tab);
                            var currentTabIndex = -1;
                            var currentBackground = Observables.Combine(
                                tabIndex,
                                selectedTabIndex,
                                (index, selectedIndex) => index == selectedIndex ? props.selectedBackground : props.deselectedBackground
                            );

                            tab.AddBinding(tabIndex.Subscribe(x => currentTabIndex = x.currentValue));

                            var button = tab.gameObject.AddComponent<Button>();
                            button.onClick.AddListener(() => selectedTabIndex.From(currentTabIndex));

                            return tab.Children(
                                Image(new ImageProps()
                                {
                                    sprite = currentBackground.SelectDynamic(x => x.sprite),
                                    color = currentBackground.SelectDynamic(x => x.color),
                                    imageType = currentBackground.SelectDynamic(x => x.imageType),
                                    fillCenter = currentBackground.SelectDynamic(x => x.fillCenter),
                                    pixelsPerUnitMultiplier = currentBackground.SelectDynamic(x => x.pixelsPerUnitMultiplier),
                                    raycastTarget = currentBackground.SelectDynamic(x => x.raycastTarget),
                                    raycastPadding = currentBackground.SelectDynamic(x => x.raycastPadding),
                                    useSpriteMesh = currentBackground.SelectDynamic(x => x.useSpriteMesh),
                                    preserveAspect = currentBackground.SelectDynamic(x => x.preserveAspect),
                                    fillOrigin = currentBackground.SelectDynamic(x => x.fillOrigin),
                                    fillMethod = currentBackground.SelectDynamic(x => x.fillMethod),
                                    fillAmount = currentBackground.SelectDynamic(x => x.fillAmount)
                                }).FillParent(),
                                Text(new TextProps()
                                {
                                    value = Props.From(tabLabel),
                                    style = new TextStyleProps()
                                    {
                                        verticalAlignment = Props.From(TMPro.VerticalAlignmentOptions.Capline),
                                        horizontalAlignment = Props.From(TMPro.HorizontalAlignmentOptions.Center)
                                    }
                                }).FillParent()
                            );
                        })
                    ))
            );

            control.AddBinding(
                props.tabs.Subscribe(_ => selectedTabIndex.From(0)),
                props.value.Subscribe(x => selectedTabIndex.From(x.currentValue)),
                selectedTabIndex.Subscribe(x => props.onValueChanged?.Invoke(x.currentValue))
            );

            return control;
        }
    }
}