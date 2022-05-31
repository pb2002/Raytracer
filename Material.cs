using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    public struct Material
    {
        public Vector3 Color;
        public float Roughness;
        public bool Metallic;

        public Material(Vector3 color, float roughness, bool metallic = false)
        {
            Color = color;
            Roughness = roughness;
            Metallic = metallic;
        }
    }
}