using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion
{
    public class LocalizationMapModel
    {
        public Guid Id;
        public string Name;
        public bool Active;
        public int Lighting;
        public long Color;
        public double PositionX;
        public double PositionY;
        public double PositionZ;
        public double RotationX;
        public double RotationY;
        public double RotationZ;
        public double RotationW;
        public Vector3[] Points;

        public double3 EcefPosition => new double3(PositionX, PositionY, PositionZ);
        public quaternion EcefRotation => new quaternion((float)RotationX, (float)RotationY, (float)RotationZ, (float)RotationW);

        public LocalizationMapModel(Guid id = default, string name = default, bool active = default, int lighting = default, long color = default, double positionX = default, double positionY = default, double positionZ = default, double rotationX = default, double rotationY = default, double rotationZ = default, double rotationW = default, Vector3[] points = default)
        {
            this.Id = id;
            // to ensure "name" is required (not null)
            if (name == null)
            {
                throw new ArgumentNullException("name is a required property for LocalizationMapModel and cannot be null");
            }
            this.Name = name;
            this.Active = active;
            this.Lighting = lighting;
            this.Color = color;
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.PositionZ = positionZ;
            this.RotationX = rotationX;
            this.RotationY = rotationY;
            this.RotationZ = rotationZ;
            this.RotationW = rotationW;
            this.Points = points;
        }
    }

    [Serializable]
    public class LocalizeResponse
    {
        public Guid mapID;
        public double3 ecefPosition;
        public quaternion ecefRotation;
        public int confidence;
    }

    [Serializable]
    public class LocalizeRequest
    {
        public double latitude;
        public double longitude;
        public float elevation;
        public Guid[] maps;
        public int width;
        public int height;
        public Vector4 intrinsics;
        public Quaternion cameraRotation;
        public byte[] cameraImage;
    }

    static public class PlerionAPI
    {
        public static async UniTask<LocalizeResponse> Localize(int imageWidth, int imageHeight, Vector4 intrinsics, Quaternion cameraRotation, byte[] cameraImage, double latitude, double longitude, float height)
        {
            return new LocalizeResponse();
        }

        public static async UniTask<LocalizeResponse> Localize(int imageWidth, int imageHeight, Vector4 intrinsics, Quaternion cameraRotation, byte[] cameraImage, params Guid[] maps)
        {
            return new LocalizeResponse();
        }

        public static async UniTask<List<LocalizationMapModel>> GetMapsWithinRadiusAsync(double latitude, double longitude, double height, double radius, Lighting lighting, bool includePoints)
        {
            return new List<LocalizationMapModel>();
        }

        public static async UniTask<LocalizationMapModel> GetMap(Guid id, bool includePoints = false)
        {
            return new LocalizationMapModel(id);
        }
    }
}