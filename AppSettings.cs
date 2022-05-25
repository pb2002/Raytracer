namespace Template
{
    partial class MyApplication
    {
        // these values control the UBO size representing the scene.
        // these need to match the #define-statements in the fragment shader.
        // UBO size cannot exceed 16KB
        private const int primitiveCount = 256;
        private const int lightCount = 64;

        private const float spawnFieldSize = 150f;

        private const float cameraSpeed = 10f;

        // enable tonemapping
        private bool enableTonemapping = true;
        // tonemapper exposure bias
        private float exposureBias = 3.0f;
    }
}