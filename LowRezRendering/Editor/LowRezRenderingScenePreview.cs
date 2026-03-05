#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LowRezRenderingScenePreview
{
    private const string ScenePreviewMenuPath = "View/Low Rez Scene Preview";
    private const string ScenePreviewEnabledKey = "Workbench.LowRezRendering.ScenePreviewEnabled";
    private static readonly PropertyInfo CameraViewportProperty =
        typeof(SceneView).GetProperty("cameraViewport", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    static LowRezRenderingScenePreview()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        LowRezRendering renderer = FindActiveRenderer();
        if (renderer == null || !renderer.previewInEditMode || !IsScenePreviewEnabled())
            return;

        if (Event.current.type != EventType.Repaint)
            return;

        Rect contentRect = GetSceneContentRect(sceneView);
        RenderTexture texture = renderer.RenderSceneViewPreview(sceneView.camera, sceneView.camera.pixelRect);
        if (texture == null)
            return;

        Handles.BeginGUI();
        EditorGUI.DrawRect(contentRect, Color.black);
        GUI.DrawTexture(contentRect, texture, ScaleMode.StretchToFill, false);

        Rect labelRect = new Rect(contentRect.x + 8f, contentRect.y + 8f, 220f, 18f);
        GUI.Label(
            labelRect,
            $"{renderer.preset}  {renderer.CurrentResolution.x}x{renderer.CurrentResolution.y} -> {texture.width}x{texture.height}",
            EditorStyles.whiteMiniLabel
        );
        Handles.EndGUI();
    }

    [MenuItem(ScenePreviewMenuPath)]
    private static void ToggleScenePreview()
    {
        SetScenePreviewEnabled(!IsScenePreviewEnabled());
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    [MenuItem(ScenePreviewMenuPath, true)]
    private static bool ValidateToggleScenePreview()
    {
        Menu.SetChecked(ScenePreviewMenuPath, IsScenePreviewEnabled());
        return true;
    }

    private static LowRezRendering FindActiveRenderer()
    {
        LowRezRendering[] renderers = Object.FindObjectsByType<LowRezRendering>(FindObjectsSortMode.None);
        foreach (LowRezRendering renderer in renderers)
        {
            if (renderer.isActiveAndEnabled)
                return renderer;
        }

        return null;
    }

    private static Rect GetSceneContentRect(SceneView sceneView)
    {
        if (CameraViewportProperty != null && CameraViewportProperty.GetValue(sceneView) is Rect viewport && viewport.width > 0f && viewport.height > 0f)
            return viewport;

        Rect cameraRect = sceneView.camera.pixelRect;
        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
        float guiX = cameraRect.x / pixelsPerPoint;
        float guiY = sceneView.position.height - (cameraRect.yMax / pixelsPerPoint);
        float guiWidth = cameraRect.width / pixelsPerPoint;
        float guiHeight = cameraRect.height / pixelsPerPoint;
        return new Rect(guiX, guiY, guiWidth, guiHeight);
    }

    private static bool IsScenePreviewEnabled()
    {
        return SessionState.GetBool(ScenePreviewEnabledKey, false);
    }

    private static void SetScenePreviewEnabled(bool enabled)
    {
        SessionState.SetBool(ScenePreviewEnabledKey, enabled);
        Menu.SetChecked(ScenePreviewMenuPath, enabled);
    }
}
#endif
