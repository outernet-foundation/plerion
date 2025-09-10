using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using FofX;

using System.Threading;

namespace Outernet.Client.Location
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MapRenderer : MonoBehaviour
    {
        public enum Status
        {
            None,
            Fetching,
            Loading,
            Loaded
        }

        public static float pointSize = 0.01f;

        public Guid mapID { get; private set; }
        public string mapName { get; private set; }

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        public Status status { get; private set; } = Status.None;
        public int nativeHandle { get; private set; }

        public bool renderVisualization
        {
            get { return meshRenderer.enabled; }
            set { meshRenderer.enabled = value; }
        }

        private TaskHandle _loadTask = TaskHandle.Complete;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = true;
            meshRenderer.material = new Material(Shader.Find("Outernet/Point Cloud"));
            meshRenderer.material.SetFloat("_PointSize", pointSize);
        }

        private void OnDestroy()
        {
            _loadTask.Cancel();
            Unload(mesh, mapName, nativeHandle).Forget();
            Destroy(meshRenderer.material);
        }

        public void SetColor(Color color)
            => meshRenderer.material.SetColor("_PointColor", color);

        public UniTask Load(Guid mapID, string mapName, Action<string> progressCallback = null)
        {
            _loadTask.Cancel();
            Unload(mesh, this.mapName, nativeHandle).Forget();
            nativeHandle = 0;

            this.mapID = mapID;
            this.mapName = mapName;
            _loadTask = TaskHandle.Execute(token => Load(
                mapID,
                mapName,
                progressCallback == null ? new Progress<string>() : new Progress<string>(progressCallback),
                cancellationToken: token
            ));

            return _loadTask.completionTask;
        }

        private async UniTask Load(Guid mapID, string mapName, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.Register(() =>
            {
                progress.Report($"Map {mapName} load aborted (user moved away)");
                Log.Info(LogGroup.Localizer, "Unload requested during load of map {Name}", mapName);
            });

            // Create timer
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            status = Status.Fetching;
            var bytes = await PlerionAPI.DownloadMapBytes(mapID, mapName);
            timer.Stop();
            Log.Info(LogGroup.Localizer, $"Downloaded binary for map {mapName} in {timer.ElapsedMilliseconds}ms");

            cancellationToken.ThrowIfCancellationRequested();

            progress.Report($"Activating map {mapName}...");

            timer.Restart();
            status = Status.Loading;
            nativeHandle = await UniTask.RunOnThreadPool(() => ImmersalNative.LoadMap(bytes), cancellationToken: cancellationToken); // ask tyler about cancellation token here
            timer.Stop();
            Log.Info(LogGroup.Localizer, $"Loaded binary for map {mapName} in {timer.ElapsedMilliseconds}ms");

            timer.Restart();
            int numPoints = await UniTask.RunOnThreadPool(() => ImmersalNative.PointsGetCount(nativeHandle), cancellationToken: cancellationToken);
            timer.Stop();
            Log.Info(LogGroup.Localizer, $"Got point count for map {mapName} in {timer.ElapsedMilliseconds}ms");

            timer.Restart();
            Vector3[] points = await UniTask.RunOnThreadPool(() => ImmersalNative.PointsGet(nativeHandle, numPoints), cancellationToken: cancellationToken);
            timer.Stop();
            Log.Info(LogGroup.Localizer, $"Got points for map {mapName} in {timer.ElapsedMilliseconds}ms");

            cancellationToken.ThrowIfCancellationRequested(); // ask tyler about this

            for (int i = 0; i < numPoints; i++)
            {
                points[i] = points[i].SwitchHandedness();
            }

            mesh = new Mesh();
            mesh.Clear();
            mesh.indexFormat = numPoints > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = points;
            mesh.colors32 = new Color32[numPoints];
            mesh.SetIndices(Enumerable.Range(0, numPoints).ToArray(), MeshTopology.Points, 0);

            var min = new Vector3(
                points.Select(x => x.x).Min(),
                points.Select(x => x.y).Min(),
                points.Select(x => x.z).Min()
            );

            var max = new Vector3(
                points.Select(x => x.x).Max(),
                points.Select(x => x.y).Max(),
                points.Select(x => x.z).Max()
            );

            mesh.bounds = new Bounds((min + max) / 2f, max - min);

            meshFilter.mesh = mesh;

            Log.Info(LogGroup.Localizer, $"Finished loading map {mapName}");

            progress.Report($"Map {mapName} activated");

            status = Status.Loaded;
        }

        private async UniTask Unload(Mesh mesh, string mapName, int nativeHandle)
        {
            Destroy(mesh);

            if (nativeHandle != 0)
            {
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();
                await ImmersalNative.FreeMap(nativeHandle);
                timer.Stop();
                Log.Info(LogGroup.Localizer, $"Freed map {mapName} in {timer.ElapsedMilliseconds}ms");
            }

            Log.Info(LogGroup.Localizer, $"Finished unloading map {mapName}");
        }
    }
}