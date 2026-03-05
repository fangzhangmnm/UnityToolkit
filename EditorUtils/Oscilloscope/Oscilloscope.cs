/*********
Creates an oscilloscope on an texture in Unity3D, so you can show it in VR or on debug GUI
to debug transient physical values over time.

Usage:
    Oscilloscope.Instance.LogValue("MotorSpeed", motorSpeed);
    Oscilloscope.Instance.AccumulateValue("ForceApplied", force.y);
    void OnJumpStart() {
        Oscilloscope.Instance.SetTrigger(0.5f); 
        // Only record the next 0.5 seconds of data after jump starts, 
        // then freeze the display until next trigger.
    }
    Oscilloscope.Instance.ResetScanMode(5.0f); 
        // Recover to moving window mode with 5 seconds timebase.
        // clears all previous data.

Author: fangzhangmnm and chatgpt.com, Jan.9 2026
License: MIT
*********/

using UnityEngine;
using System.Collections.Generic;
using TMPro;
[DefaultExecutionOrder(1000)]// ensure it updates after most components
public class Oscilloscope : MonoBehaviour
{

    public void RecordValue(string label, float value) => GetOrCreateCurve(label).SetValue(value);
    public void AccumulateValue(string label, float delta, float defaultValue = 0f) => GetOrCreateCurve(label).AccumulateValue(delta, defaultValue);
    public void ResetScanMode(float timebase = 10.0f)
    {
        triggerMode = TriggerMode.MovingWindow;
        timebaseSeconds = timebase;
        ClearAllData();
    }
    public void SetTrigger(float duration = 10.0f)
    {
        triggerMode = TriggerMode.Triggered;
        timebaseSeconds = duration;
        ClearAllData();
    }
    public RenderTexture renderTexture;
    public TMP_Text infoText;
    public enum TriggerMode { MovingWindow , Triggered }
    public TriggerMode triggerMode = TriggerMode.MovingWindow;
    public float timebaseSeconds = 10.0f; // for moving window mode
    public float verticalRange { get; private set; } = 1.0f; // vertical range
    public bool autoScaleVerticalRange = true;
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    public Color gridColor = new Color(0.2f, 0.2f, 0.2f);
    public int verticalDivisions = 4;
    public bool debug_sine_test = false;
    public int horizontalDivisions = 10;
    public float DeltaTime => Time.fixedDeltaTime;
    // Update
    void Tick() {
        // Advance frame for each curve
        foreach (var curve in curves)
            curve.NextFrame();
        int nFrames = Mathf.CeilToInt(timebaseSeconds / DeltaTime);
        // Trim data according to mode
        foreach (var curve in curves)
        {
            if (triggerMode == TriggerMode.MovingWindow)
                curve.TrimFirst(nFrames);
            else if (triggerMode == TriggerMode.Triggered)
                curve.TrimLast(nFrames);
        }
        // Auto scale vertical range
        if (autoScaleVerticalRange)
        {
            float maxAbsValue = 0f;
            foreach (var curve in curves)
                maxAbsValue = Mathf.Max(maxAbsValue, curve.GetMaxAbsValue());
            float rawDivRange = Mathf.Max(0.001f, maxAbsValue * 1.2f / verticalDivisions);
            float log10 = Mathf.Log10(rawDivRange);
            float baseValue = Mathf.Pow(10f, Mathf.Floor(log10));
            float fraction = rawDivRange / baseValue;
            float roundedDivRange;
            if (fraction < 1f)
            roundedDivRange = 1f * baseValue;
            else if (fraction < 2f)
            roundedDivRange = 2f * baseValue;
            else if (fraction < 5f)
            roundedDivRange = 5f * baseValue;
            else
            roundedDivRange = 10f * baseValue;
            verticalRange = roundedDivRange * verticalDivisions;
        }
        // Render
        if (renderTexture != null)
        {
            Draw();
        }
        // Update info text
        if (infoText != null)
        {
            infoText.richText = true;
            infoText.text = GetInfoText();
        }
    }
    void DebugSineTest()
    {
        float t = Time.time;
        string label = "1/2*sin(π*t)";
        RecordValue(label, Mathf.Sin(t * 2f * Mathf.PI * 0.5f));
    }
    // Rendering    
    static Material _mat;
    static void EnsureMat()
    {
        if (_mat) return;
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _mat.hideFlags = HideFlags.HideAndDontSave;
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
    }

