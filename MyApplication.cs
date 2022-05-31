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
        public Surface Screen;
        
        private Scene _scene;
        private Camera _camera;

        private RaytracingShader _shader; // shader program ids

        private bool _tPressedLastFrame; // memory state for tonemap toggle
        private bool _tabPressedLastFrame; // memory state for debug toggle
        private bool _spacePressedLastFrame; // memory state for fast mode toggle
        
        private Stopwatch _fpsCounter = new Stopwatch();
        private float _frameTime = 1f;
        
        public bool Moved;
        // ===========================================================================================================

        // create scene
        private void InitScene()
        {
            Console.WriteLine("Constructing Scene...");
            _scene = new Scene();
            _scene.Planes.AddRange(new Plane[]
            {
                new Plane(new Vector3(0, 0, 0), Vector3.UnitY, new Material(new Vector3(0.3f, 0.6f, 0.7f), 1))
            });
            Sphere[] spheres = new Sphere[AppSettings.SphereCount];
            int idx = 0;
            for (int i = 0; i < spheres.Length; i++)
            {
                float r = (float) Math.Pow(Utils.Rng.NextDouble(), 4);
                float radius = 1 + r * 3;

                Vector3 pos = Vector3.Zero;

                // poisson disk sampling (kinda)
                bool valid = false;
                int attempts = 0;
                while (!valid && attempts < 50)
                {
                    pos = new Vector3(0.5f * AppSettings.SpawnFieldSize - (float) Utils.Rng.NextDouble() * AppSettings.SpawnFieldSize, radius,
                        0.5f * AppSettings.SpawnFieldSize - (float) Utils.Rng.NextDouble() * AppSettings.SpawnFieldSize);
                    valid = true;
                    if (spheres.Any(s => (s.Position - pos).Length < radius + s.Radius))
                    {
                        valid = false;
                        attempts++;
                    }
                }

                if (!valid) continue;
                Vector3 color = Utils.RandomColor();
                bool metallic = Utils.Rng.NextDouble() > 0.8;

                spheres[idx] = new Sphere(pos, radius, new Material(metallic ? new Vector3(0.7f, 0.7f, 0.7f) : color, (float)Utils.Rng.NextDouble(), metallic));
                idx++;
            };
            _scene.Spheres.AddRange(spheres);
            Console.WriteLine($"Spheres spawned: {idx}");
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(
                    0.5f * AppSettings.SpawnFieldSize - (float) Utils.Rng.NextDouble() * AppSettings.SpawnFieldSize,
                    40, 0.5f * AppSettings.SpawnFieldSize - (float) Utils.Rng.NextDouble() * AppSettings.SpawnFieldSize);
                Vector3 color = Vector3.Lerp(Utils.RandomColor(), Vector3.One, 0.2f);
                _scene.Lights.Add(new Light(pos, 4f, color, 6000f));
            }
            _camera = new Camera(new Vector3(0, 3, -10), Vector3.UnitZ, 2, Screen.Width/2, Screen.Height);
        }
        
        // initialize OpenGL
        private void InitGL()
        {
            // load the shader --------------------------------------------------------------
            _shader = new RaytracingShader("../../shaders/vs.glsl", "../../shaders/fs.glsl");
            // ------------------------------------------------------------------------------
            
            // set dynamic uniform values
            UpdateDynamicUniforms();
            
            // set the object count values
            _shader.SetIntUniform("sphereCount", _scene.Spheres.Count);
            _shader.SetIntUniform("planeCount", _scene.Planes.Count);
            _shader.SetIntUniform("lightCount", _scene.Lights.Count);
            
            _shader.SetIntUniform("reflectionBounces", AppSettings.ReflectionBounces);
            _shader.SetFloatUniform("specularPow", AppSettings.SpecularPow);
            _shader.SetVector3Uniform("skyColor", AppSettings.SkyColor);
            _shader.SetFloatUniform("ambientIntensity", AppSettings.AmbientIntensity);
            _shader.SetFloatUniform("shadowStrength", AppSettings.ShadowStrength);
            _shader.SetVector2Uniform("resolution", new Vector2(AppSettings.ViewportWidth/2f, AppSettings.ViewportHeight));
            
            // create and write to SSBO
            _shader.CreateSceneSSBO(_scene.CreateDataBuffer());
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
            _shader.SetCameraUniform(_camera);
            _shader.SetVector2Uniform("tnoise", new Vector2((float)Utils.Rng.NextDouble(), (float)Utils.Rng.NextDouble()));
            _shader.SetBoolUniform("useTonemapping", AppSettings.UseTonemapping);
            _shader.SetFloatUniform("exposureBias", AppSettings.ExposureBias);
            _shader.SetBoolUniform("fastMode", AppSettings.FastMode);
        }

        private void HandleInput()
        {
            var keyboardState = Keyboard.GetState();
            Moved = false;
            // camera controls -----------------------------------------------------------------------------------------
            
            // flattened forward direction
            Vector3 forward = Vector3.Normalize(new Vector3(_camera.ViewDirection.X, 0, _camera.ViewDirection.Z));
            // camera-pivot distance
            float camDistance = Vector3.Distance(_camera.Position, _camera.Pivot);
            // steepness of the view direction
            float steepness = Vector3.Dot(_camera.ViewDirection, -Vector3.UnitY);
            
            // rotation & zoom
            if (keyboardState.IsKeyDown(Key.E))
            {
                _camera.SetPosition(_camera.Position + AppSettings.CameraSpeed * 0.03f * _frameTime * camDistance * _camera.Right);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.Q))
            {
                _camera.SetPosition(_camera.Position - AppSettings.CameraSpeed * 0.03f * _frameTime * camDistance * _camera.Right);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.Z) && camDistance > 0.5f)
            {
                _camera.SetPosition(_camera.Position + AppSettings.CameraSpeed * _frameTime * _camera.ViewDirection);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.X))
            {
                _camera.SetPosition(_camera.Position - AppSettings.CameraSpeed * _frameTime * _camera.ViewDirection);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.R) && steepness < 0.98f)
            {
                _camera.SetPosition(_camera.Position + AppSettings.CameraSpeed * 0.03f * _frameTime * camDistance * _camera.Up);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.F) && steepness > -0.95f)
            {
                _camera.SetPosition(_camera.Position - AppSettings.CameraSpeed * 0.03f * _frameTime * camDistance * _camera.Up);
                Moved = true;
            }

            // movement
            if (keyboardState.IsKeyDown(Key.W))
            {
                var delta = forward * AppSettings.CameraSpeed * _frameTime;
                _camera.Pivot += delta;
                _camera.SetPosition(_camera.Position + delta);
                Moved = true;
            }
            if (keyboardState.IsKeyDown(Key.S))
            {
                var delta = forward * AppSettings.CameraSpeed * _frameTime;
                _camera.Pivot -= delta;
                _camera.SetPosition(_camera.Position - delta);
                Moved = true;
            }
            if (keyboardState.IsKeyDown(Key.A))
            {
                var delta = _camera.Right * AppSettings.CameraSpeed * _frameTime;
                _camera.Pivot -= delta;
                _camera.SetPosition(_camera.Position - delta);
                Moved = true;
            }
            if (keyboardState.IsKeyDown(Key.D))
            {
                var delta = _camera.Right * AppSettings.CameraSpeed * _frameTime;
                _camera.Pivot += delta;
                _camera.SetPosition(_camera.Position + delta);
                Moved = true;
            }
            
            // fov
            if (keyboardState.IsKeyDown(Key.Plus))
            {
                float fl = Math.Min(10, _camera.FocalLength + 0.02f * _camera.FocalLength);
                _camera.SetFocalLength(fl);
                Moved = true;
            }
            if (keyboardState.IsKeyDown(Key.Minus))
            {
                float fl = Math.Max(0.25f, _camera.FocalLength - 0.02f * _camera.FocalLength);
                _camera.SetFocalLength(fl);
                Moved = true;
            }
            
            // look at the pivot point
            _camera.SetViewDirection(Vector3.Normalize(_camera.Pivot - _camera.Position));
            
            // ---------------------------------------------------------------------------------------------------------
            
            // tonemapping toggle --------------------------------------------------------------------------------------
            if (keyboardState.IsKeyDown(Key.T))
            {
                if (!_tPressedLastFrame)
                {
                    AppSettings.UseTonemapping = !AppSettings.UseTonemapping;
                    _tPressedLastFrame = true;
                    Moved = true;
                }
            }
            else _tPressedLastFrame = false;
            // ---------------------------------------------------------------------------------------------------------
            
            // debug mode toggle ---------------------------------------------------------------------------------------
            if (keyboardState.IsKeyDown(Key.Tab))
            {
                if (!_tabPressedLastFrame)
                {
                    _debugMode = !_debugMode;
                    _tabPressedLastFrame = true;   
                }
            }
            else _tabPressedLastFrame = false;
            // ---------------------------------------------------------------------------------------------------------
            
            if (keyboardState.IsKeyDown(Key.Space))
            {
                if (!_spacePressedLastFrame)
                {
                    AppSettings.FastMode = !AppSettings.FastMode;
                    _spacePressedLastFrame = true;  
                    Moved = true;
                }
            }
            else _spacePressedLastFrame = false;
            
            // exposure bias -------------------------------------------------------------------------------------------
            if (keyboardState.IsKeyDown(Key.LBracket))
            {
                AppSettings.ExposureBias = Math.Max(0.25f, AppSettings.ExposureBias - 0.05f);
                Moved = true;
            }

            if (keyboardState.IsKeyDown(Key.RBracket))
            {
                AppSettings.ExposureBias = Math.Min(10f, AppSettings.ExposureBias + 0.05f);
                Moved = true;
            }
        }
        
        // tick: renders one frame
        public void Tick()
        {
            // fps counter
            if (_fpsCounter.IsRunning)
            {
                _fpsCounter.Stop();
                _frameTime = (float) _fpsCounter.Elapsed.TotalSeconds;
                _fpsCounter.Restart();
            }
            else
            {
                _fpsCounter.Start();
            }
            
            HandleInput();
            UpdateDynamicUniforms();

            Screen.Clear(0);
            DrawDebug();
        }
        
        // because the shader needs to be selectively disabled in order to draw the debug view, the raytracing
        // draw call is done in a separate method.
        public void OnRender()
        {
            _shader.Draw();
        }
    }
}