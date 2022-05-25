using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Template
{
    public class Material
    {
        public Vector3 color;
        public float specular;

        public Material(Vector3 color, float specular, bool metallic)
        {
            this.color = color;
            this.specular = specular;
        }

        public void LoadIntoUniform(int program, string name)
        {
            int loc;
            loc = GL.GetUniformLocation(program, $"{name}.color");
            GL.ProgramUniform3(program, loc, color);
            loc = GL.GetUniformLocation(program, $"{name}.specular");
            GL.ProgramUniform1(program, loc, specular);
        }
    }
}