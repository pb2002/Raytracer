using OpenTK;

namespace Template
{
    public class Intersection
    {
        public float dst;
        public Vector3 point;

        public Intersection(float dst, Vector3 point)
        {
            this.dst = dst;
            this.point = point;
        }
    }
}