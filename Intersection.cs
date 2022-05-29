using OpenTK;

namespace Template
{
    public struct Intersection
    {
        public float Dst;
        public Vector3 Point;

        public Intersection(float dst, Vector3 point)
        {
            Dst = dst;
            Point = point;
        }
    }
}