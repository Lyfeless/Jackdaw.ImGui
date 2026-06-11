## ImGui Integration
A wrapper for creating [ImGui](https://github.com/ocornut/imgui) menus as components in a Jackdaw project using the [ImguiNet bindings](https://github.com/ImGuiNET/ImGui.NET).
This extension is still work-in-progress and may be missing integration with some ImGui features.

### Usage
Jackdaw ImGUI is handled through `ImGuiComponent` components using custom behavior defined with `ILayoutBuilder`.

```cs
// Create the game instance with a basic configuration
Game game = new(new GameConfig() {
    // ... Basic game config
});

// ... Game setup

ScreenBoundsComponent screenBounds = new(game);
game.Root = new(game, screenBounds, new ImGuiComponent(game, screenBounds, new CustomImGuiMenu()));

// ... Continue game setup

// Define custom ImGui behavior
public struct CustomImGuiMenu(): ILayoutBuilder {
    public void Build() => ImGui.ShowDemoWindow();
}
```