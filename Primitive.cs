namespace Template
{
    public abstract class Primitive
    {
        public Material mat;
        public abstract void Intersect(Ray ray, ref Intersection closest);
    }
}