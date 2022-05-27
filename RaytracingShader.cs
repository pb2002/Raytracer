using System;
using System.IO;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace Template
{
    public class RaytracingShader
    {
        public int ProgramID { get; private set; }
        public int VertShaderID { get; private set; }
        public int FragShaderID { get; private set; }

        
        // vertices of the image plane
        private float[] _verts = 
        {
            0, 1, 0,
            0, -1, 0,
            1, 1, 0,
            1, -1, 0,
            1, 1, 0,
            0, -1, 0
        };

        private int _vPositionAttribute;
        private int _vbo;
        private int _ssbo;

        public RaytracingShader(string vPath, string fPath)
        {
            ProgramID = GL.CreateProgram();
            VertShaderID = LoadShader(vPath, ShaderType.VertexShader, ProgramID);
            FragShaderID = LoadShader(fPath, ShaderType.FragmentShader, ProgramID);
            GL.LinkProgram(ProgramID);
            
            _vPositionAttribute = GL.GetAttribLocation(ProgramID, "vPosition");
            
            CreateVBO();
        }

        private void CreateVBO()
        {
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                _verts.Length * 4,
                _verts,
                BufferUsageHint.StaticDraw
            );
            
            GL.VertexAttribPointer(_vPositionAttribute, 3, VertexAttribPointerType.Float, false, 12, 0);
        }
        
        public void CreateSceneSSBO(float[] data)
        {
            // https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object
            // create a new SSBO
            _ssbo = GL.GenBuffer();
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            
            // write the buffer data
            GL.BufferData(BufferTarget.ShaderStorageBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _ssbo);
            
            // unbind the SSBO
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        public void Draw()
        {
            GL.UseProgram(ProgramID);
            GL.EnableVertexAttribArray(_vPositionAttribute);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        
        // sources for the code below:
        // https://www.lighthouse3d.com/tutorials/glsl-tutorial/uniform-variables/
        public void SetFloatUniform(string name, float value)
        {
            int loc = GL.GetUniformLocation(ProgramID, name);
            GL.ProgramUniform1(ProgramID, loc, value);   
        }

        public void SetIntUniform(string name, int value)
        {
            int loc = GL.GetUniformLocation(ProgramID, name);
            GL.ProgramUniform1(ProgramID, loc, value);   
        }

        public void SetBoolUniform(string name, bool value)
        {
            int loc = GL.GetUniformLocation(ProgramID, name);
            GL.ProgramUniform1(ProgramID, loc, value ? 1 : 0);
        }

        public void SetVector3Uniform(string name, Vector3 value)
        {
            int loc = GL.GetUniformLocation(ProgramID, name);
            GL.ProgramUniform3(ProgramID, loc, value);
        }

        public void SetCameraUniform(Camera camera)
        {
            SetVector3Uniform("camera.position", camera.Position);
            SetVector3Uniform("camera.screenCenter", camera.ImagePlaneCenter);
            SetVector3Uniform("camera.up", camera.Up);
            SetVector3Uniform("camera.right", camera.Right);
            SetFloatUniform("camera.aspectRatio", camera.AspectRatio);
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
    }
}