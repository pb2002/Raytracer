using System;
using System.Runtime.InteropServices;
using OpenTK;

namespace Template
{
    public struct Plane
    {
        public Vector3 position;
        public Vector3 normal;
        public Material material;

        public const float EPSILON = 0.00001f;
        public void Intersect(Ray ray, ref Intersection closest)
        {
            float denom = Vector3.Dot(ray.direction, normal);
            if (Math.Abs(denom) <= EPSILON) return;
            
            float t = Vector3.Dot(position - ray.origin, normal) / denom;
            if (t < 0 || t > closest.dst) return;

            closest.dst = t;
            closest.point = ray.origin + t * ray.direction;
        }

        public Plane(Vector3 position, Vector3 normal, Material material)
        {
            this.position = position;
            this.normal = normal;
            this.material = material;
        }
        public const int sizeInBytes = 4 * sizeInFloats;
        public const int sizeInFloats = 12;
    }
}