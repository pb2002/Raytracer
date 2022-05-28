using OpenTK;

namespace Template
{
    public static class AppSettings
    {
        public const int ViewportWidth = 1600;
        public const int ViewportHeight = ViewportWidth / 2;
        
        // these values control the SSBO size representing the scene.
        // these need to match the #define-statements in the fragment shader.
        public const int MaxPrimitiveCount = 8192;
        public const int MaxLightCount = 256;
        
        public const float SpawnFieldSize = 100f;
        public const int SphereCount = 128;

        public const int ReflectionBounces = 2;
        public const float SpecularPow = 250f;
        public static Vector3 SkyColor = new Vector3(0.15f, 0.25f, 0.5f);
        public const float AmbientIntensity = 0.1f;
        public const float ShadowStrength = 0.97f;

        public const float debugUnitScale = SpawnFieldSize * 1.5f;

        public const float CameraSpeed = 30f;

        public static bool UseTonemapping = true;
        public static float ExposureBias = 3.0f;
        public const int PrimitiveBufferSize = MaxPrimitiveCount * Sphere.sizeInFloats
                                               + MaxPrimitiveCount * Plane.sizeInFloats
                                               + MaxLightCount * Light.sizeInFloats;
    }
}