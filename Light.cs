using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Light
    {
        public Vector3 position;
        public float size;
        public Vector3 color;
        public float intensity;
        

        public Light(Vector3 position, float size, Vector3 color, float intensity)
        {
            this.position = position;
            this.size = size;
            this.color = color;
            this.intensity = intensity;
        }
        
        public const int sizeInBytes = 4 * sizeInFloats;
        public const int sizeInFloats = 8;
    }
}