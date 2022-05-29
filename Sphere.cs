using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Sphere
    {
        public Vector3 Position;
        public float Radius;
        public Material Material;

        public void Intersect(Ray ray, ref Intersection closest)
        {
            float r2 = Radius * Radius;
            Vector3 c = Position - ray.Origin;
            
            float t = Vector3.Dot(c, ray.Direction);
            Vector3 q = c - t * ray.Direction;
            float p2 = q.LengthSquared;

            if (p2 > r2) return;
            if (c.LengthSquared <= r2) t += (float)Math.Sqrt(r2 - p2); 
            else t -= (float)Math.Sqrt(r2 - p2);
            if (t < 0) return;
            
            Vector3 point = ray.Origin + ray.Direction * t;
            if (!(t <= closest.Dst)) return;
            
            closest.Dst = t;
            closest.Point = point;
        }

        public Sphere(Vector3 position, float radius, Material material)
        {
            Position = position;
            Radius = radius;
            Material = material;
        }

        public const int SizeInFloats = 12;
    }
}