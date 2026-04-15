using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Platform.Windows;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace GLLib
{
    class GLView : GameWindow
    {
        public GLView(int width, int height, string title)
            : base(
                    width,
                    height,
                    GraphicsMode.Default,
                    title,
                    GameWindowFlags.FixedWindow,
                    DisplayDevice.Default,
                    2, 0, // OpenGL v2.0
                    GraphicsContextFlags.Default
                  )
        {
            log($"Running project [");
            log($"GL Version: \t{GL.GetString(StringName.Version)}");
            log($"GL Vendor: \t{GL.GetString(StringName.Vendor)}");
            log($"GL Renderer: \t{GL.GetString(StringName.Renderer)}");
            log($"GLSL Version: \t{GL.GetString(StringName.ShadingLanguageVersion)}");
            log($"GL Extensions: \t{GL.GetString(StringName.Extensions)}");
            log($"  ]");

            _frameTime = 0f;
            _frameCounter = 0;
            _fps = 0;
            _deltaTime = 0f;

            VSync = VSyncMode.Off;
            WindowBorder = WindowBorder.Resizable;
        }

        public static void log(in string message)
        {
            Console.WriteLine(message);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GL.ClearColor(1f, 0.4f, 0.4f, 1f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _dlId = CreateDL();
            InitVBO();
            InitVAO();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }

        protected void updateTime(float delta) // Delta + FPS counting
        {
            _deltaTime = (float)(delta);
            _frameTime += _deltaTime;
            _frameCounter++;
            if (_frameTime >= 1f)
            {
                _fps = _frameCounter;
                _frameTime = 0f;
                _frameCounter = 0;
            }
        }

        protected void processKeyboard()
        {
            KeyboardState key = Keyboard.GetState();
            if (key.IsKeyDown(Key.Escape))
            {
                Close();
            }
        }

        protected void updateWindowTitle(in string baseName)
        {
            Title = $"{baseName} [DEBUG]: (DeltaTime: {_deltaTime}, FPS: {_fps})";
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            updateTime((float)(e.Time));
            processKeyboard();
            updateWindowTitle("GLView");
        }
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.LineWidth(6f);
            GL.PushMatrix();

            DrawDL(_dlId);
            DrawVA(_vertices, _verticesColors);
            DrawVBO(_vertsVBOId, _colorVBOId, _vboVertsCount);
            DrawVAO(_vaoId, _vaoVertsCount);

            GL.PopMatrix();
            SwapBuffers();
            base.OnRenderFrame(e);
        }
        protected override void OnUnload(EventArgs e)
        {
            base.OnUnload(e);

            DeleteDL(_dlId);
            DeleteVBO(_colorVBOId);
            DeleteVBO(_vertsVBOId);
            DeleteVAO(_vaoId);
        }


        // --- Display Lists --- 
        /*
        стандартный способ оптимизировать статические отрисовки примитивов сократив время на передачу вершин и цветов и прочих
        данных каждый вызов - создать Display List который помещает эти данные в видеопамять и просто вызывать отрисовку DL
        вместо самой стандартной отрисовки glBegin(), glEnd() где значения вершин и т.д. передаются в видеокарту каждый вызов отрисовки  
        
        - использовать для статической геометрии
        - ВАЖНО выгружать неиспользуемые DL, т.к. видеопамять очень ограничена.
        - DL нельзя поменять после создания, любое изменение геометрии = пересоздание списка (дорого и неудобно)
         */
        private int CreateDL()
        {
            int index = GL.GenLists(1);
            GL.NewList(index, ListMode.Compile);

            float radius = 0.3f;
            int segments = 10;
            float rotation = 0f;
            float rotationRadians = rotation * (float)Math.PI / 180f;
            float segmentDegrees = 360f / segments;
            float segmentRadians = segmentDegrees * (float)Math.PI / 180f;

            GL.Begin(PrimitiveType.Polygon);

            GL.Color3(1f, 0f, 0f);
            GL.Vertex2(0f, 0f);

            GL.Color3(1f, 1f, 0f);
            for (int i = 0; i <= segments; i++)
            {
                float x = (float)Math.Cos((double)rotationRadians + ((double)segmentRadians * i)) * radius;
                float y = (float)Math.Sin((double)rotationRadians + ((double)segmentRadians * i)) * radius;

                GL.Vertex2(x, y);
            }

            GL.End();
            GL.EndList();

            return index;
        }

        private void DrawDL(int DLIndex)
        {
            GL.CallList(DLIndex);
        }

        private void DeleteDL(int DLIndex)
        {
            GL.DeleteLists(DLIndex, 1);
        }
        // ---

        // --- VertexArray (массив вершин) (OpenGL 1.1+)
        /*
        массив вершин просто передаёт значения видеокарте каждый вызов отрисовки DrawVA, видеопамять не используется
        почти то же самое что и стандартная отрисовка примитивов с помощью glBegin(); glEnd();

        - использовать для динамической геометрии
        */
        private void DrawVA(float[] vertices, float[] colors)
        {
            // GL.EnableClientState можно указать как перед так и после указания VertexPointer
            // главное ПЕРЕД отрисовкой VA
            GL.EnableClientState(ArrayCap.VertexArray); // <- Активировать возможность работы с массивом вершин
            GL.VertexPointer(3, VertexPointerType.Float, 0, vertices);

            GL.EnableClientState(ArrayCap.ColorArray);
            GL.ColorPointer(4, ColorPointerType.Float, 0, colors);

            /*
             vertexesCount:
            для отрисовки VA, нужно указать тип примитива, начальную вершину и общее кол-во вершин
            т.к. в нашем случае передаваемый массив - множество float 
            значений которое делится на три где 1 2 3 это X, Y, Z координаты соответственно, то мы общее число значений массива
            делим на 3 и получаем кол-во вершин нашей фигуры
             
            private float[] _vertices = { 
                0.0f, 0.2f, 0f, -> Vertex3(0, 0.2, 0) 
                0.2f, 0.0f, 0f, -> Vertex3(0.2, 0, 0)
                0.2f, -0.2f, 0f -> Vertex3(0.2, -0.2, 0)
            };

            с цветами соответственно также.

             */
            int vertexesCount = vertices.Length / 3;
            GL.DrawArrays(PrimitiveType.Polygon, 0, vertexesCount);

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);
        }
        // ---

        // --- VBO (Vertex Buffer Objects) 
        /*
         современный аналог DL - VBO. Он оптимизирует вызовы по принципу с DL, но позволяет менять геометрию, современнее,
        легче, контроллированее
        Создаётся Vertex Buffer (в видеопамяти) и он заполняется теми данными которые передаются из CPU 
         */
        private void InitVBO()
        {
            float rectSize = 0.6f;
            float opacity = 0.6f;

            float[] verts = { 
                -rectSize, rectSize, 0,
                rectSize, rectSize, 0,
                rectSize, -rectSize, 0,
                -rectSize, -rectSize, 0
            };
            float[] colors = { 
                0f,0f,1f,opacity,
                1f,1f,1f,opacity,
                1f,1f,0f,opacity,
                1f,0f,1f,opacity
            };

            _vertsVBOId = CreateVBO(verts);
            _colorVBOId = CreateVBO(colors);

            _vboVertsCount = verts.Length / 3;
        }

        private int CreateVBO(float[] data)
        {
            int vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo); // говорим GL с каким буфером работаем
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0); // сброс привязки буффера ( на 0 )

            return vbo;
        }

        private void DrawVBO(int vertsVBO, int colorsVBO, int vertsCount)
        {
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertsVBO);
            // 0 в конце вместо float[] verticles потому что данные уже в видеопамяти
            // читать DrawVA();
            GL.VertexPointer(3, VertexPointerType.Float, 0, 0); 

            GL.BindBuffer(BufferTarget.ArrayBuffer, colorsVBO);
            GL.ColorPointer(4, ColorPointerType.Float, 0, 0);

            // vertsCount нужно брать из массива данных о вершинах на CPU
            // т.к. запрашивать данные из видеопамяти дорого.
            GL.DrawArrays(PrimitiveType.Polygon, 0, vertsCount); 

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0); // сброс линка с буффером

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);
        }

        private void DeleteVBO(int VBOId)
        {
            GL.DeleteBuffer(VBOId);
        }
        // ---

        // --- Vertex Array Object (VAO no shaders)
        private void InitVAO()
        {
            float size = 0.2f;
            float opacity = 1.0f;

            float[] verts = { 
                -size, 0f, 0f,
                0, size, 0f,
                size, 0f, 0f,
                0, -size, 0f, 0f
            };
            float[] colors = { 
                0.4f, 0.4f, 0.4f, opacity,
                1f,1f,1f, opacity,
                0.4f, 0.4f, 0.4f, opacity,
                1f,1f,1f, opacity
            };

            _vaoId = CreateVAO(verts, colors);
            _vaoVertsCount = verts.Length / 3;
        }

        private int CreateVAO(float[] verts, float[] colors)
        {
            int vao = GL.GenVertexArray();

            GL.BindVertexArray(vao);

            int vboV = CreateVBO(verts);
            int vboC = CreateVBO(colors);

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);


            GL.BindBuffer(BufferTarget.ArrayBuffer, vboV);
            GL.VertexPointer(3, VertexPointerType.Float, 0, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboC);
            GL.ColorPointer(4, ColorPointerType.Float, 0, 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            // сброс линка на VAO должен происходит перед отключкой клиент стейтов
            GL.BindVertexArray(0);

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);

            return vao;
        }

        private void DrawVAO(int vao, int vertsCount)
        {
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Polygon, 0, vertsCount);
            GL.BindVertexArray(0);
        }

        private void DeleteVAO(int vao)
        {
            GL.DeleteVertexArray(vao);
        }
        // ---

        private float _fps;
        private float _deltaTime;
        private float _frameTime;
        private int _frameCounter;

        // --- Render ---
        private int _dlId;
        private int _colorVBOId;
        private int _vertsVBOId;
        private int _vboVertsCount;
        private int _vaoId;
        private int _vaoVertsCount;

        private float[] _vertices = { // x, y, z % 3 = 0
            0.0f, 0.2f, 0f,
            0.2f, 0.0f, 0f,
            0.0f, -0.2f, 0f
        };
        private float[] _verticesColors = { // r, g, b, a % 4 = 0
            1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f
        };
        // ---
    }

    class Program
    {
        static void Main(string[] args)
        {

            GLView glView = new GLView(1280, 720, "");
            glView.Run();

            Console.ReadLine();
        }
    }
}
