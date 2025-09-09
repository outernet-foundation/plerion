using System.Buffers;
using MessagePack;
using Outernet.Shared;

namespace Outernet.Server
{
    class Room(SyncedStateSystem syncedStateSystem, string roomID)
    {
        public string roomID = roomID;
        public Dictionary<Guid, Client> clients = [];
        public RoomRecord roomState = new(syncedStateSystem);
        public Queue<byte[]> pendingRemoteStateDeltas = new();
        public TaskCompletionSource<List<NodeRecord>>? fetchNodesTaskCompletionSource = null;
        private SyncedStateSystem syncedStateSystem = syncedStateSystem;

        public void UpdateNodes(bool fetchNodes)
        {
            if (fetchNodesTaskCompletionSource != null)
            {
                if (!fetchNodesTaskCompletionSource.Task.IsCompleted) return;

                var nodes = fetchNodesTaskCompletionSource.Task.Result;
                fetchNodesTaskCompletionSource = null;

                // Add new nodes
                foreach (var node in nodes.Where(node => !roomState.nodes.ContainsKey(node.uuid)))
                {
                    roomState.nodes.EnqueueAdd(node.uuid, new Shared.NodeRecord.InitializationData()
                    {
                        geoPose = new GeoPoseRecord.InitializationData()
                        {
                            ecefPosition = new Double3(node.position_x, node.position_y, node.position_z),
                            ecefRotation = new UnityEngine.Quaternion(node.rotation_x, node.rotation_y, node.rotation_z, node.rotation_w)
                        },
                        link = node.link,
                        linkType = node.link_type,
                        label = node.label,
                        labelType = node.label_type,
                        labelScale = node.label_scale,
                        labelWidth = node.label_width,
                        labelHeight = node.label_height,
                        layer = node.layer
                    });
                }

                // Remove nodes that are no longer present
                var nodeIDs = nodes.Select(node => node.uuid).ToHashSet();
                foreach (var nodeID in roomState.nodes.Keys.Where(nodeID => !nodeIDs.Contains(nodeID)))
                {
                    roomState.nodes.EnqueueRemove(nodeID);
                }

                // // Update existing nodes
                foreach (var node in nodes.Where(node => roomState.nodes.ContainsKey(node.uuid)))
                {
                    roomState.nodes.Get(node.uuid).geoPose.ecefPosition.EnqueueSet(new Double3(node.position_x, node.position_y, node.position_z));
                    roomState.nodes.Get(node.uuid).geoPose.ecefRotation.EnqueueSet(new UnityEngine.Quaternion(node.rotation_x, node.rotation_y, node.rotation_z, node.rotation_w));
                    roomState.nodes.Get(node.uuid).link.EnqueueSet(node.link);
                    roomState.nodes.Get(node.uuid).linkType.EnqueueSet(node.link_type);
                    roomState.nodes.Get(node.uuid).labelScale.EnqueueSet(node.label_scale);
                    roomState.nodes.Get(node.uuid).labelWidth.EnqueueSet(node.label_width);
                    roomState.nodes.Get(node.uuid).labelHeight.EnqueueSet(node.label_height);
                    roomState.nodes.Get(node.uuid).layer.EnqueueSet(node.layer);
                }
            }

            if (fetchNodes)
            {
                fetchNodesTaskCompletionSource = new TaskCompletionSource<List<NodeRecord>>();

                PlerionAPI
                    .GetNodes(
                        roomState.users.Values
                            .Select(user => user.geoPose.ecefPosition.Value)
                            .Where(ecefPosition => ecefPosition.x != 0 || ecefPosition.y != 0 || ecefPosition.z != 0),
                        roomState.settingsNodeFetchRadius.Value,
                        roomState.settingsNodeFetchLimit.Value)
                    .ContinueWith(task => fetchNodesTaskCompletionSource.SetResult(task.Result));
            }
        }

        public void ApplyClientDeltas()
        {
            while (pendingRemoteStateDeltas.Count > 0)
            {
                var reader = new MessagePackReader(pendingRemoteStateDeltas.Dequeue());
                roomState.DeserializeDelta(ref reader);
            }
        }

        public void UpdateClients()
        {
            // Perform a full serialization of the room state to be sent to any client
            // requiring it
            var serializedRoomState = Serialize(roomState);

            foreach (var client in clients)
            {
                if (client.Value.fullSerializationTaskCompletionSource != null)
                {
                    client.Value.fullSerializationTaskCompletionSource.SetResult(serializedRoomState);
                    client.Value.fullSerializationTaskCompletionSource = null;
                    continue;
                }

                // Serialize the per-client delta and send it to that client
                client.Value.group.Single(client.Value.connectionID).OnReceiveRoomStateDelta(SerializeDelta(roomState));
            }

            // Clear the entire delta
            roomState.ClearDelta();
        }

        private static byte[] Serialize(RoomRecord roomState)
        {
            var sequenceWriter = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(sequenceWriter);
            roomState.Serialize(ref writer);
            writer.Flush();
            return sequenceWriter.WrittenSpan.ToArray();
        }

        private static byte[] SerializeDelta(RoomRecord roomState)
        {
            var sequenceWriter = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(sequenceWriter);
            roomState.SerializeDelta(ref writer);
            writer.Flush();
            return sequenceWriter.WrittenSpan.ToArray();
        }
    }
}