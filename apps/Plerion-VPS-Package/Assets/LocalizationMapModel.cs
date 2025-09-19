using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Plerion.VPS
{
    public class LocalizationMapModel
    {
        public Guid Id;
        public string Name;
        public bool Active;
        public int Lighting;
        public int Color;
        public double PositionX;
        public double PositionY;
        public double PositionZ;
        public double RotationX;
        public double RotationY;
        public double RotationZ;
        public double RotationW;
        public List<double> Points;

        public double3 EcefPosition => new double3(PositionX, PositionY, PositionZ);
        public quaternion EcefRotation => new quaternion((float)RotationX, (float)RotationY, (float)RotationZ, (float)RotationW);

        public LocalizationMapModel(Guid id = default, string name = default, bool active = default, int lighting = default, int color = default, double positionX = default, double positionY = default, double positionZ = default, double rotationX = default, double rotationY = default, double rotationZ = default, double rotationW = default, List<double> points = default)
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
}