using OpenTK;

namespace Template
{
    public static class AppSettings
    {
        public const int ViewportWidth = 1280;
        public const int ViewportHeight = ViewportWidth / 2;
        
        // these values control the SSBO size representing the scene.
        // these need to match the #define-statements in the fragment shader.
        public const int MaxPrimitives = 8192;
        public const int MaxLights = 256;
        
        // The size of the field in which the spheres are spawned
        public const float SpawnFieldSize = 50f;
        // The number of spheres to spawn
        public const int SphereCount = 64;

        // Number of reflection bounces
        public const int ReflectionBounces = 2;
        // Specular power scalar
        public const float SpecularPow = 1000f;
        // Sky color
        public static Vector3 SkyColor = new Vector3(0.15f, 0.25f, 0.5f);
        // Ambient light intensity (as a multiple of the sky color)
        public const float AmbientIntensity = 0.2f;
        // Shadow strength
        public const float ShadowStrength = 0.97f;
        // Camera movement speed
        public const float CameraSpeed = 30f;

        public static bool UseTonemapping = true;
        public static float ExposureBias = 3.0f;
        
        public const float DebugUnitScale = SpawnFieldSize * 1.5f;

        public const int PrimitiveBufferSize = MaxPrimitives * Sphere.SizeInFloats
                                               + MaxPrimitives * Plane.SizeInFloats
                                               + MaxLights * Light.SizeInFloats;
    }
}