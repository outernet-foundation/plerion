using System;
using System.Linq;

using UnityEngine;
using FofX.Stateful;

using Unity.Mathematics;
using System.Collections.Generic;

namespace Outernet.Client
{
    public class SetPrimitiveValueAction<T> : ObservableNodeAction<ObservablePrimitive<T>>
    {
        private T _value;

        public SetPrimitiveValueAction(T value)
        {
            _value = value;
        }

        public override void Execute(ObservablePrimitive<T> target)
        {
            target.value = _value;
        }
    }

    public class AddValueToSetAction<T> : ObservableNodeAction<ObservableSet<T>>
    {
        private T _value;

        public AddValueToSetAction(T value)
        {
            _value = value;
        }

        public override void Execute(ObservableSet<T> target)
        {
            target.Add(_value);
        }
    }

    public class RemoveValueFromSetAction<T> : ObservableNodeAction<ObservableSet<T>>
    {
        private T _value;

        public RemoveValueFromSetAction(T value)
        {
            _value = value;
        }

        public override void Execute(ObservableSet<T> target)
        {
            target.Remove(_value);
        }
    }

    public class SetValuesInSetAction<T> : ObservableNodeAction<ObservableSet<T>>
    {
        private T[] _values;

        public SetValuesInSetAction(T[] values)
        {
            _values = values;
        }

        public override void Execute(ObservableSet<T> target)
        {
            target.SetFrom(_values);
        }
    }

    public class AddKeyToDictionaryAction<TKey, TValue> : ObservableNodeAction<ObservableDictionary<TKey, TValue>>
        where TValue : IObservableNode, new()
    {
        private TKey _key;

        public AddKeyToDictionaryAction(TKey key)
        {
            _key = key;
        }

        public override void Execute(ObservableDictionary<TKey, TValue> target)
        {
            target.Add(_key);
        }
    }

    public class RemoveKeyFromDictionaryAction<TKey, TValue> : ObservableNodeAction<ObservableDictionary<TKey, TValue>>
        where TValue : IObservableNode, new()
    {
        private TKey _key;

        public RemoveKeyFromDictionaryAction(TKey key)
        {
            _key = key;
        }

        public override void Execute(ObservableDictionary<TKey, TValue> target)
        {
            target.Remove(_key);
        }
    }

    public class SetMapsAction : ObservableNodeAction<ClientState>
    {
        private PlerionClient.Model.LocalizationMapRead[] _maps;

        public SetMapsAction(PlerionClient.Model.LocalizationMapRead[] maps)
        {
            _maps = maps;
        }

        public override void Execute(ClientState target)
        {
            var newMapsByID = _maps.ToDictionary(x => x.Id);
            var oldMapsByID = target.maps.ToDictionary(x => x.key, x => x.value);

            foreach (var toRemove in oldMapsByID.Where(x => !newMapsByID.ContainsKey(x.Key)))
                new DestroySceneObjectAction(toRemove.Key).Execute(target);

            foreach (var toUpdate in newMapsByID.Select(x => x.Value))
            {
                new AddOrUpdateMapAction(
                    id: toUpdate.Id,
                    name: toUpdate.Name,
                    position: new double3() { x = toUpdate.PositionX, y = toUpdate.PositionY, z = toUpdate.PositionZ },
                    rotation: new Quaternion((float)toUpdate.RotationX, (float)toUpdate.RotationY, (float)toUpdate.RotationZ, (float)toUpdate.RotationW),
                    lighting: (Shared.Lighting)toUpdate.Lighting,
                    color: toUpdate.Color
                // UNDO TYLER
                // localInputImagePositions: ParsePoints(toUpdate.Points)
                ).Execute(target);
            }
        }

        private double3[] ParsePoints(List<double> flatPoints)
        {
            var result = new double3[flatPoints.Count / 3];
            for (int i = 0; i < flatPoints.Count; i += 3)
                result[i / 3] = new double3(flatPoints[i], flatPoints[i + 1], flatPoints[i + 2]);

            return result;
        }
    }

    public class AddOrUpdateMapAction : ObservableNodeAction<ClientState>
    {
        private Guid _id;
        private string _name;
        private double3 _position;
        private Quaternion _rotation;
        private Shared.Lighting _lighting;
        private long _color;
        private double3[] _localInputImagePositions;

        public AddOrUpdateMapAction(
            Guid id,
            string name = default,
            double3 position = default,
            Quaternion rotation = default,
            Shared.Lighting lighting = default,
            long color = default,
            double3[] localInputImagePositions = default)
        {
            _id = id;
            _name = name;
            _position = position;
            _rotation = rotation;
            _lighting = lighting;
            _color = color;
            _localInputImagePositions = localInputImagePositions;
        }

        public override void Execute(ClientState target)
        {
            var map = target.maps.GetOrAdd(_id);
            var transform = target.transforms.GetOrAdd(_id);

            map.name.value = _name;
            map.lighting.value = _lighting;
            map.color.value = _color;
            map.localInputImagePositions.SetValue(_localInputImagePositions);

            transform.position.value = _position;
            transform.rotation.value = _rotation;
        }
    }

