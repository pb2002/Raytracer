using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Light
    {
        public Vector3 Position;
        public float Size;
        public Vector3 Color;
        public float Intensity;
        

        public Light(Vector3 position, float size, Vector3 color, float intensity)
        {
            Position = position;
            Size = size;
            Color = color;
            Intensity = intensity;
        }
        
        public const int SizeInFloats = 8;
    }
}