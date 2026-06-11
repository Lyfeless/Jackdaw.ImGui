using System.Numerics;
using Foster.Framework;
using ImGuiNET;

namespace Jackdaw.UI.ImGuiUI;

/// <summary>
/// A component for updating and rendering custom ImGui content.
/// </summary>
/// <param name="game">The current game instance.</param>
/// <param name="bounds">The bounds to render the content inside of.</param>
/// <param name="builders">The builders for controlling what content is shown.</param>
public unsafe class ImGuiComponent(Game game, BoundsComponent bounds, params ILayoutBuilder[] builders) : Component(game) {
    IntPtr ImGuiContext;

    /// <summary>
    /// The bounds to render ImGui content inside of.
    /// </summary>
    public BoundsComponent Bounds = bounds;

    Target target;
    Texture font;
    private static Mesh mesh;
    private static Material material;
    readonly List<Texture> boundTextures = [];

    /// <summary>
    /// The builders for controlling what content is shown.
    /// </summary>
    public List<ILayoutBuilder> Builders = [.. builders];

    protected override void EnterTree() {
        ImGuiContext = ImGui.CreateContext();

        SetContext();

        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags = ImGuiBackendFlags.None;
        io.ConfigFlags = ImGuiConfigFlags.DockingEnable;

        io.Fonts.AddFontDefault();

        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);
        font = new Texture(Game.GraphicsDevice, width, height, new ReadOnlySpan<byte>(pixelData, width * height * 4));

        mesh = new Mesh<PosTexColVertex, ushort>(Game.GraphicsDevice);
        material = new(Game.GraphicsDevice.Defaults.TexturedMaterial.Clone());
        SetBounds();

        ClearContext();
    }

    protected override void ExitTree() {
        ImGui.DestroyContext(ImGuiContext);
        ImGuiContext = nint.Zero;
    }

    protected override void Update() {
        SetContext();

        boundTextures.Clear();

        if (BoundsChanged) { SetBounds(); }

        var io = ImGui.GetIO();

        io.Fonts.SetTexID(GetTextureID(font));

        io.DeltaTime = Game.Time.Delta;
        io.DisplaySize = Bounds.Size;

        Vector2 localMousePos = Game.Convert.MouseToDisplayLocal(Actor);
        io.AddMousePosEvent(localMousePos.X, localMousePos.Y);
        io.AddMouseButtonEvent((int)ImGuiMouseButton.Left, Game.Input.Mouse.LeftDown || Game.Input.Mouse.LeftPressed);
        io.AddMouseButtonEvent((int)ImGuiMouseButton.Right, Game.Input.Mouse.RightDown || Game.Input.Mouse.RightPressed);
        io.AddMouseButtonEvent((int)ImGuiMouseButton.Middle, Game.Input.Mouse.MiddleDown || Game.Input.Mouse.MiddlePressed);
        io.AddMouseWheelEvent(Game.Input.Mouse.Wheel.X, Game.Input.Mouse.Wheel.Y);

        io.AddKeyEvent(ImGuiKey.ModShift, Game.Input.Keyboard.Shift);
        io.AddKeyEvent(ImGuiKey.ModAlt, Game.Input.Keyboard.Alt);
        io.AddKeyEvent(ImGuiKey.ModCtrl, Game.Input.Keyboard.Ctrl);
        io.AddKeyEvent(ImGuiKey.ModSuper, Game.Input.Keyboard.Down(Keys.LeftOS) || Game.Input.Keyboard.Down(Keys.RightOS));

        foreach (var k in Mappings.KeyMappings) {
            if (Game.Input.Keyboard.Pressed(k.Item2))
                io.AddKeyEvent(k.Item1, true);
            if (Game.Input.Keyboard.Released(k.Item2))
                io.AddKeyEvent(k.Item1, false);
        }

        if (Game.Input.Keyboard.Text.Length > 0) {
            for (int i = 0; i < Game.Input.Keyboard.Text.Length; i++)
                io.AddInputCharacter(Game.Input.Keyboard.Text[i]);
        }

        if (io.WantTextInput) { Game.Window.StartTextInput(); }
        else { Game.Window.StopTextInput(); }

        ImGui.NewFrame();
        foreach (ILayoutBuilder builder in Builders) {
            builder.Build();
        }
        ImGui.Render();

        ClearContext();
    }

    protected override void Render(Batcher batcher) {
        if (mesh == null || material == null) {
            return;
        }

        SetContext();
        ImDrawDataPtr drawData = ImGui.GetDrawData();
        ClearContext();

        target.Clear(Color.Transparent);

        if (drawData.NativePtr == null || drawData.CmdListsCount <= 0 || drawData.TotalVtxCount <= 0) {
            return;
        }

        Matrix4x4 mat = Matrix4x4.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0.1f, 1000.0f);
        material.Vertex.SetUniformBuffer(mat);

        for (int i = 0; i < drawData.CmdListsCount; ++i) {
            RenderCommandList(drawData.CmdLists[i], drawData);
        }

        batcher.Image(target, Bounds.Position, Color.White);

    }

    void RenderCommandList(ImDrawListPtr commandList, ImDrawDataPtr drawData) {
        mesh.SetVertices(commandList.VtxBuffer.Data, commandList.VtxBuffer.Size);
        mesh.SetIndices(commandList.IdxBuffer.Data, commandList.IdxBuffer.Size);

        for (int i = 0; i < commandList.CmdBuffer.Size; ++i) {
            ImDrawCmdPtr drawCommand = commandList.CmdBuffer[i];

            if (drawCommand.ElemCount == 0) { continue; }

            RectInt scissor = AsRect(drawCommand.ClipRect).Scale(drawData.FramebufferScale).Int();
            if (scissor.Width <= 0 || scissor.Height <= 0) { continue; }

            if (drawCommand.TextureId < boundTextures.Count) {
                material.Fragment.Samplers[0] = new(boundTextures[(int)drawCommand.TextureId], new());
            }

            DrawCommand command = new(target, mesh, material) {
                BlendMode = new(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                VertexOffset = (int)drawCommand.VtxOffset,
                IndexOffset = (int)drawCommand.IdxOffset,
                IndexCount = (int)drawCommand.ElemCount,
                Scissor = scissor
            };
            command.Submit(Game.GraphicsDevice);
        }
    }

    /// <summary>
    /// Prepare a texture for use in ImGui menus and get its numeric identifier
    /// </summary>
    /// <param name="texture">The texture to use.</param>
    /// <returns>The numeric id used for rendering an image with ImGui.</returns>
    public IntPtr GetTextureID(Texture texture) {
        boundTextures.Add(texture);
        return new(boundTextures.Count - 1);
    }

    void SetContext() { ImGui.SetCurrentContext(ImGuiContext); }
    void ClearContext() { ImGui.SetCurrentContext(nint.Zero); }

    int BoundsWidth => (int)Bounds.Size.X;
    int BoundsHeight => (int)Bounds.Size.Y;
    bool BoundsChanged => target.Width != BoundsWidth || target.Width != BoundsHeight;

    void SetBounds() {
        target = new(Game.GraphicsDevice, BoundsWidth, BoundsHeight);
    }

    static Rect AsRect(Vector4 vec) => new(
        vec.X,
        vec.Y,
        vec.Z - vec.X,
        vec.W - vec.Y
    );
}