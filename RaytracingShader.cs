using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            LoadTexture("../../assets/marble.png", TextureUnit.Texture1);
            LoadTexture("../../assets/skybox.jpeg", TextureUnit.Texture2);
            SetIntUniform("texture1", 1);
            SetIntUniform("texture2", 2);
            CreateVBO();
        }

        // https://opentk.net/learn/chapter1/5-textures.html
        public void LoadTexture(string path, TextureUnit unit)
        {
            Console.WriteLine($"Loading image '{path}'...");
            Image<Rgba32> image = Image.Load<Rgba32>(path);

            // ImageSharp loads from the top-left pixel, whereas OpenGL loads from the bottom-left, causing the texture to be flipped vertically.
            // This will correct that, making the texture display properly.
            image.Mutate(x => x.Flip(FlipMode.Vertical));

            // Convert ImageSharp's format into a byte array, so we can use it with OpenGL.
            var pixels = new byte[4 * image.Width * image.Height];
            Console.WriteLine("    - Copying into byte array...");

            Parallel.For(0, image.Height, y => {
                for (int x = 0; x < image.Width; x++)
                {
                    int base_idx = 4 * (x + y * image.Width);
                    pixels[base_idx] = image[x,y].R;
                    pixels[base_idx + 1] = image[x,y].G;
                    pixels[base_idx + 2] = image[x,y].B;
                    pixels[base_idx + 3] = image[x,y].A;
                }
            });
            
            Console.WriteLine("    - Binding...");
            int id = GL.GenTexture();
            GL.ActiveTexture(unit);
            GL.BindTexture( TextureTarget.Texture2D, id );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear );
            GL.TexParameter( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear );
            GL.TexImage2D(
                TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.Rgba, 
                image.Width, 
                image.Height, 
                0, 
                PixelFormat.Rgba, 
                PixelType.UnsignedByte, 
                pixels
            );
            Console.WriteLine("    - Generating mipmaps...");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private void CreateVBO()
        {
            Console.WriteLine("Creating VBO...");
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
            Console.WriteLine("Creating SSBO...");
            // https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object
            // create a new SSBO
            _ssbo = GL.GenBuffer();
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            
            // write the buffer data
            Console.WriteLine("    - Writing data to buffer...");
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
        public void SetVector2Uniform(string name, Vector2 value)
        {
            int loc = GL.GetUniformLocation(ProgramID, name);
            GL.ProgramUniform2(ProgramID, loc, value);
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
            Console.WriteLine($"Loading shader '{name}'...");
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