    public class SetNodesAction : ObservableNodeAction<ClientState>
    {
        private PlerionClient.Model.NodeRead[] _nodes;

        public SetNodesAction(PlerionClient.Model.NodeRead[] nodes)
        {
            _nodes = nodes;
        }

        public override void Execute(ClientState target)
        {
            var newMapsByID = _nodes.ToDictionary(x => x.Id);
            var oldMapsByID = target.nodes.ToDictionary(x => x.key, x => x.value);

            foreach (var toRemove in oldMapsByID.Where(x => !newMapsByID.ContainsKey(x.Key)))
                new DestroySceneObjectAction(toRemove.Key).Execute(target);

            foreach (var toUpdate in newMapsByID.Select(x => x.Value))
            {
                new AddOrUpdateNodeAction(
                    id: toUpdate.Id,
                    name: toUpdate.Name,
                    label: toUpdate.Label,
                    labelType: (Shared.LabelType)(toUpdate.LabelType.HasValue ? toUpdate.LabelType.Value : default),
                    link: toUpdate.Link,
                    linkType: (Shared.LinkType)(toUpdate.LinkType.HasValue ? toUpdate.LinkType.Value : default),
                    labelScale: (float)(toUpdate.LabelScale.HasValue ? toUpdate.LabelScale.Value : default),
                    labelWidth: (float)(toUpdate.LabelWidth.HasValue ? toUpdate.LabelWidth.Value : default),
                    labelHeight: (float)(toUpdate.LabelHeight.HasValue ? toUpdate.LabelHeight.Value : default),
                    layer: toUpdate.LayerId.HasValue ? toUpdate.LayerId.Value : Guid.Empty,
                    parentID: toUpdate.ParentId,
                    position: new double3() { x = toUpdate.PositionX, y = toUpdate.PositionY, z = toUpdate.PositionZ },
                    rotation: new Quaternion((float)toUpdate.RotationX, (float)toUpdate.RotationY, (float)toUpdate.RotationZ, (float)toUpdate.RotationW)
                ).Execute(target);
            }
        }
    }

    public class AddOrUpdateNodeAction : ObservableNodeAction<ClientState>
    {
        private Guid _id;
        private string _name;
        private Guid? _parentID;
        private string _label;
        private Shared.LabelType _labelType;
        private string _link;
        private Shared.LinkType _linkType;
        private float _labelScale;
        private float _labelWidth;
        private float _labelHeight;
        private Guid _layer;
        private Guid[] _hoveringUsers;
        private Guid _interactingUser;
        private bool _exhibitOpen;
        private double3 _exhibitPosition;
        private Quaternion _exhibitRotation;
        private Vector2 _exhibitPanelDimensions;
        private float _exhibitPanelScrollPosition;

        private double3 _position;
        private Quaternion _rotation;

        public static AddOrUpdateNodeAction FromRecord(Guid uuid, Shared.NodeRecord record)
        {
            return new AddOrUpdateNodeAction(
                uuid,
                label: record.label.Value,
                labelType: record.labelType.Value,
                link: record.link.Value,
                linkType: record.linkType.Value,
                labelScale: record.labelScale.Value,
                labelWidth: record.labelWidth.Value,
                labelHeight: record.labelHeight.Value,
                layer: record.layer.Value,
                hoveringUsers: record.hoveringUsers.ToArray(),
                interactingUser: record.interactingUser.Value,
                exhibitOpen: record.exhibitOpen.Value,
                exhibitPosition: record.exhibitGeoPose.ecefPosition.Value.ToMathematicsDouble3(),
                exhibitRotation: record.exhibitGeoPose.ecefRotation.Value,
                exhibitPanelDimensions: record.exhibitPanelDimensions.Value,
                exhibitPanelScrollPosition: record.exhibitPanelScrollPosition.Value,
                position: record.geoPose.ecefPosition.Value.ToMathematicsDouble3(),
                rotation: record.geoPose.ecefRotation.Value
            );
        }

        public AddOrUpdateNodeAction(
            Guid id,
            string name = default,
            string label = default,
            Shared.LabelType labelType = default,
            string link = default,
            Shared.LinkType linkType = default,
            float labelScale = default,
            float labelWidth = default,
            float labelHeight = default,
            Guid layer = default,
            Guid? parentID = default,
            Guid[] hoveringUsers = default,
            Guid interactingUser = default,
            bool exhibitOpen = default,
            double3 exhibitPosition = default,
            Quaternion exhibitRotation = default,
            Vector2 exhibitPanelDimensions = default,
            float exhibitPanelScrollPosition = default,
            double3 position = default,
            Quaternion rotation = default
        )
        {
            _id = id;
            _name = name;
            _label = label;
            _labelType = labelType;
            _link = link;
            _linkType = linkType;
            _labelScale = labelScale;
            _labelWidth = labelWidth;
            _labelHeight = labelHeight;
            _layer = layer;
            _parentID = parentID;
            _hoveringUsers = hoveringUsers;
            _interactingUser = interactingUser;
            _exhibitOpen = exhibitOpen;
            _exhibitPosition = exhibitPosition;
            _exhibitRotation = exhibitRotation;
            _exhibitPanelDimensions = exhibitPanelDimensions;
            _exhibitPanelScrollPosition = exhibitPanelScrollPosition;
            _position = position;
            _rotation = rotation;
        }

