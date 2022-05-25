using System;
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
        private const int primitiveBufferSize =
            primitiveCount * Sphere.sizeInFloats
            + primitiveCount * Plane.sizeInFloats
            + lightCount * Light.sizeInFloats;
        private const int primitiveBufferSizeBytes = primitiveBufferSize * 4;
        
        private Random rng = new Random();
        public Surface screen;
        
        private Scene scene;
        private Camera camera;
        private Vector3 focus = Vector3.Zero; // pivot point of the camera

        // image plane vertices
        private float[] verts =
        {
            0, 1, 0,
            0, -1, 0,
            1, 1, 0,
            1, -1, 0,
            1, 1, 0,
            0, -1, 0
        };
        
        private int vbo; // vbo id
        private int programID, vsID, fsID; // shader program ids
        
        // vpos attribute id
        private int attribute_vpos;

        private bool tPressedLastFrame; // memory state for tonemap toggle
        
        // ===========================================================================================================
        
        // create scene
        private void InitScene()
        {
            scene = new Scene();
            scene.planes.AddRange(new Plane[]
            {
                new Plane(new Vector3(0, 0, 0), Vector3.UnitY, new Material(new Vector3(0.7f, 0.7f, 0.7f), 0.1f, false))
            });
            
            
            // because of the UBO 16 KB limitation and my lack of understanding of SSBO's, 512 is the highest
            // number of spheres we can get working with this setup.
            for (int i = 0; i < primitiveCount; i++)
            {
                float r = (float) rng.NextDouble();
                float radius = 1 + r * r * 5;

                Vector3 pos = Vector3.Zero;
                
                // poisson disk sampling (kinda)
                bool valid = false;
                int attempts = 0;
                while (!valid && attempts < 50)
                {
                    pos = new Vector3(0.5f * spawnFieldSize - (float) rng.NextDouble() * spawnFieldSize, radius,
                        0.5f * spawnFieldSize - (float) rng.NextDouble() * spawnFieldSize);
                    valid = true;
                    if (scene.spheres.Any(s => (s.position - pos).Length < radius + s.radius))
                    {
                        valid = false;
                        attempts++;
                    }
                    
                }
                if (!valid) continue;
                
                Vector3 color = new Vector3((float) rng.NextDouble(), (float) rng.NextDouble(),
                    (float) rng.NextDouble());
                
                // this code makes the random colors look a bit nicer
                color.X *= color.X;
                color.Y *= color.Y;
                color.Z *= color.Z;
                color /= Math.Max(color.X, Math.Max(color.Y, color.Z));

                float specular = (float) rng.NextDouble();
                scene.spheres.Add(new Sphere(pos, radius, new Material(color, specular, true)));
            }
            scene.lights.AddRange(new[]
                {
                    new Light(new Vector3(-1000, 1500, -1500), 5000000, new Vector3(1, 0.94f, 0.3f)),
                }
            );
            camera = new Camera(new Vector3(0, 3, -10), Vector3.UnitZ, 2, screen.width/2, screen.height);
        }
        
        // set all uniform values
        // this code is executed at initialization
        private void SetUniforms()
        {
            UpdateDynamicUniforms();
            // check if the object counts do not exceed their maximum values
            if (scene.spheres.Count > primitiveCount 
                || scene.planes.Count > primitiveCount
                || scene.lights.Count > lightCount)
                throw new Exception("Primitive buffer size too small");

            int loc;
            
            // sources for the code below:
            // https://www.lighthouse3d.com/tutorials/glsl-tutorial/uniform-variables/
            #region Create uniform buffer
            // create a new UBO
            int ubo = GL.GenBuffer();
            
            // bind buffer
            GL.BindBufferBase(BufferTarget.UniformBuffer, 2, ubo);
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo);
            
            
            // create & fill data array
            float[] dataBuf = new float[primitiveBufferSize];
            
            // the UBO is laid out like this:
            // Sphere[primitives]
            // Plane[primitives]
            // Light[primitives]

            for (int i = 0; i < scene.spheres.Count; i++)
            {
                var s = scene.spheres[i];
                var base_idx = i * Sphere.sizeInFloats;
                
                dataBuf[base_idx + 0] = s.position.X;
                dataBuf[base_idx + 1] = s.position.Y;
                dataBuf[base_idx + 2] = s.position.Z;
                dataBuf[base_idx + 3] = s.radius;
                dataBuf[base_idx + 4] = s.mat.color.X;
                dataBuf[base_idx + 5] = s.mat.color.Y;
                dataBuf[base_idx + 6] = s.mat.color.Z;
                dataBuf[base_idx + 7] = s.mat.specular;
            }

            int offset = primitiveCount * Sphere.sizeInFloats;
            for (int i = 0; i < scene.planes.Count; i++)
            {
                var s = scene.planes[i];
                var base_idx = i * Plane.sizeInFloats + offset;
                
                dataBuf[base_idx + 0] = s.position.X;
                dataBuf[base_idx + 1] = s.position.Y;
                dataBuf[base_idx + 2] = s.position.Z;
                // because of 16 byte alignment rule, we skip base_idx + 3
                dataBuf[base_idx + 4] = s.normal.X;
                dataBuf[base_idx + 5] = s.normal.Y;
                dataBuf[base_idx + 6] = s.normal.Z;
                // idem
                dataBuf[base_idx + 8] = s.mat.color.X;
                dataBuf[base_idx + 9] = s.mat.color.Y;
                dataBuf[base_idx + 10] = s.mat.color.Z;
                dataBuf[base_idx + 11] = s.mat.specular;
            }

            offset += primitiveCount * Plane.sizeInFloats;
            for (int i = 0; i < scene.lights.Count; i++)
            {
                var l = scene.lights[i];
                var base_idx = i * Light.sizeInFloats + offset;
                
                dataBuf[base_idx + 0] = l.position.X;
                dataBuf[base_idx + 1] = l.position.Y;
                dataBuf[base_idx + 2] = l.position.Z;
                // 16 byte rule
                dataBuf[base_idx + 4] = l.color.X;
                dataBuf[base_idx + 5] = l.color.Y;
                dataBuf[base_idx + 6] = l.color.Z;
                dataBuf[base_idx + 7] = l.intensity;
            }
            // write the buffer data
            GL.BufferData(BufferTarget.UniformBuffer, dataBuf.Length * sizeof(float), dataBuf, BufferUsageHint.StaticDraw);
            // unbind the UBO
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            
            // set the object count values
            loc = GL.GetUniformLocation(programID, "sphereCount");
            GL.ProgramUniform1(programID, loc, scene.spheres.Count);
            loc = GL.GetUniformLocation(programID, "planeCount");
            GL.ProgramUniform1(programID, loc, scene.planes.Count);
            loc = GL.GetUniformLocation(programID, "lightCount");
            GL.ProgramUniform1(programID, loc, scene.lights.Count);
            #endregion
        }
        
        int LoadShader(string name, ShaderType type, int program)
        {
            // create the shader
            int id = GL.CreateShader(type);
            
            // load shader code from file
            using (StreamReader sr = new StreamReader(name))
            {
                GL.ShaderSource(id, sr.ReadToEnd());
            }
            
            // compile & attach
            GL.CompileShader(id);
            GL.AttachShader(program, id);
#if DEBUG
            Console.WriteLine(GL.GetShaderInfoLog(id));
#endif
            return id;
        }
        
        private void InitGL()
        {
            if (primitiveBufferSizeBytes > 16384) Console.WriteLine("UBO is bigger than 16KB!");
            
            programID = GL.CreateProgram();
            vsID = LoadShader("../../shaders/vs.glsl", ShaderType.VertexShader, programID);
            fsID = LoadShader("../../shaders/fs.glsl", ShaderType.FragmentShader, programID);
            GL.LinkProgram(programID);
  
            attribute_vpos = GL.GetAttribLocation(programID, "vPosition");
            SetUniforms();
            
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                verts.Length * 4,
                verts,
                BufferUsageHint.StaticDraw
            );
            
            GL.VertexAttribPointer(attribute_vpos, 3, VertexAttribPointerType.Float, false, 12, 0);    
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
            int loc;
            loc = GL.GetUniformLocation(programID, "camera.position");
            GL.ProgramUniform3(programID, loc, camera.Position);
            loc = GL.GetUniformLocation(programID, "camera.screenCenter");
            GL.ProgramUniform3(programID, loc, camera.ImagePlaneCenter);
            loc = GL.GetUniformLocation(programID, "camera.up");
            GL.ProgramUniform3(programID, loc, camera.Up);
            loc = GL.GetUniformLocation(programID, "camera.right");
            GL.ProgramUniform3(programID, loc, camera.Right);
            loc = GL.GetUniformLocation(programID, "camera.aspectRatio");
            GL.ProgramUniform1(programID, loc, camera.AspectRatio);
            
            loc = GL.GetUniformLocation(programID, "useTonemapping");
            GL.ProgramUniform1(programID, loc, enableTonemapping ? 1 : 0);
            loc = GL.GetUniformLocation(programID, "exposureBias");
            GL.ProgramUniform1(programID, loc, exposureBias);
        }

        void HandleInput()
        {
            var keyboardState = Keyboard.GetState();
            
            var camDistance = Vector3.Distance(camera.Position, focus);
            
            if (keyboardState.IsKeyDown(Key.E)) 
                camera.SetPosition(camera.Position + cameraSpeed * 0.1f * frameTime * camera.Right * camDistance);
            
            if (keyboardState.IsKeyDown(Key.Q)) 
                camera.SetPosition(camera.Position - cameraSpeed * 0.1f * frameTime * camera.Right * camDistance);

            if (keyboardState.IsKeyDown(Key.Z) && camDistance > 0.5f) 
                camera.SetPosition(camera.Position + cameraSpeed * frameTime * camera.ViewDirection);
            
            if (keyboardState.IsKeyDown(Key.X)) 
                camera.SetPosition(camera.Position - cameraSpeed * frameTime * camera.ViewDirection);

            var steepness = Vector3.Dot(camera.ViewDirection, -Vector3.UnitY);
            if (keyboardState.IsKeyDown(Key.R) && steepness < 0.9f) 
                camera.SetPosition(camera.Position + cameraSpeed * frameTime * camera.Up);
            
            if (keyboardState.IsKeyDown(Key.F) && steepness > 0.1f) 
                camera.SetPosition(camera.Position - cameraSpeed * frameTime * camera.Up);

            Vector3 forward = Vector3.Normalize(new Vector3(camera.ViewDirection.X, 0, camera.ViewDirection.Z));

            if (keyboardState.IsKeyDown(Key.W))
            {
                var delta = forward * cameraSpeed * frameTime;
                focus += delta;
                camera.SetPosition(camera.Position + delta);
            }

            if (keyboardState.IsKeyDown(Key.S))
            {
                var delta = forward * cameraSpeed * frameTime;
                focus -= delta;
                camera.SetPosition(camera.Position - delta);
            }

            if (keyboardState.IsKeyDown(Key.D))
            {
                var delta = camera.Right * cameraSpeed * frameTime;
                focus += delta;
                camera.SetPosition(camera.Position + delta);
            }

            if (keyboardState.IsKeyDown(Key.A))
            {
                var delta = camera.Right * cameraSpeed * frameTime;
                focus -= delta;
                camera.SetPosition(camera.Position - delta);
            }

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
                    enableTonemapping = !enableTonemapping;
                    tPressedLastFrame = true;   
                }
            }
            else tPressedLastFrame = false;

            if (keyboardState.IsKeyDown(Key.LBracket))
            {
                exposureBias = Math.Max(0.25f, exposureBias - 0.05f);
            }

            if (keyboardState.IsKeyDown(Key.RBracket))
            {
                exposureBias = Math.Min(5f, exposureBias + 0.05f);
            }
        }
        
        // tick: renders one frame
        public void Tick()
        {
            HandleInput();

            camera.SetViewDirection(Vector3.Normalize(focus - camera.Position));

            UpdateDynamicUniforms();

            screen.Clear(0);
            Debug();
        }
        
        // because the shader needs to be selectively disabled in order to draw the debug view, the raytracing
        // draw call is done in a separate method.
        public void OnRender()
        {
            GL.UseProgram(programID);
            
            GL.EnableVertexAttribArray(attribute_vpos);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
    }
}