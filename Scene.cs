using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenTK;

namespace Template
{
    public class Scene
    {
        public List<Sphere> Spheres = new List<Sphere>();
        public List<Plane> Planes = new List<Plane>();
        public List<Light> Lights = new List<Light>();

        public Intersection Intersect(Ray r)
        {
            Intersection closest = new Intersection(float.PositiveInfinity, Vector3.Zero);
            foreach (var s in Spheres)
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
            if (Spheres.Count > AppSettings.MaxPrimitives
                || Planes.Count > AppSettings.MaxPrimitives
                || Lights.Count > AppSettings.MaxLights)
                throw new Exception("Buffer size too small");

            // then populate the big chungus array
            Parallel.For(0, Spheres.Count, i =>
            {
                var s = Spheres[i];
                var baseIdx = i * Sphere.SizeInFloats;

                dataBuffer[baseIdx + 0] = s.Position.X;
                dataBuffer[baseIdx + 1] = s.Position.Y;
                dataBuffer[baseIdx + 2] = s.Position.Z;
                dataBuffer[baseIdx + 3] = s.Radius;
                dataBuffer[baseIdx + 4] = s.Material.Color.X;
                dataBuffer[baseIdx + 5] = s.Material.Color.Y;
                dataBuffer[baseIdx + 6] = s.Material.Color.Z;
                dataBuffer[baseIdx + 7] = s.Material.Roughness;
                dataBuffer[baseIdx + 8] = s.Material.Metallic ? 1 : 0;
            });

            int offset = AppSettings.MaxPrimitives * Sphere.SizeInFloats;
            Parallel.For(0, Planes.Count, i =>
            {
                var s = Planes[i];
                var baseIdx = i * Plane.SizeInFloats + offset;

                dataBuffer[baseIdx + 0] = s.Position.X;
                dataBuffer[baseIdx + 1] = s.Position.Y;
                dataBuffer[baseIdx + 2] = s.Position.Z;
                // because of 16 byte alignment rule, we skip base_idx + 3
                dataBuffer[baseIdx + 4] = s.Normal.X;
                dataBuffer[baseIdx + 5] = s.Normal.Y;
                dataBuffer[baseIdx + 6] = s.Normal.Z;
                // idem
                dataBuffer[baseIdx + 8] = s.Material.Color.X;
                dataBuffer[baseIdx + 9] = s.Material.Color.Y;
                dataBuffer[baseIdx + 10] = s.Material.Color.Z;
                dataBuffer[baseIdx + 11] = s.Material.Roughness;
                dataBuffer[baseIdx + 12] = s.Material.Metallic ? 1 : 0;
            });

            offset += AppSettings.MaxPrimitives * Plane.SizeInFloats;
            Parallel.For(0, Lights.Count, i =>
            {
                var l = Lights[i];
                var baseIdx = i * Light.SizeInFloats + offset;

                dataBuffer[baseIdx + 0] = l.Position.X;
                dataBuffer[baseIdx + 1] = l.Position.Y;
                dataBuffer[baseIdx + 2] = l.Position.Z;
                dataBuffer[baseIdx + 3] = l.Size;
                dataBuffer[baseIdx + 4] = l.Color.X;
                dataBuffer[baseIdx + 5] = l.Color.Y;
                dataBuffer[baseIdx + 6] = l.Color.Z;
                dataBuffer[baseIdx + 7] = l.Intensity;
            });

            // oh lawd he comin
            return dataBuffer;
        }
    }
}

