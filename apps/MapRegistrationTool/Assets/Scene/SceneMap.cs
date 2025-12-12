using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FofX;
using FofX.Stateful;
using Plerion.Core;
using Plerion.VPS;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Outernet.MapRegistrationTool
{
    [RequireComponent(typeof(LocalizationMapVisualizer))]
    public class SceneMap : Control<SceneMap.Props>, IPointerClickHandler
    {
        public class Props : ObservableObject
        {
            public ObservablePrimitive<Guid> sceneObjectID { get; private set; }
            public ObservablePrimitive<string> name { get; private set; }
            public ObservablePrimitive<Vector3> position { get; private set; }
            public ObservablePrimitive<Quaternion> rotation { get; private set; }
            public ObservablePrimitive<Guid> reconstructionID { get; private set; }

            public Props()
                : base() { }

            public Props(
                Guid sceneObjectID = default,
                string name = default,
                Vector3 position = default,
                Quaternion? rotation = default,
                Guid reconstructionID = default
            )
            {
                this.sceneObjectID = new ObservablePrimitive<Guid>(sceneObjectID);
                this.name = new ObservablePrimitive<string>(name);
                this.position = new ObservablePrimitive<Vector3>(position);
                this.rotation = new ObservablePrimitive<Quaternion>(rotation ?? Quaternion.identity);
                this.reconstructionID = new ObservablePrimitive<Guid>(reconstructionID);
            }
        }

        private LocalizationMapVisualizer _localizationMapVisualizer;
        private TaskHandle _loadPointsTask = TaskHandle.Complete;
        private List<Vector3> _localInputPositions = new List<Vector3>();

        private void Awake()
        {
            _localizationMapVisualizer = GetComponent<LocalizationMapVisualizer>();
        }

        private void Update()
        {
            if (props.position.value != transform.position)
                props.position.ExecuteSet(transform.position);

            if (props.rotation.value != transform.rotation)
                props.rotation.ExecuteSet(transform.rotation);

            for (int i = 0; i < _localInputPositions.Count - 1; i++)
            {
                RuntimeGizmos.DrawLine(
                    transform.TransformPoint(_localInputPositions[i]),
                    transform.TransformPoint(_localInputPositions[i + 1]),
                    0.01f,
                    Color.white
                );
            }
        }

        public override void Setup() => InitializeAndBind(new Props());

        public void Setup(
            Guid sceneObjectID = default,
            string name = default,
            Vector3 position = default,
            Quaternion? rotation = default,
            Guid reconstructionID = default
        ) => InitializeAndBind(new Props(sceneObjectID, name, position, rotation, reconstructionID));

        protected override void Bind()
        {
            AddBinding(
                props.position.OnChange(x => transform.position = x),
                props.rotation.OnChange(x => transform.rotation = x),
                props.reconstructionID.OnChange(x =>
                {
                    _loadPointsTask.Cancel();

                    if (x == Guid.Empty)
                        return;

                    _loadPointsTask = TaskHandle.Execute(async token =>
                    {
                        List<PlerionApiClient.Model.PointCloudPoint> points = default;
                        List<PlerionApiClient.Model.Transform> localInputPositions = default;

                        await UniTask.WhenAll(
                            App.API.GetReconstructionPointsAsync(x, token).AsUniTask().ContinueWith(x => points = x),
                            App.API.GetReconstructionFramePosesAsync(x, token)
                                .AsUniTask()
                                .ContinueWith(x => localInputPositions = x)
                        );

                        await UniTask.SwitchToMainThread(cancellationToken: token);

                        _localInputPositions.AddRange(
                            localInputPositions.Select(x =>
                            {
                                var unityBasis = Plerion.Core.LocationUtilities.ChangeBasisUnityFromOpenCV(
                                    new double3((double)x.Position.X, (double)x.Position.Y, (double)x.Position.Z),
                                    quaternion.identity.ToDouble3x3()
                                );

                                return new Vector3(
                                    (float)unityBasis.Item1.x,
                                    (float)unityBasis.Item1.y,
                                    (float)unityBasis.Item1.z
                                );
                            })
                        );

                        _localizationMapVisualizer.Load(
                            points
                                .Select(point =>
                                {
                                    var (positionUnityBasis, _) =
                                        Plerion.Core.LocationUtilities.ChangeBasisUnityFromOpenCV(
                                            point.Position.ToDouble3(),
                                            double3x3.identity
                                        );
                                    point.Position.X = positionUnityBasis.x;
                                    point.Position.Y = positionUnityBasis.y;
                                    point.Position.Z = positionUnityBasis.z;
                                    return point;
                                })
                                .ToArray()
                        );
                    });
                })
            );
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            App.SetSelectedObjects(props.sceneObjectID.value);
        }

        public static SceneMap Create(
            Guid sceneObjectID = default,
            string name = default,
            Vector3 position = default,
            Quaternion? rotation = default,
            Guid reconstructionID = default,
            Transform parent = default,
            Func<Props, IDisposable> bind = default
        )
        {
            SceneMap instance = Instantiate(Prefabs.Map, parent);
            instance.InitializeAndBind(new Props(sceneObjectID, name, position, rotation, reconstructionID));

            instance.AddBinding(Bindings.OnRelease(() => Destroy(instance)));

            if (bind != null)
                instance.AddBinding(bind(instance.props));

            return instance;
        }
    }
}