        public override void Execute(ClientState target)
        {
            var node = target.nodes.GetOrAdd(_id);
            var transform = target.transforms.GetOrAdd(_id);

            node.name.value = _name;
            node.label.value = _label;
            node.labelType.value = _labelType;
            node.link.value = _link;
            node.linkType.value = _linkType;
            node.labelScale.value = _labelScale;
            node.labelWidth.value = _labelWidth;
            node.labelHeight.value = _labelHeight;
            node.layer.value = _layer;
            node.parentID.value = _parentID;
            node.hoveringUsers.SetFrom(_hoveringUsers);
            node.interactingUser.value = _interactingUser;
            node.exhibitOpen.value = _exhibitOpen;
            node.exhibitPosition.value = _exhibitPosition;
            node.exhibitRotation.value = _exhibitRotation;
            node.exhibitPanelDimensions.value = _exhibitPanelDimensions;
            node.exhibitPanelScrollPosition.value = _exhibitPanelScrollPosition;

            transform.position.value = _position;
            transform.rotation.value = _rotation;
        }
    }

    public class ClearSceneObjectsAction : ObservableNodeAction<ClientState>
    {
        public override void Execute(ClientState target)
        {
            foreach (var componentDict in target.authoringTools.componentDictionaries)
                componentDict.Clear();

            target.authoringTools.selectedObjects.Clear();
        }
    }

    public class DestroySceneObjectAction : ObservableNodeAction<ClientState>
    {
        private Guid _sceneObjectID;

        public DestroySceneObjectAction(Guid sceneObjectID)
        {
            _sceneObjectID = sceneObjectID;
        }

        public override void Execute(ClientState target)
        {
            if (target.authoringTools.nodeGroups.TryGetValue(_sceneObjectID, out var group))
            {
                foreach (var child in group.childObjects.ToArray())
                    new DestroySceneObjectAction(child).Execute(target);
            }

            target.authoringTools.selectedObjects.Remove(_sceneObjectID);

            foreach (var componentDict in target.authoringTools.componentDictionaries)
                componentDict.Remove(_sceneObjectID);
        }
    }

    public class UpdateNodeLocationsAction : ObservableNodeAction<ClientState>
    {
        private double4x4 _ecefToLocalMatrix;
        private double4x4 _localToEcefMatrix;
        private NodeProps[] _toUpdate;

        public UpdateNodeLocationsAction(double4x4 ecefToLocalMatrix, NodeProps[] toUpdate)
        {
            _ecefToLocalMatrix = ecefToLocalMatrix;
            _localToEcefMatrix = math.inverse(_ecefToLocalMatrix);
            _toUpdate = toUpdate;
        }

        public override void Execute(ClientState target)
        {
            foreach (var clientNode in _toUpdate)
            {
                var transform = target.transforms[clientNode.uuid.value];
                var localNodeTransform = Utility.EcefToLocal(
                    _ecefToLocalMatrix,
                    transform.position.value,
                    transform.rotation.value
                );

                clientNode.position.value = localNodeTransform.position;
                clientNode.rotation.value = localNodeTransform.rotation;

                if (clientNode.exhibitOpen.value)
                {
                    var nodeState = target.nodes[clientNode.uuid.value];

                    if (clientNode.interacting.value)
                    {
                        var ecefExhibitTransform = Utility.LocalToEcef(
                            _localToEcefMatrix,
                            clientNode.exhibitPosition.value,
                            clientNode.exhibitRotation.value
                        );

                        nodeState.exhibitPosition.value = ecefExhibitTransform.position;
                        nodeState.exhibitRotation.value = ecefExhibitTransform.rotation;
                    }
                    else
                    {
                        var localExhibitTransform = Utility.EcefToLocal(
                            _ecefToLocalMatrix,
                            nodeState.exhibitPosition.value,
                            nodeState.exhibitRotation.value
                        );

                        clientNode.exhibitPosition.value = localExhibitTransform.position;
                        clientNode.exhibitRotation.value = localExhibitTransform.rotation;
                    }
                }
            }
        }
    }

    public class RemoveLayerAction : ObservableNodeAction<ClientState>
    {
        private Guid _layer;

        public RemoveLayerAction(Guid layer)
        {
            _layer = layer;
        }

        public override void Execute(ClientState target)
        {
            target.layers.Remove(_layer);
            foreach (var node in target.nodes)
            {
                if (node.value.layer.value == _layer)
                    node.value.layer.value = Guid.Empty;
            }
        }
    }
}
