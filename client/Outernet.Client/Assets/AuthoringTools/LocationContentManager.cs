using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using UnityEngine;
using Unity.Mathematics;

using Cysharp.Threading.Tasks;

using FofX;
using FofX.Stateful;

using Outernet.Client.Location;
using CesiumForUnity;

namespace Outernet.Client.AuthoringTools
{
    public class LocationContentManager : MonoBehaviour
    {
        private TaskHandle _updateLocationAndContentTask = TaskHandle.Complete;
        private double2? _loadedLocation;
        private float _loadedDrawDistance;

        private void Awake()
        {
            App.RegisterObserver(HandleLocationChanged, App.state.authoringTools.location, App.state.authoringTools.settings.nodeFetchRadius);
        }

        private void OnDestroy()
        {
            _updateLocationAndContentTask.Cancel();
        }

        private void HandleLocationChanged(NodeChangeEventArgs args)
        {
            _updateLocationAndContentTask.Cancel();

            if (_loadedLocation.Equals(App.state.authoringTools.location.value) &&
                _loadedDrawDistance == App.state.authoringTools.settings.nodeFetchRadius.value)
            {
                return;
            }

            if (!App.state.authoringTools.location.value.HasValue)
            {
                _loadedLocation = App.state.authoringTools.location.value;
                _loadedDrawDistance = App.state.authoringTools.settings.nodeFetchRadius.value;
                App.ExecuteActionOrDelay(new ClearSceneObjectsAction());
                return;
            }

            _updateLocationAndContentTask = TaskHandle.Execute(token => HandleUnsavedChangesAndLoadContent(
                App.state.authoringTools.location.value.Value,
                App.state.authoringTools.settings.nodeFetchRadius.value,
                Utility.GetPreviousValue(App.state.authoringTools.location, args.changes),
                Utility.GetPreviousValue(App.state.authoringTools.settings.nodeFetchRadius, args.changes),
                token
            ));
        }

        private async UniTask HandleUnsavedChangesAndLoadContent(double2 location, float drawDistance, double2? previousLocation, float previousDrawDistance, CancellationToken cancellationToken = default)
        {
            if (App.state.authoringTools.hasUnsavedChanges.value)
            {
                var dialog = Dialogs.UnsavedChangesDialog(
                    title: "Unsaved Changes",
                    text: "Changing locations or node fetch distance will overwrite unsaved changes. \nWould you like to save before changing locations?",
                    allowCancel: true
                );

                var dialogProps = (Dialogs.UnsavedChangesDialogProps)dialog.props;

                cancellationToken.Register(() =>
                {
                    if (dialog == null)
                        return;

                    Destroy(dialog.gameObject);
                });

                await UniTask.WaitUntil(() => dialogProps.status.value != DialogStatus.Pending, cancellationToken: cancellationToken);

                if (dialogProps.status.value == DialogStatus.Canceled)
                {
                    App.state.authoringTools.location.ExecuteSetOrDelay(previousLocation);
                    App.state.authoringTools.settings.nodeFetchRadius.ExecuteSetOrDelay(previousDrawDistance);
                    return;
                }

                if (dialogProps.saveRequested.value)
                {
                    var loadingDialog = Dialogs.Show(
                        title: "Saving",
                        allowCancel: false,
                        minimumWidth: 200,
                        constructControls: props => UIBuilder.Text("Please wait", horizontalAlignment: TMPro.HorizontalAlignmentOptions.Center)
                    );

                    cancellationToken.Register(() =>
                    {
                        if (loadingDialog == null)
                            return;

                        Destroy(loadingDialog.gameObject);
                    });

                    App.state.authoringTools.saveRequested.ExecuteSetOrDelay(true);
                    await UniTask.WaitUntil(() => !App.state.authoringTools.hasUnsavedChanges.value);

                    Destroy(loadingDialog.gameObject);
                }
            }

            _loadedLocation = location;
            _loadedDrawDistance = drawDistance;

            await LoadContent(location.x, location.y, drawDistance, cancellationToken);
        }

        private async UniTask LoadContent(double latitude, double longitude, double radius, CancellationToken cancellationToken = default)
        {
            App.ExecuteActionOrDelay(new SetLocationContentLoadedAction(false));

            var dialog = Dialogs.Show(
                title: "Loading Content",
                allowCancel: false,
                minimumWidth: 250,
                constructControls: props => UIBuilder.Text("Please wait", horizontalAlignment: TMPro.HorizontalAlignmentOptions.Center)
            );

            cancellationToken.Register(() =>
            {
                if (dialog == null)
                    return;

                Destroy(dialog.gameObject);
            });

            var heights = await CesiumAPI.GetHeights(new List<(double latitude, double longitude)> { (latitude, longitude) });
            cancellationToken.ThrowIfCancellationRequested();

            if (heights == null || heights.Count == 0)
                throw new Exception("No heights found.");

            var height = heights[0];
            var ecefCoordinates = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(longitude, latitude, height));

            LocalizedReferenceFrame.SetTargetLocalToEcefTransform(
                Double4x4.FromTranslationRotation(
                    ecefCoordinates,
                    Client.Utility.GetEUNRotationFromECEFPosition(ecefCoordinates)
                ),
                smoothTransition: false
            );

            List<LocalizationMapRecord> maps = null;
            List<NodeRecord> nodes = null;
            List<NodeGroupRecord> nodeGroups = null;

            await UniTask.WhenAll(
                PlerionAPI.GetMapsWithinRadiusAsync(latitude, longitude, height, radius, Settings.lightingCondition)
                    .ContinueWith(x => maps = x),
                PlerionAPI.GetNodes(new double3[] { ecefCoordinates }, radius, 9999)
                    .ContinueWith(x =>
                    {
                        nodes = x;
                        return GetNodeGroupsRecursive(x, cancellationToken);
                    })
                    .ContinueWith(x => nodeGroups = x)
            );

            await UniTask.SwitchToMainThread(cancellationToken);

            App.ExecuteActionOrDelay(
                new SetMapsAction(maps.ToArray()),
                new SetNodesAction(nodes.ToArray()),
                new SetNodeGroupsAction(nodeGroups.ToArray())
            );

            Destroy(dialog.gameObject);

            App.ExecuteActionOrDelay(new SetLocationContentLoadedAction(true));
        }

        private async UniTask<List<NodeGroupRecord>> GetNodeGroupsRecursive(List<NodeRecord> nodes, CancellationToken cancellationToken = default)
        {
            var groups = new List<NodeGroupRecord>();

            var directGroups = await PlerionAPI.GetNodeGroups(
                nodes.Where(x => x.group_uuid.HasValue)
                    .Select(x => x.group_uuid.Value)
                    .Distinct()
                    .ToArray()
            );

            groups.AddRange(directGroups);

            cancellationToken.ThrowIfCancellationRequested();

            while (groups.Any(x => x.parent_uuid.HasValue && !groups.Any(y => y.uuid == x.parent_uuid)))
            {
                var recursiveGroups = await PlerionAPI.GetNodeGroups(
                    groups.Where(x => x.parent_uuid.HasValue && !groups.Any(y => y.uuid == x.parent_uuid))
                        .Select(x => x.parent_uuid.Value)
                        .Distinct()
                        .ToArray()
                );

                groups.AddRange(recursiveGroups);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return groups;
        }
    }
}
