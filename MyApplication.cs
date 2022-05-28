using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using OpenTK;
using OpenTK.Input;
using System.Threading;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    partial class MyApplication
    {
        private Random rng = new Random();
        public Surface screen;
        
        private Scene scene;
        private Camera camera;

        private RaytracingShader shader; // shader program ids

        private bool tPressedLastFrame; // memory state for tonemap toggle
        private bool tabPressedLastFrame; // memory state for debug toggle
        
        private Stopwatch fpsCounter = new Stopwatch();
        private float frameTime = 1f;
        
        // ===========================================================================================================
        private Vector3 RandomColor()
        {
            Vector3 color = new Vector3((float) rng.NextDouble(), (float) rng.NextDouble(),
                (float) rng.NextDouble());

            // this code makes the random colors look a bit nicer
            color -= Vector3.One * Math.Min(color.X, Math.Min(color.Y, color.Z));
            color /= Math.Max(color.X, Math.Max(color.Y, color.Z));
            return color;
        }
        
        // create scene
        private void InitScene()
        {
            Console.WriteLine("Constructing Scene...");
            scene = new Scene();
            scene.planes.AddRange(new Plane[]
            {
                new Plane(new Vector3(0, 0, 0), Vector3.UnitY, new Material(new Vector3(0.7f, 0.7f, 0.7f), 0.1f))
            });
            Sphere[] spheres = new Sphere[AppSettings.SphereCount];
            int idx = 0;
            for (int i = 0; i < spheres.Length; i++)
            {
                float r = (float) Math.Pow(rng.NextDouble(), 4);
                float radius = 1 + r * 3;

                Vector3 pos = Vector3.Zero;

                // poisson disk sampling (kinda)
                bool valid = false;
                int attempts = 0;
                while (!valid && attempts < 50)
                {
                    pos = new Vector3(0.5f * AppSettings.SpawnFieldSize - (float) rng.NextDouble() * AppSettings.SpawnFieldSize, radius,
                        0.5f * AppSettings.SpawnFieldSize - (float) rng.NextDouble() * AppSettings.SpawnFieldSize);
                    valid = true;
                    if (spheres.Any(s => (s.position - pos).Length < radius + s.radius))
                    {
                        valid = false;
                        attempts++;
                    }
                }

                if (!valid) continue;
                Vector3 color = RandomColor();

                spheres[idx] = new Sphere(pos, radius, new Material(color, 1));
                idx++;
            };
            scene.spheres.AddRange(spheres);
            Console.WriteLine($"Spheres spawned: {idx}");
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = new Vector3(
                    0.5f * AppSettings.SpawnFieldSize - (float) rng.NextDouble() * AppSettings.SpawnFieldSize,
                    15, 0.5f * AppSettings.SpawnFieldSize - (float) rng.NextDouble() * AppSettings.SpawnFieldSize);
                Vector3 color = Vector3.Lerp(RandomColor(), Vector3.One, 0.4f);
                scene.lights.Add(new Light(pos, 0.1f, color, 1000f));
            }
            camera = new Camera(new Vector3(0, 3, -10), Vector3.UnitZ, 2, screen.width/2, screen.height);
        }
        
        // initialize OpenGL
        private void InitGL()
        {
            // load the shader --------------------------------------------------------------
            shader = new RaytracingShader("../../shaders/vs.glsl", "../../shaders/fs.glsl");
            // ------------------------------------------------------------------------------
            
            // set dynamic uniform values
            UpdateDynamicUniforms();
            
            // set the object count values
            shader.SetIntUniform("sphereCount", scene.spheres.Count);
            shader.SetIntUniform("planeCount", scene.planes.Count);
            shader.SetIntUniform("lightCount", scene.lights.Count);

            shader.SetIntUniform("reflectionBounces", AppSettings.ReflectionBounces);
            shader.SetFloatUniform("specularPow", AppSettings.SpecularPow);
            shader.SetVector3Uniform("skyColor", AppSettings.SkyColor);
            shader.SetFloatUniform("ambientIntensity", AppSettings.AmbientIntensity);
            shader.SetFloatUniform("shadowStrength", AppSettings.ShadowStrength);
            shader.SetVector2Uniform("resolution", new Vector2(AppSettings.ViewportWidth/2f, AppSettings.ViewportHeight));
            
            // create and write to SSBO
            shader.CreateSceneSSBO(scene.CreateDataBuffer());
        }
        
        // initialize
        public void Init()
        {
            InitScene();
            InitGL();
        }
        
        // update shader uniform values that change during runtime
        private void UpdateDynamicUniforms()
        {
            shader.SetCameraUniform(camera);
            shader.SetBoolUniform("useTonemapping", AppSettings.UseTonemapping);
            shader.SetFloatUniform("exposureBias", AppSettings.ExposureBias);
        }

        private void HandleInput()
        {
            var keyboardState = Keyboard.GetState();
            
            var camDistance = Vector3.Distance(camera.Position, camera.pivot);
            
            // camera controls -----------------------------------------------------------------------------------
            if (keyboardState.IsKeyDown(Key.E)) 
                camera.SetPosition(camera.Position + AppSettings.CameraSpeed * 0.03f * frameTime * camDistance * camera.Right);
            
            if (keyboardState.IsKeyDown(Key.Q)) 
                camera.SetPosition(camera.Position - AppSettings.CameraSpeed * 0.03f * frameTime * camDistance * camera.Right);

            if (keyboardState.IsKeyDown(Key.Z) && camDistance > 0.5f) 
                camera.SetPosition(camera.Position + AppSettings.CameraSpeed * frameTime * camera.ViewDirection);
            
            if (keyboardState.IsKeyDown(Key.X)) 
                camera.SetPosition(camera.Position - AppSettings.CameraSpeed * frameTime * camera.ViewDirection);

            var steepness = Vector3.Dot(camera.ViewDirection, -Vector3.UnitY);
            if (keyboardState.IsKeyDown(Key.R) && steepness < 0.98f) 
                camera.SetPosition(camera.Position + AppSettings.CameraSpeed * 0.03f * frameTime * camDistance * camera.Up);
            
            if (keyboardState.IsKeyDown(Key.F) && steepness > -0.95f) 
                camera.SetPosition(camera.Position - AppSettings.CameraSpeed * 0.03f * frameTime * camDistance * camera.Up);
            
            // flattened forward direction
            Vector3 forward = Vector3.Normalize(new Vector3(camera.ViewDirection.X, 0, camera.ViewDirection.Z));

            if (keyboardState.IsKeyDown(Key.W))
            {
                var delta = forward * AppSettings.CameraSpeed * frameTime;
                camera.pivot += delta;
                camera.SetPosition(camera.Position + delta);
            }

            if (keyboardState.IsKeyDown(Key.S))
            {
                var delta = forward * AppSettings.CameraSpeed * frameTime;
                camera.pivot -= delta;
                camera.SetPosition(camera.Position - delta);
            }

            if (keyboardState.IsKeyDown(Key.D))
            {
                var delta = camera.Right * AppSettings.CameraSpeed * frameTime;
                camera.pivot += delta;
                camera.SetPosition(camera.Position + delta);
            }

            if (keyboardState.IsKeyDown(Key.A))
            {
                var delta = camera.Right * AppSettings.CameraSpeed * frameTime;
                camera.pivot -= delta;
                camera.SetPosition(camera.Position - delta);
            }

            // look at pivot point
            camera.SetViewDirection(Vector3.Normalize(camera.pivot - camera.Position));
            // ---------------------------------------------------------------------------------------------------
            
            if (keyboardState.IsKeyDown(Key.Plus))
            {
                float fl = Math.Min(10, camera.FocalLength + 0.02f * camera.FocalLength);
                camera.SetFocalLength(fl);
            }

            if (keyboardState.IsKeyDown(Key.Minus))
            {
                float fl = Math.Max(0.25f, camera.FocalLength - 0.02f * camera.FocalLength);
                camera.SetFocalLength(fl);
            }

            if (keyboardState.IsKeyDown(Key.T))
            {
                if (!tPressedLastFrame)
                {
                    AppSettings.UseTonemapping = !AppSettings.UseTonemapping;
                    tPressedLastFrame = true;   
                }
            }
            else tPressedLastFrame = false;
            
            if (keyboardState.IsKeyDown(Key.Tab))
            {
                if (!tabPressedLastFrame)
                {
                    debugMode = !debugMode;
                    tabPressedLastFrame = true;   
                }
            }
            else tabPressedLastFrame = false;

            if (keyboardState.IsKeyDown(Key.LBracket))
            {
                AppSettings.ExposureBias = Math.Max(0.25f, AppSettings.ExposureBias - 0.05f);
            }

            if (keyboardState.IsKeyDown(Key.RBracket))
            {
                AppSettings.ExposureBias = Math.Min(10f, AppSettings.ExposureBias + 0.05f);
            }
        }
        
        // tick: renders one frame
        public void Tick()
        {
            if (fpsCounter.IsRunning)
            {
                fpsCounter.Stop();
                frameTime = (float) fpsCounter.Elapsed.TotalSeconds;
                fpsCounter.Restart();
            }
            else
            {
                fpsCounter.Start();
            }
            
            HandleInput();
            UpdateDynamicUniforms();

            screen.Clear(0);
            DrawDebug();
        }
        
        // because the shader needs to be selectively disabled in order to draw the debug view, the raytracing
        // draw call is done in a separate method.
        public void OnRender()
        {
            shader.Draw();
        }
    }
}