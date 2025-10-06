using System;

[Serializable]
public class RigConfig
{
    [Serializable]
    public struct RigCamera
    {
        public string id;
        public string model;
        public int width;
        public int height;
        public float[] intrinsics;
        public bool ref_sensor;
        public float[] rotation; // w.r.t. rig
        public float[] translation; // w.r.t. rig
    }

    [Serializable]
    public class Rig
    {
        public string id;
        public RigCamera[] cameras;
    }

    public Rig[] rigs;
}