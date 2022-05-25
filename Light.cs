using OpenTK;

namespace Template
{
    public class Light
    {
        public const int sizeInBytes = 32;
        public const int sizeInFloats = 8;
        
        public Vector3 position;
        public Vector3 color;
        public float intensity;

        public Light(Vector3 position, float intensity, Vector3 color)
        {
            this.position = position;
            this.intensity = intensity;
            this.color = color;
        }
    }
}