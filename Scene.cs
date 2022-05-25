using System.Collections.Generic;
using OpenTK;

namespace Template
{
    public class Scene
    {
        public List<Sphere> spheres = new List<Sphere>();
        public List<Plane> planes = new List<Plane>();

        public Intersection Intersect(Ray r)
        {
            Intersection closest = new Intersection(float.PositiveInfinity, Vector3.Zero);
            foreach (var s in spheres)
            {
                s.Intersect(r, ref closest);
            }

            return closest;
        }
        public List<Light> lights = new List<Light>();
        
        
    }
}