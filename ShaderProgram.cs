using System.IO;
using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace GLLib
{
    class ShaderProgram
    {
        public enum Source
        {
            FILE,
            STATIC
        }

        public ShaderProgram()
        {
            _vertexShader = 0;
            _fragmentShader = 0;
            _program = 0;
        }

        public ShaderProgram(string vert, string frag, Source source) : this()
        {
            if (source == Source.FILE)  LoadFromFile(vert, frag);
            else                        LoadFromSource(vert, frag);
        }

        public void LoadFromFile(in string vertexPath, in string fragmentPath)
        {
            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            LoadFromSource(vertexSource, fragmentSource);
        }

        public void LoadFromSource(in string vertSource, in string fragSource)
        {
            // GLSL code
            string vertexSource = vertSource, fragmentSource = fragSource;

            // creation
            _vertexShader = GL.CreateShader(ShaderType.VertexShader);
            _fragmentShader = GL.CreateShader(ShaderType.VertexShader);

            // binding
            GL.ShaderSource(_vertexShader, vertexSource);
            GL.ShaderSource(_fragmentShader, fragmentSource);

            int compileSuccess = (int)All.False;

            GL.CompileShader(_vertexShader);
            GL.GetShader(_vertexShader, ShaderParameter.CompileStatus, out compileSuccess);
            if (compileSuccess != (int)All.True)
            {
                string infoLog = GL.GetShaderInfoLog(_vertexShader);
                throw new Exception($"Vertex shader compilation error! (Error message: {infoLog})");
            }

            GL.CompileShader(_fragmentShader);
            GL.GetShader(_fragmentShader, ShaderParameter.CompileStatus, out compileSuccess);
            if (compileSuccess != (int)All.True)
            {
                string infoLog = GL.GetShaderInfoLog(_fragmentShader);
                throw new Exception($"Fragment shader compilation error! (Error message: {infoLog})");
            }

            _program = GL.CreateProgram();
            GL.AttachShader(_program, _vertexShader);
            GL.AttachShader(_program, _fragmentShader);

            GL.LinkProgram(_program);
            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out compileSuccess);
            if (compileSuccess != (int)All.True)
            {
                string errorMessage = GL.GetProgramInfoLog(_program);
                throw new Exception($"Shader Program link error! (Error message: {errorMessage})");
            }
        }

        public void ActivateProgram()
        {
            GL.UseProgram(_program);
        }

        public void DeactivateProgram()
        {
            GL.UseProgram(0);
        }

        public void DeleteProgram()
        {
            GL.DetachShader(_program, _vertexShader);
            GL.DetachShader(_program, _fragmentShader);

            GL.DeleteProgram(_program);
        }

        private int _vertexShader;
        private int _fragmentShader;
        private int _program;
    }
}
