using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    public struct Material
    {
        public Vector3 Color;
        public float Specular;
        public bool Metallic;

        public Material(Vector3 color, float specular, bool metallic = false)
        {
            Color = color;
            Specular = specular;
            Metallic = metallic;
        }
    }
}