using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using FofX.Stateful;

using Outernet.Client.Location;
using Plerion.VPS;

namespace Outernet.Client.AuthoringTools
{
    public class AuthoringToolsSceneViewManager : MonoBehaviour
    {
        public static Transform sceneRoot => _instance.transform;

        private static AuthoringToolsSceneViewManager _instance;

        private Dictionary<Guid, AuthoringToolsNode> _nodes = new Dictionary<Guid, AuthoringToolsNode>();
        private Dictionary<Guid, SceneMap> _maps = new Dictionary<Guid, SceneMap>();

        private void Awake()
        {
            VisualPositioningSystem.OnEcefToUnityWorldTransformUpdated += () => App.ExecuteActionOrDelay(
                new UpdateNodeLocationsAction(VisualPositioningSystem.EcefToUnityWorldTransform, _nodes.Values.Select(x => x.props).ToArray()),
                new UpdateMapLocationsAction(VisualPositioningSystem.EcefToUnityWorldTransform, _maps.Values.Select(x => x.props).ToArray())
            );

            if (_instance != null)
            {
                Destroy(this);
                throw new Exception($"Only one instance of {nameof(AuthoringToolsSceneViewManager)} allowed in the scene at a time!");
            }

            _instance = this;

            App.state.maps.Each(kvp => SetupMap(kvp.value));
            App.state.nodes.Each(kvp => SetupNode(kvp.value));
        }

        private IDisposable SetupMap(MapState map)
        {
            var transform = App.state.transforms[map.uuid];

            var view = Instantiate(AuthoringToolsPrefabs.SceneMap, sceneRoot);
            view.Setup(sceneObjectID: map.uuid, mapID: map.uuid);
            view.AddBinding(
                Bindings.BindECEFTransform(transform.position, transform.rotation, view.props.position, view.props.rotation),
                view.props.name.From(map.name),
                view.props.bounds.From(transform.bounds),
                view.props.localInputImagePositions.Derive(
                    _ => view.props.localInputImagePositions.SetValue(
                        map.localInputImagePositions
                            .Where((x, i) => i % 3 == 0)
                            .Select(x => new Vector3(-(float)x.x, -(float)x.y, -(float)x.z))
                            .ToArray()
                    ),
                    ObservationScope.All,
                    map.localInputImagePositions
                ),
                Bindings.OnRelease(() => _maps.Remove(map.uuid))
            );

            _maps.Add(map.uuid, view);
            return view;
        }

        private IDisposable SetupNode(NodeState node)
        {
            var transform = App.state.transforms[node.id];
            var instance = AuthoringToolsNode.Create(
                uuid: node.id,
                parent: sceneRoot,
                bind: props => Bindings.Compose(
                    Bindings.BindECEFTransform(transform.position, transform.rotation, props.position, props.rotation),
                    props.bounds.BindTo(transform.bounds),
                    props.visible.From(node.visible),
                    props.link.From(node.link),
                    props.linkType.From(node.linkType),
                    props.label.From(node.label),
                    props.labelType.From(node.labelType),
                    props.labelScale.From(node.labelScale),
                    props.labelDimensions.Derive(
                        _ => props.labelDimensions.value = new Vector2(
                            node.labelWidth.value,
                            node.labelHeight.value
                        ),
                        ObservationScope.Self,
                        node.labelWidth,
                        node.labelHeight
                    ),
                    Bindings.OnRelease(() => _nodes.Remove(node.id))
                )
            );

            _nodes.Add(node.id, instance);
            return instance;
        }
    }
}