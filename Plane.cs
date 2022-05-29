using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Plane
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Material Material;

        public Plane(Vector3 position, Vector3 normal, Material material)
        {
            Position = position;
            Normal = normal;
            Material = material;
        }
        public const int SizeInFloats = 16;
    }
}