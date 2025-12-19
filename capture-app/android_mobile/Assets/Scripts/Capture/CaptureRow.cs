using System;

using UnityEngine;

using TMPro;

using FofX.Stateful;

using Nessle;

using ObserveThing;
using ObserveThing.StatefulExtensions;

using static Nessle.UIBuilder;

namespace PlerionClient.Client
{
    public static partial class UIElements
    {
        public static IControl CaptureRow(CaptureState capture)
        {
            return VerticalLayout(new LayoutProps()
            {
                childControlWidth = Props.From(true),
                childControlHeight = Props.From(true),
                spacing = Props.From(10f),
                padding = Props.From(new RectOffset(30, 30, 30, 30))
            }).Children(
                Image(props: new ImageProps() { color = Props.From(new Color(.25f, .25f, .25f, 1f)) })
                    .IgnoreLayout(true)
                    .FillParent(),
                LabeledControl(new LabeledControlProps()
                {
                    label = new TextProps() { value = Props.From("Name") },
                    labelWidth = Props.From(240f),
                    control = HorizontalLayout().Children(
                        InputField(new InputFieldProps()
                        {
                            value = capture.name.AsObservable(),
                            placeholderValue = Props.From($"<i>Unnamed [{capture.id}]"),
                            inputTextStyle = new TextStyleProps()
                            {
                                verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                                textWrappingMode = Props.From(TextWrappingModes.Normal),
                                overflowMode = Props.From(TextOverflowModes.Ellipsis)
                            },
                            placeholderTextStyle = new TextStyleProps()
                            {
                                verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                                textWrappingMode = Props.From(TextWrappingModes.Normal),
                                overflowMode = Props.From(TextOverflowModes.Ellipsis)
                            },
                            onEndEdit = x => capture.name.ExecuteSetOrDelay(x)
                        }).FlexibleWidth(true),
                        RoundIconButton(new RoundIconButtonProps()
                        {
                            icon = new ImageProps()
                            {
                                sprite = Props.From(elements.moreMenuSprite),
                                preserveAspect = Props.From(true)
                            },
                            onClick = () => CaptureController.UI.children.Add(
                                CaptureDataDialog(capture).FillParent()
                            )
                        })
                    )
                }),
                LabeledControl(new LabeledControlProps()
                {
                    label = new TextProps() { value = Props.From("Source") },
                    labelWidth = Props.From(240f),
                    control = Text(new TextProps()
                    {
                        value = capture.type.AsObservable().SelectDynamic(x => x == PlerionApiClient.Model.DeviceType.ARFoundation ? "Mobile" : "Zed"),
                        style = new TextStyleProps()
                        {
                            verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                            horizontalAlignment = Props.From(HorizontalAlignmentOptions.Right)
                        }
                    })
                }),
                LabeledControl(new LabeledControlProps()
                {
                    label = new TextProps() { value = Props.From("Created At") },
                    labelWidth = Props.From(240f),
                    control = Text(new TextProps()
                    {
                        value = capture.createdAt.AsObservable().SelectDynamic(x => x.ToString()),
                        style = new TextStyleProps()
                        {
                            verticalAlignment = Props.From(VerticalAlignmentOptions.Capline),
                            horizontalAlignment = Props.From(HorizontalAlignmentOptions.Right)
                        }
                    })
                }),
                Row(new LayoutProps() { childAlignment = Props.From(TextAnchor.MiddleRight) })
                    .FlexibleWidth(true)
                    .Children(
                        LabeledButton(new LabeledButtonProps()
                        {
                            label = capture.status.AsObservable().SelectDynamic(x =>
                                x switch
                                {
                                    CaptureUploadStatus.NotUploaded => "Upload",
                                    CaptureUploadStatus.UploadRequested => "Initializing",
                                    CaptureUploadStatus.Initializing => "Initializing",
                                    CaptureUploadStatus.Uploading => "Uploading",
                                    CaptureUploadStatus.ReconstructionNotStarted => "Reconstruct",
                                    CaptureUploadStatus.ReconstructRequested => "Constructing",
                                    CaptureUploadStatus.Reconstructing => "Constructing",
                                    CaptureUploadStatus.Uploaded => "Create Map",
                                    CaptureUploadStatus.CreateMapRequested => "Create Map",
                                    CaptureUploadStatus.MapCreated => "Map Created",
                                    CaptureUploadStatus.Failed => "Failed",
                                    _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                                }
                            ),
                            interactable = capture.status.AsObservable().SelectDynamic(x =>
                                x == CaptureUploadStatus.NotUploaded ||
                                x == CaptureUploadStatus.ReconstructionNotStarted ||
                                x == CaptureUploadStatus.Uploaded
                            ),
                            onClick = () =>
                            {
                                if (capture.status.value == CaptureUploadStatus.NotUploaded)
                                {
                                    capture.status.ExecuteSetOrDelay(CaptureUploadStatus.UploadRequested);
                                }
                                else if (capture.status.value == CaptureUploadStatus.ReconstructionNotStarted)
                                {
                                    capture.status.ExecuteSetOrDelay(CaptureUploadStatus.ReconstructRequested);
                                }
                                else if (capture.status.value == CaptureUploadStatus.Uploaded)
                                {
                                    capture.status.ExecuteSetOrDelay(CaptureUploadStatus.CreateMapRequested);
                                }
                            }
                        })
                    )
            );
        }
    }
}