    // Your signature:
    public static void DrawLineAA(float x0, float y0, float x1, float y1, Color c)
    {
        EnsureMat();
        _mat.SetPass(0);

        GL.Begin(GL.LINES);
        GL.Color(c);
        GL.Vertex3(x0, y0, 0);
        GL.Vertex3(x1, y1, 0);
        GL.End();
    }
    void DrawCurve(Curve curve, float xRange, float yRange)
    {
        // Connect dots using anti-aliased lines
        // break line if value is NaN\
        float width = renderTexture.width;
        float height = renderTexture.height;
        float halfHeight = height / 2f;
        float xScale = width / xRange;
        float yScale = halfHeight / yRange;
        Vector2? prevPoint = null;
        for (int i = 0; i < curve.values.Count; i++)
        {
            float v = curve.values[i];
            if (!float.IsFinite(v)) { prevPoint = null; continue; }
            Vector2 currPoint = new Vector2(i * DeltaTime * xScale, halfHeight - v * yScale);
            if (prevPoint.HasValue)
            {
                DrawLineAA(prevPoint.Value.x, prevPoint.Value.y, currPoint.x, currPoint.y, curve.color);
            }
            prevPoint = currPoint;
        }
    }
    void DrawGrid(float xRange, float yRange)
    {
        float width = renderTexture.width;
        float height = renderTexture.height;
        float halfHeight = height / 2f;
        float xScale = width / xRange;
        float yScale = halfHeight / yRange;
        // Vertical lines
        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float x = i * (width / horizontalDivisions);
            DrawLineAA(x, 0, x, height, gridColor);
        }
        // Horizontal lines
        for (int i = -verticalDivisions; i <= verticalDivisions; i++)
        {
            float y = halfHeight + i * (halfHeight / verticalDivisions);
            DrawLineAA(0, y, width, y, gridColor);
        }
    }
    void Draw()
    {
        if (renderTexture == null) return;
        RenderTexture.active = renderTexture;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);
        // Clear background
        GL.Clear(true, true, backgroundColor);
        // we do not update range here. just draw
        DrawGrid(timebaseSeconds, verticalRange);
        foreach (var curve in curves)
            DrawCurve(curve, timebaseSeconds, verticalRange);
        GL.PopMatrix();
        RenderTexture.active = null;
    }
    string GetInfoText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        float divT = timebaseSeconds / horizontalDivisions;
        float divV = verticalRange / verticalDivisions;
        sb.AppendLine($"ΔT: {divT:F2}s  ΔV: ±{divV:F2}");
        foreach (var curve in curves)
            sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(curve.color)}>{curve.label}</color>");
        return sb.ToString();
    }

    // Data Structure
    public class Curve
    {
        public string label;
        public Color color;
        public List<float> values = new();
        public float currentValue = float.NaN;
        public void NextFrame() { values.Add(currentValue); currentValue = float.NaN; }
        public void SetValue(float value) { currentValue = value; }
        public void AccumulateValue(float delta, float defaultValue = 0f) { currentValue = float.IsNaN(currentValue) ? defaultValue + delta : currentValue + delta; }
        public void Clear() { values.Clear(); }
        // public void Trim(int nFrames) { if (values.Count > nFrames) values.RemoveRange(0, values.Count - nFrames); }
        public void TrimFirst(int nFrames) { if (values.Count > nFrames) values.RemoveRange(0, values.Count - nFrames); }
        public void TrimLast(int nFrames) { if (values.Count > nFrames) values.RemoveRange(nFrames, values.Count - nFrames); }
        public float GetMaxAbsValue()
        {
            float maxAbs = 0f;
            foreach (var v in values)
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(float.IsFinite(v) ? v : 0f));
            return maxAbs;
        }
    }
    private List<Curve> curves = new();
    private Curve GetOrCreateCurve(string label)
    {
        foreach (var curve in curves)
            if (curve.label == label)
                return curve;
        var newCurve = new Curve() { label = label, color = GetNextColor() };
        curves.Add(newCurve);
        return newCurve;
    }
    private void ClearAllData()
    {
        foreach (var curve in curves)
            curve.Clear();
    }
    // Color assignment
    private int currentColorIndex = 0;
    private Color GetNextColor()
    {
        // use matplotlib tab10 colors
        Color[] tab10 = new Color[]
        {
            new Color(0.121f, 0.467f, 0.706f),
            new Color(1.000f, 0.498f, 0.054f),
            new Color(0.172f, 0.627f, 0.172f),
            new Color(0.839f, 0.153f, 0.157f),
            new Color(0.580f, 0.404f, 0.741f),
            new Color(0.549f, 0.337f, 0.294f),
            new Color(0.890f, 0.467f, 0.761f),
            new Color(0.498f, 0.498f, 0.498f),
            new Color(0.737f, 0.741f, 0.133f),
            new Color(0.090f, 0.745f, 0.811f)
        };
        Color color = tab10[currentColorIndex % tab10.Length];
        currentColorIndex++;
        return color;
    }
    // Singleton pattern
    public static Oscilloscope Instance;
    void EnsureSingleInstance() { if (Instance == null) Instance = this; else { Destroy(this); return; } }
    void Awake()
    {
        EnsureSingleInstance();
    }
    void FixedUpdate()
    {
        if(debug_sine_test) DebugSineTest();
        Tick();
    }
}
