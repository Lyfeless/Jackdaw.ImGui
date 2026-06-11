namespace Jackdaw.UI.ImGuiUI;

/// <summary>
/// An interface for adding custom menu elements to an ImGui instance.
/// </summary>
public interface ILayoutBuilder {
    /// <summary>
    /// Add custom elements to an ImGui instance.
    /// Should include all behavior between the ImGui.NewFrame() and ImGui.Render() commands.
    /// </summary>
    public void Build();
}