using OpenTK;

namespace Template
{
    partial class MyApplication
    {
        public const int viewportWidth = 1024;

        public const int viewportHeight = 512;
        
        // these values control the SSBO size representing the scene.
        // these need to match the #define-statements in the fragment shader.
        public const int primitiveCount = 8192;
        public const int lightCount = 256;

        public const float spawnFieldSize = 350f;
        public const int sphereCount = 5000;

        public const float cameraSpeed = 30f;

        // tonemapping
        public bool useTonemapping = true;
        // tonemapping exposure bias
        public float exposureBias = 3.0f;

        // reflection bounces
        public int reflectionBounces = 2;
        // specular power
        public float specularPow = 250f;
        // sky color
        public Vector3 skyColor = new Vector3(0.3f, 0.8f, 1.0f);
        // ambient light intensity
        public float ambientIntensity = 0.05f;
        // shadow strength
        public float shadowStrength = 0.95f;
        
        public const int primitiveBufferSize =
            primitiveCount * Sphere.sizeInFloats
            + primitiveCount * Plane.sizeInFloats
            + lightCount * Light.sizeInFloats;
    }
}