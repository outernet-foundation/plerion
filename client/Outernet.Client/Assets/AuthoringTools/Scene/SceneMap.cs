using System;

using Outernet.Client.Location;
using Outernet.Shared;

using UnityEngine;
using UnityEngine.EventSystems;

using Cysharp.Threading.Tasks;

using FofX.Stateful;

namespace Outernet.Client.AuthoringTools
{
    [RequireComponent(typeof(MapRenderer))]
    public class SceneMap : Control<SceneMap.Props>, IPointerClickHandler
    {
        public class Props : ObservableObject
        {
            public ObservablePrimitive<Guid> sceneObjectID { get; private set; }
            public ObservablePrimitive<string> name { get; private set; }
            public ObservablePrimitive<Vector3> position { get; private set; }
            public ObservablePrimitive<Quaternion> rotation { get; private set; }
            public ObservablePrimitive<Bounds> bounds { get; private set; }
            public ObservablePrimitive<int> mapID { get; private set; }
            public ObservablePrimitive<long> color { get; private set; }
            public ObservablePrimitiveArray<Vector3> localInputImagePositions { get; private set; }

            public Props() : base() { }

            public Props(Guid sceneObjectID = default, string name = default, Vector3 position = default, Quaternion? rotation = default, Bounds bounds = default, int mapID = default, long color = default, params Vector3[] localInputImagePositions)
            {
                this.sceneObjectID = new ObservablePrimitive<Guid>(sceneObjectID);
                this.name = new ObservablePrimitive<string>(name);
                this.position = new ObservablePrimitive<Vector3>(position);
                this.rotation = new ObservablePrimitive<Quaternion>(rotation ?? Quaternion.identity);
                this.bounds = new ObservablePrimitive<Bounds>(bounds);
                this.mapID = new ObservablePrimitive<int>(mapID);
                this.color = new ObservablePrimitive<long>(color);
                this.localInputImagePositions = new ObservablePrimitiveArray<Vector3>(localInputImagePositions);
            }
        }

        private MapRenderer _mapRenderer;

        private void Awake()
        {
            _mapRenderer = GetComponent<MapRenderer>();
        }

        private void Update()
        {
            if (props.position.value != transform.position)
                props.position.ExecuteSet(transform.position);

            if (props.rotation.value != transform.rotation)
                props.rotation.ExecuteSet(transform.rotation);

            for (int i = 0; i < props.localInputImagePositions.count - 1; i++)
            {
                RuntimeGizmos.DrawLine(
                    transform.TransformPoint(props.localInputImagePositions[i]),
                    transform.TransformPoint(props.localInputImagePositions[i + 1]),
                    0.01f,
                    Color.white
                );
            }
        }

        public override void Setup()
            => InitializeAndBind(new Props());

        public void Setup(Guid sceneObjectID = default, string name = default, Vector3 position = default, Quaternion? rotation = default, Bounds bounds = default, int mapID = default, long color = default, params Vector3[] localInputImagePositions)
            => InitializeAndBind(new Props(sceneObjectID, name, position, rotation, bounds, mapID, color, localInputImagePositions));

        protected override void Bind()
        {
            AddBinding(
                props.position.OnChange(x => transform.position = x),
                props.rotation.OnChange(x => transform.rotation = x),
                props.mapID.OnChange(x =>
                {
                    if (x != 0)
                        _mapRenderer.Load(x, props.name.value).Forget();
                }),
                props.color.OnChange(x =>
                {
                    var color = ColorExtensions.ToColor(x);
                    color.a = 1f;
                    _mapRenderer.SetColor(color);
                })
            );
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            AuthoringToolsApp.SetSelectedObjects(props.sceneObjectID.value);
        }
    }
}