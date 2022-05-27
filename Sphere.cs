using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Material material;

        public void Intersect(Ray ray, ref Intersection closest)
        {
            float r2 = radius * radius;
            Vector3 c = position - ray.origin;
            
            float t = Vector3.Dot(c, ray.direction);
            Vector3 q = c - t * ray.direction;
            float p2 = q.LengthSquared;

            if (p2 > r2) return;
            if (c.LengthSquared <= r2) t += (float)Math.Sqrt(r2 - p2); 
            else t -= (float)Math.Sqrt(r2 - p2);
            if (t < 0) return;
            
            Vector3 point = ray.origin + ray.direction * t;
            if (!(t <= closest.dst)) return;
            
            closest.dst = t;
            closest.point = point;
        }

        public Sphere(Vector3 position, float radius, Material material)
        {
            this.position = position;
            this.radius = radius;
            this.material = material;
        }
        
        public static readonly int Size = BlittableValueType<Sphere>.Stride;
        
        public const int sizeInBytes = 4 * sizeInFloats;
        public const int sizeInFloats = 8;
    }
}