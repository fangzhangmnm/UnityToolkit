using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LowRezRendering : MonoBehaviour
{

    [Header("Resolution")]
    public ResolutionPreset preset = ResolutionPreset.GameBoyAdvance;
    [Min(1)] public int targetWidth = 240;
    [Min(1)] public int targetHeight = 160;

    [Header("Scene References")]
    public Camera targetCamera;
    public RawImage targetCanvas;
    public RenderTexture targetTexture;
    public AspectRatioFitter renderAreaAspect;
    public CanvasScaler canvasScaler;

    [Header("Behavior")]
    public bool syncCanvasScalerToResolution;
    public bool previewInEditMode = true;

    private RenderTexture m_RuntimeTexture;

#if UNITY_EDITOR
    private RenderTexture m_SceneViewPreviewTexture;
    private Vector2Int m_SceneViewPreviewResolution;
    private bool m_EditorRefreshQueued;
    private bool m_EditorForceRecreateQueued;
#endif

    public RenderTexture CurrentTexture => targetTexture != null ? targetTexture : m_RuntimeTexture;
    public Vector2Int CurrentResolution => new(targetWidth, targetHeight);

    private bool IsRuntimeInstance()
    {
        return Application.IsPlaying(gameObject);
    }

    void Reset()
    {
        AutoAssignReferences();
        ApplyPresetSize();
        if (IsRuntimeInstance())
            ApplyRendering(forceRecreate: true, renderPreview: true);
        else
            QueueEditorRefresh(forceRecreate: true);
    }

    void OnEnable()
    {
        AutoAssignReferences();
        ApplyPresetSize();
        if (IsRuntimeInstance())
            ApplyRendering(forceRecreate: true, renderPreview: true);
        else
            QueueEditorRefresh(forceRecreate: true);
    }

    void Update()
    {
        if (IsRuntimeInstance())
            return;

#if UNITY_EDITOR
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
#endif

        AutoAssignReferences();
        ApplyPresetSize();
        ApplyRendering(forceRecreate: false, renderPreview: previewInEditMode);

    }

    void OnValidate()
    {
        targetWidth = Mathf.Max(1, targetWidth);
        targetHeight = Mathf.Max(1, targetHeight);

        AutoAssignReferences();
        ApplyPresetSize();
        QueueEditorRefresh(forceRecreate: true);
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshEditorState;
        m_EditorRefreshQueued = false;
        m_EditorForceRecreateQueued = false;
#endif
        ReleaseRuntimeTexture();
        ReleaseSceneViewPreviewTexture();
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshEditorState;
        m_EditorRefreshQueued = false;
        m_EditorForceRecreateQueued = false;
#endif
        ReleaseRuntimeTexture();
        ReleaseSceneViewPreviewTexture();
    }

    [Button]
    public void ApplyCurrentSettings()
    {
        ApplyPresetSize();
        if (IsRuntimeInstance())
            ApplyRendering(forceRecreate: true, renderPreview: true);
        else
            QueueEditorRefresh(forceRecreate: true);
    }

    public void SetPreset(ResolutionPreset newPreset)
    {
        preset = newPreset;
        ApplyPresetSize();
        if (IsRuntimeInstance())
            ApplyRendering(forceRecreate: true, renderPreview: true);
        else
            QueueEditorRefresh(forceRecreate: true);
    }

    public void SetCustomResolution(int width, int height)
    {
        preset = ResolutionPreset.Custom;
        targetWidth = Mathf.Max(1, width);
        targetHeight = Mathf.Max(1, height);
        if (IsRuntimeInstance())
            ApplyRendering(forceRecreate: true, renderPreview: true);
        else
            QueueEditorRefresh(forceRecreate: true);
    }

    public void SetCustomResolution(Vector2Int resolution)
    {
        SetCustomResolution(resolution.x, resolution.y);
    }

    [Button]
    public void RenderPreviewNow()
    {
        if (!previewInEditMode && !Application.isPlaying)
            return;

        Camera camera = ResolveCamera();
        RenderTexture texture = EnsureWorkingTexture(forceRecreate: false);
        if (camera == null || texture == null)
            return;

        RenderCameraToTexture(camera, texture);

        Canvas.ForceUpdateCanvases();

#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    public Vector2Int SceneViewPreviewResolution => m_SceneViewPreviewResolution;

    public RenderTexture RenderSceneViewPreview(Camera sceneViewCamera, Rect scenePixelRect)
    {
        if (!previewInEditMode || sceneViewCamera == null)
            return null;

        Vector2Int resolution = GetFitInsideResolution(scenePixelRect.width, scenePixelRect.height);
        float viewportAspect = Mathf.Max(1f, scenePixelRect.width) / Mathf.Max(1f, scenePixelRect.height);
        RenderTexture texture = EnsureSceneViewPreviewTexture(resolution, forceRecreate: false);
        RenderCameraToTexture(sceneViewCamera, texture, viewportAspect);
        return texture;
    }

    private void QueueEditorRefresh(bool forceRecreate)
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        m_EditorForceRecreateQueued |= forceRecreate;
        if (m_EditorRefreshQueued)
            return;

        m_EditorRefreshQueued = true;
        EditorApplication.delayCall += RefreshEditorState;
    }

    private void RefreshEditorState()
    {
        bool forceRecreate = m_EditorForceRecreateQueued;
        m_EditorRefreshQueued = false;
        m_EditorForceRecreateQueued = false;

        if (this == null || !isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        AutoAssignReferences();
        ApplyPresetSize();
        ApplyRendering(forceRecreate, renderPreview: previewInEditMode);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
#endif

    private void ApplyRendering(bool forceRecreate, bool renderPreview)
    {
        Camera camera = ResolveCamera();
        RenderTexture texture = EnsureWorkingTexture(forceRecreate);

        if (camera != null && texture != null)
        {
            if (camera.targetTexture != texture)
                camera.targetTexture = texture;

            if (IsRuntimeInstance())
                camera.aspect = targetWidth / (float)targetHeight;
            else
                camera.ResetAspect();
        }

        if (targetCanvas != null && texture != null && targetCanvas.texture != texture)
            targetCanvas.texture = texture;

        SyncCompanionComponents();

        if (renderPreview)
            RenderPreviewNow();
    }

    private void ApplyPresetSize()
    {
        if (preset == ResolutionPreset.Custom)
            return;

        Vector2Int size = GetResolutionForPreset(preset);
        targetWidth = size.x;
        targetHeight = size.y;
    }

    private void SyncCompanionComponents()
    {
        if (renderAreaAspect != null)
            renderAreaAspect.aspectRatio = targetWidth / (float)targetHeight;

        if (syncCanvasScalerToResolution && canvasScaler != null)
            canvasScaler.referenceResolution = new Vector2(targetWidth, targetHeight);
    }

    private RenderTexture EnsureWorkingTexture(bool forceRecreate)
    {
        if (targetTexture != null)
        {
            ConfigureTexture(targetTexture, targetWidth, targetHeight, forceRecreate);
            return targetTexture;
        }

        bool needsCreate = m_RuntimeTexture == null
            || m_RuntimeTexture.width != targetWidth
            || m_RuntimeTexture.height != targetHeight;

        if (needsCreate)
        {
            ReleaseRuntimeTexture();
            m_RuntimeTexture = new RenderTexture(targetWidth, targetHeight, 16)
            {
                name = "LowRezRuntimeTexture",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        ConfigureTexture(m_RuntimeTexture, targetWidth, targetHeight, forceRecreate);
        return m_RuntimeTexture;
    }

    private void ConfigureTexture(RenderTexture texture, int width, int height, bool forceRecreate)
    {
        if (texture == null)
            return;

        bool sizeChanged = texture.width != width || texture.height != height;
        if (sizeChanged || forceRecreate)
            texture.Release();

        if (sizeChanged)
        {
            texture.width = width;
            texture.height = height;
        }

        texture.filterMode = FilterMode.Point;
        texture.antiAliasing = 1;

        if (!texture.IsCreated())
            texture.Create();
    }

    private void RenderCameraToTexture(Camera camera, RenderTexture texture)
    {
        if (camera == null || texture == null)
            return;

        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = texture;
        camera.Render();
        camera.targetTexture = previousTarget;
    }

    private void RenderCameraToTexture(Camera camera, RenderTexture texture, float aspectRatio)
    {
        if (camera == null || texture == null)
            return;

        RenderTexture previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;

        camera.targetTexture = texture;
        camera.aspect = aspectRatio;
        camera.Render();
        camera.targetTexture = previousTarget;
        camera.aspect = previousAspect;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        targetCamera = Camera.main;
        return targetCamera;
    }

    private void AutoAssignReferences()
    {
        ResolveCamera();

        if (targetCanvas == null)
            targetCanvas = GetComponentInChildren<RawImage>(includeInactive: true);

        if (renderAreaAspect == null && targetCanvas != null)
            renderAreaAspect = targetCanvas.GetComponentInParent<AspectRatioFitter>();

        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();

        if (targetTexture == null)
        {
            if (targetCanvas != null && targetCanvas.texture is RenderTexture canvasTexture)
            {
                targetTexture = canvasTexture;
            }
            else if (targetCamera != null && targetCamera.targetTexture != null)
            {
                targetTexture = targetCamera.targetTexture;
            }
        }
    }

    private void ReleaseRuntimeTexture()
    {
        if (m_RuntimeTexture == null)
            return;

        Camera camera = ResolveCamera();
        if (camera != null && camera.targetTexture == m_RuntimeTexture)
        {
            camera.targetTexture = null;
            camera.ResetAspect();
        }

        if (targetCanvas != null && targetCanvas.texture == m_RuntimeTexture)
            targetCanvas.texture = null;

        m_RuntimeTexture.Release();

        if (Application.isPlaying)
            Destroy(m_RuntimeTexture);
        else
            DestroyImmediate(m_RuntimeTexture);

        m_RuntimeTexture = null;
    }

#if UNITY_EDITOR
    private RenderTexture EnsureSceneViewPreviewTexture(Vector2Int resolution, bool forceRecreate)
    {
        bool needsCreate = m_SceneViewPreviewTexture == null
            || m_SceneViewPreviewTexture.width != resolution.x
            || m_SceneViewPreviewTexture.height != resolution.y;

        if (needsCreate)
        {
            ReleaseSceneViewPreviewTexture();
            m_SceneViewPreviewTexture = new RenderTexture(resolution.x, resolution.y, 16)
            {
                name = "LowRezSceneViewPreviewTexture",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                hideFlags = HideFlags.DontSaveInEditor
            };
        }

        m_SceneViewPreviewResolution = resolution;
        ConfigureTexture(m_SceneViewPreviewTexture, resolution.x, resolution.y, forceRecreate);
        return m_SceneViewPreviewTexture;
    }

    private void ReleaseSceneViewPreviewTexture()
    {
        if (m_SceneViewPreviewTexture == null)
            return;

        m_SceneViewPreviewTexture.Release();
        DestroyImmediate(m_SceneViewPreviewTexture);
        m_SceneViewPreviewTexture = null;
        m_SceneViewPreviewResolution = default;
    }
#else
    private void ReleaseSceneViewPreviewTexture()
    {
    }
#endif

    private Vector2Int GetFitInsideResolution(float viewportWidth, float viewportHeight)
    {
        float safeViewportWidth = Mathf.Max(1f, viewportWidth);
        float safeViewportHeight = Mathf.Max(1f, viewportHeight);
        float scale = Mathf.Min(safeViewportWidth / targetWidth, safeViewportHeight / targetHeight);
        if (scale <= 0f)
            return CurrentResolution;

        int width = Mathf.Max(1, Mathf.RoundToInt(safeViewportWidth / scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(safeViewportHeight / scale));
        return new Vector2Int(width, height);
    }

    public enum ResolutionPreset
    {
        Custom,
        LowRezJam,
        GameBoy,
        GameBoyAdvance,
        NES,
        SNES,
        Genesis,
        PlayStation,
        Nintendo64,
        NintendoDS,
        PSP,
        VGA
    }
    private static Vector2Int GetResolutionForPreset(ResolutionPreset resolutionPreset)
    {
        return resolutionPreset switch
        {
            ResolutionPreset.LowRezJam => new Vector2Int(64, 64),
            ResolutionPreset.GameBoy => new Vector2Int(160, 144),
            ResolutionPreset.GameBoyAdvance => new Vector2Int(240, 160),
            ResolutionPreset.NES => new Vector2Int(256, 240),
            ResolutionPreset.SNES => new Vector2Int(256, 224),
            ResolutionPreset.Genesis => new Vector2Int(320, 224),
            ResolutionPreset.PlayStation => new Vector2Int(320, 240),
            ResolutionPreset.Nintendo64 => new Vector2Int(320, 240),
            ResolutionPreset.NintendoDS => new Vector2Int(256, 192),
            ResolutionPreset.PSP => new Vector2Int(480, 272),
            ResolutionPreset.VGA => new Vector2Int(640, 480),
            _ => new Vector2Int(240, 160),
        };
    }
}
