using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    public struct Material
    {
        public Vector3 color;
        public float specular;

        public Material(Vector3 color, float specular)
        {
            this.color = color;
            this.specular = specular;
        }
    }
}