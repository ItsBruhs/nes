using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Nes;

public class NesWindow : GameWindow
{
    private int shaderProgram;
    private int vao, vbo;
    private int textureId;

    private Ppu ppu;

    public NesWindow(Ppu ppu) : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
        {
            ClientSize = new Vector2i(1024, 960), // 4x nes resolution
            Title = "NES Emulator"
        })
    {
        this.ppu = ppu;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        // Fullscreen quad
        float[] quadVertices =
        {
            // positions   // tex coords
            -1f,  1f,  0f, 0f,
            -1f, -1f,  0f, 1f,
             1f, -1f,  1f, 1f,
             1f,  1f,  1f, 0f
        };

        uint[] indices = [0, 1, 2, 2, 3, 0];

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

        int ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // Load shaders
        string vertSource = """
            #version 330 core
            layout(location = 0) in vec2 aPosition;
            layout(location = 1) in vec2 aTexCoord;

            out vec2 vTexCoord;

            void main()
            {
                vTexCoord = aTexCoord;
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }

            """;

        string fragSource = """
            #version 330 core
            in vec2 vTexCoord;
            out vec4 FragColor;

            uniform sampler2D uTexture;

            void main()
            {
                FragColor = texture(uTexture, vTexCoord);
            }

            """;

        int vertShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertShader, vertSource);
        GL.CompileShader(vertShader);
        Console.WriteLine(GL.GetShaderInfoLog(vertShader));

        int fragShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragShader, fragSource);
        GL.CompileShader(fragShader);
        Console.WriteLine(GL.GetShaderInfoLog(fragShader));

        shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertShader);
        GL.AttachShader(shaderProgram, fragShader);
        GL.LinkProgram(shaderProgram);

        GL.DeleteShader(vertShader);
        GL.DeleteShader(fragShader);

        // Texture
        textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    }

    protected override void OnRenderFrame(OpenTK.Windowing.Common.FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        if (ppu.FrameReady)
        {
            ppu.FrameReady = false;

            // Upload nes framebuffer
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            unsafe
            {
                fixed (uint* ptr = ppu.Framebuffer)
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                        256, 240, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (nint)ptr);
                }
            }
        }

        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);

        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);

        SwapBuffers();
    }
}
