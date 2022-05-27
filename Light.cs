using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Light
    {
        public Vector3 position;
        public Vector3 color;
        public float intensity;

        public Light(Vector3 position, float intensity, Vector3 color)
        {
            this.position = position;
            this.intensity = intensity;
            this.color = color;
        }
        
        public const int sizeInBytes = 4 * sizeInFloats;
        public const int sizeInFloats = 8;
    }
}