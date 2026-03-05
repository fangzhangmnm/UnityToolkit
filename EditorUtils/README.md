# EditorUtils

Small Unity editor/runtime helper tools.  

## What is here

- `ButtonAttribute.cs`
Adds `[Button]` for parameterless `MonoBehaviour` methods so you can click-run them in Inspector in Editing mode and in Playing mode.

- `ConditionalFieldAttribute.cs`
Shows/hides serialized fields in Inspector based on a bool or enum field value.

- `Editor/TimeScaleInspector.cs`
Adds `Window/Time Control` for pause + `Time.timeScale` control during play mode.

- `Editor/EditorFullscreenToggle.cs`
Adds `F11` fullscreen toggle support for Unity Editor/GameView workflow.

- `Oscilloscope/` + `Oscilloscope.prefab`
Runtime signal visualizer that draws value curves into a `RenderTexture` (for in-game debug panels/VR surfaces).