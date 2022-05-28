using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenTK;

namespace Template
{
    public class Scene
    {
        public List<Sphere> spheres = new List<Sphere>();
        public List<Plane> planes = new List<Plane>();
        public List<Light> lights = new List<Light>();

        public Intersection Intersect(Ray r)
        {
            Intersection closest = new Intersection(float.PositiveInfinity, Vector3.Zero);
            foreach (var s in spheres)
            {
                s.Intersect(r, ref closest);
            }

            return closest;
        }
        
        // Enjoy the hardcoded trainwreck below this comment.
        
        // I initially tried using the 'ref struct' overload of GL.BufferData(), so I wouldn't have to do it this way.
        // However, I cannot get C# to lay out my structs in the same way as they are read by the shader.
        // Google didn't help me out either, other than pointing out that I should not be doing this in C#.
        public float[] CreateDataBuffer()
        {
            Console.WriteLine("Populating scene data buffer...");
            // create a big chungus array
            float[] dataBuffer = new float[AppSettings.PrimitiveBufferSize];
            
            // the SSBO is laid out like this:
            // Sphere[primitiveCount]
            // Plane[primitiveCount]
            // Light[lightCount]

            // check if the object counts do not exceed their maximum values
            if (spheres.Count > AppSettings.MaxPrimitiveCount
                || planes.Count > AppSettings.MaxPrimitiveCount
                || lights.Count > AppSettings.MaxLightCount)
                throw new Exception("Buffer size too small");

            // then populate the big chungus array
            Parallel.For(0, spheres.Count, i =>
            {
                var s = spheres[i];
                var base_idx = i * Sphere.sizeInFloats;

                dataBuffer[base_idx + 0] = s.position.X;
                dataBuffer[base_idx + 1] = s.position.Y;
                dataBuffer[base_idx + 2] = s.position.Z;
                dataBuffer[base_idx + 3] = s.radius;
                dataBuffer[base_idx + 4] = s.material.color.X;
                dataBuffer[base_idx + 5] = s.material.color.Y;
                dataBuffer[base_idx + 6] = s.material.color.Z;
                dataBuffer[base_idx + 7] = s.material.specular;
            });

            int offset = AppSettings.MaxPrimitiveCount * Sphere.sizeInFloats;
            Parallel.For(0, planes.Count, i =>
            {
                var s = planes[i];
                var base_idx = i * Plane.sizeInFloats + offset;

                dataBuffer[base_idx + 0] = s.position.X;
                dataBuffer[base_idx + 1] = s.position.Y;
                dataBuffer[base_idx + 2] = s.position.Z;
                // because of 16 byte alignment rule, we skip base_idx + 3
                dataBuffer[base_idx + 4] = s.normal.X;
                dataBuffer[base_idx + 5] = s.normal.Y;
                dataBuffer[base_idx + 6] = s.normal.Z;
                // idem
                dataBuffer[base_idx + 8] = s.material.color.X;
                dataBuffer[base_idx + 9] = s.material.color.Y;
                dataBuffer[base_idx + 10] = s.material.color.Z;
                dataBuffer[base_idx + 11] = s.material.specular;
            });

            offset += AppSettings.MaxPrimitiveCount * Plane.sizeInFloats;
            Parallel.For(0, lights.Count, i =>
            {
                var l = lights[i];
                var base_idx = i * Light.sizeInFloats + offset;

                dataBuffer[base_idx + 0] = l.position.X;
                dataBuffer[base_idx + 1] = l.position.Y;
                dataBuffer[base_idx + 2] = l.position.Z;
                dataBuffer[base_idx + 3] = l.size;
                dataBuffer[base_idx + 4] = l.color.X;
                dataBuffer[base_idx + 5] = l.color.Y;
                dataBuffer[base_idx + 6] = l.color.Z;
                dataBuffer[base_idx + 7] = l.intensity;
            });

            // oh lawd he comin
            return dataBuffer;
        }
    }
}

