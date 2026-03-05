using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ImpactFeel
{
    [Serializable]
    public abstract class CoroutineAnimation : Effect
    {
        public override float Play(ImpactController controller, float strength = 1f)
        {
            controller.StartCoroutine(PlayCoroutine(controller, strength));
            return GetDuration();
        }

        protected abstract IEnumerator PlayCoroutine(ImpactController controller, float strength);
        protected abstract float GetDuration();
    }

    [Serializable]
    public class Curve3
    {
        public enum RepeatMode
        { Once, Loop, Accumulate } // Accumulate adds end value every loop
        public bool useSingleCurve = true;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve xCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve yCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve zCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public Vector3 valueScale = Vector3.one;
        public float timeScale = 1f;
        public int repeats = 1;
        int Repeats => repeatMode == RepeatMode.Once ? 1 : repeats;
        public RepeatMode repeatMode = RepeatMode.Once;

        float EndTime(AnimationCurve c) => c.keys[c.length - 1].time;
        float BaseDuration =>
            useSingleCurve
                ? EndTime(curve)
                : Mathf.Max(EndTime(xCurve), EndTime(yCurve), EndTime(zCurve));
        public float GetTotalDuration()
        {
            if (Repeats <= 0 || timeScale <= 0f) return 0f;
            return BaseDuration * Repeats * timeScale;
        }
        float SampleSafe(AnimationCurve c, float t) => c.Evaluate(Mathf.Clamp(t, 0f, EndTime(c)));
        Vector3 SampleSafe(float t)
        {
            if (useSingleCurve)
                return valueScale * SampleSafe(curve, t);
            else
                return new Vector3(
                    valueScale.x * SampleSafe(xCurve, t),
                    valueScale.y * SampleSafe(yCurve, t),
                    valueScale.z * SampleSafe(zCurve, t)
                );
        }
        public Vector3 Evaluate(float t)
        {
            float baseDuration = BaseDuration;
            if (baseDuration <= 0f || timeScale <= 0f || Repeats <= 0) return Vector3.zero;
            float tCurveTotal = Mathf.Clamp(t / timeScale, 0f, baseDuration * Repeats);
            if (repeatMode == RepeatMode.Once)
                return SampleSafe(tCurveTotal);
            if (repeatMode == RepeatMode.Loop)
                return SampleSafe(tCurveTotal % baseDuration);
            else if (repeatMode == RepeatMode.Accumulate)
            {
                int episodesFinished = Mathf.FloorToInt(tCurveTotal / baseDuration);
                float tIn = tCurveTotal - episodesFinished * baseDuration;
                Vector3 E = SampleSafe(baseDuration);
                Vector3 current = SampleSafe(tIn);
                return episodesFinished * E + current;
            }
            return Vector3.zero;
        }
    }
    [Serializable]
    public abstract class Curve3Animator<TTarget> : CoroutineAnimation where TTarget : UnityEngine.Object
    {
        public Curve3 curve = new Curve3();

        public bool resetOnStart = true; // Reset to avoid drift
        public bool resetOnEnd = true;
        public bool cacheOriginalValue = false; 

        protected override float GetDuration() => curve.GetTotalDuration();

        protected abstract Vector3 GetOriginalValue(ImpactController controller, TTarget target);
        protected abstract void ResetValue(TTarget target, Vector3 value);
        protected abstract void ApplyDelta(TTarget target, Vector3 delta);
        protected abstract TTarget GetTarget(ImpactController controller);

        protected override IEnumerator PlayCoroutine(ImpactController controller, float strength = 1f)
        {
            var target = GetTarget(controller);
            float total = curve.GetTotalDuration();
            if (!target || total <= 0f) yield break;

            float elapsed = 0f;
            Vector3 last = Vector3.zero; // if the curve dont start at zero, hop the target for initial offset

            if (resetOnStart)
            {
                last = curve.Evaluate(0f) * strength;
                var baseValue = cacheOriginalValue ? GetOriginalValue(controller, target) : Vector3.zero;
                ResetValue(target, last + baseValue);
            }
            while (elapsed < total && target)
            {
                Vector3 value = curve.Evaluate(elapsed) * strength;
                Vector3 delta = value - last;
                if (delta != Vector3.zero)
                    ApplyDelta(target, delta);
                last = value;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (target)
            {
                if (resetOnEnd)
                {
                    var baseValue = cacheOriginalValue ? GetOriginalValue(controller, target) : Vector3.zero;
                    ResetValue(target, baseValue);
                }
                else
                {
                    // apply the final delta
                    Vector3 value = curve.Evaluate(total) * strength;
                    Vector3 delta = value - last;
                    if (delta != Vector3.zero)
                        ApplyDelta(target, delta);
                }
            }
        }
    }
    [Serializable]
    public class Move : Curve3Animator<Transform>
    {
        public string targetTransformName = "";
        protected override void ResetValue(Transform target, Vector3 value) => target.localPosition = value;
        protected override void ApplyDelta(Transform target, Vector3 delta) => target.localPosition += delta;
        protected override Transform GetTarget(ImpactController controller) => controller.GetTransform(targetTransformName);
        class LocalPositionCache: EffectCache<Vector3> { }
        protected override Vector3 GetOriginalValue(ImpactController controller, Transform target) =>
            cacheOriginalValue ? controller.GetEffectCache<LocalPositionCache>().GetOriginal(target.GetInstanceID(), target.localPosition) : Vector3.zero;
    }
    [Serializable]
    public class Rotate : Curve3Animator<Transform>
    {
        public string targetTransformName = "";
        protected override void ResetValue(Transform target, Vector3 value) => target.localRotation = Quaternion.Euler(value);
        protected override void ApplyDelta(Transform target, Vector3 delta)
        {
            // convert to Euler, do additive
            Vector3 euler = target.localRotation.eulerAngles;
            euler += delta;
            target.localRotation = Quaternion.Euler(euler);

        }
        protected override Transform GetTarget(ImpactController controller) => controller.GetTransform(targetTransformName);
        class LocalRotationCache : EffectCache<Vector3> { }
        protected override Vector3 GetOriginalValue(ImpactController controller, Transform target) =>
            cacheOriginalValue ? controller.GetEffectCache<LocalRotationCache>().GetOriginal(target.GetInstanceID(), target.localRotation.eulerAngles) : Vector3.zero;
    }
    [Serializable]
    public class Scale : Curve3Animator<Transform>
    {
        public string targetTransformName = "";
        protected override void ResetValue(Transform target, Vector3 value) => target.localScale = value;
        protected override void ApplyDelta(Transform target, Vector3 delta) => target.localScale += delta;
        protected override Transform GetTarget(ImpactController controller) => controller.GetTransform(targetTransformName);
        class LocalScaleCache : EffectCache<Vector3> { }
        protected override Vector3 GetOriginalValue(ImpactController controller, Transform target) =>
            cacheOriginalValue ? controller.GetEffectCache<LocalScaleCache>().GetOriginal(target.GetInstanceID(), target.localScale) : Vector3.zero;
    }
    [Serializable]
    public class Alpha : Curve3Animator<Renderer>
    {
        public string targetRendererName = "";
        Material GetMaterialAndEnsureInstanced(Renderer renderer) => renderer.material; // automatically ensure instanced
        protected override void ResetValue(Renderer target, Vector3 value)
        {
            var mat = GetMaterialAndEnsureInstanced(target);
            mat.color = new UnityEngine.Color(mat.color.r, mat.color.g, mat.color.b, value.x);
        }
        protected override void ApplyDelta(Renderer target, Vector3 delta)
        {
            var mat = GetMaterialAndEnsureInstanced(target);
            mat.color = new UnityEngine.Color(mat.color.r, mat.color.g, mat.color.b, mat.color.a + delta.x);
        }
        protected override Renderer GetTarget(ImpactController controller) => controller.GetRenderer(targetRendererName);
        class AlphaCache : EffectCache<float> { }
        protected override Vector3 GetOriginalValue(ImpactController controller, Renderer target)
        {
            var mat = GetMaterialAndEnsureInstanced(target);
            float originalAlpha = cacheOriginalValue ? controller.GetEffectCache<AlphaCache>().GetOriginal(target.GetInstanceID(), mat.color.a) : mat.color.a;
            return new Vector3(originalAlpha, originalAlpha, originalAlpha); 
        }
    }
    [Serializable]
    public class Color : CoroutineAnimation
    {
        public string targetRendererName = "";
        [ColorUsage(false, true)]
        public UnityEngine.Color color = UnityEngine.Color.white;
        public enum BlendMode { Multiply, Additive, Mix }
        public BlendMode blendMode = BlendMode.Multiply;
        public Curve3 curve = new Curve3();
        public bool cacheOriginalValue = true;

        class ColorCache:EffectCache<UnityEngine.Color> { }

        protected override float GetDuration() => curve.GetTotalDuration();

        UnityEngine.Color Blend(UnityEngine.Color original, UnityEngine.Color target, float t)
        {
            // bypass alpha
            switch (blendMode)
            {
                case BlendMode.Multiply:
                    return new UnityEngine.Color(
                        Mathf.Lerp(original.r, original.r * target.r, t),
                        Mathf.Lerp(original.g, original.g * target.g, t),
                        Mathf.Lerp(original.b, original.b * target.b, t),
                        original.a
                    );
                case BlendMode.Additive:
                    return new UnityEngine.Color(
                        Mathf.Lerp(original.r, original.r + target.r, t),
                        Mathf.Lerp(original.g, original.g + target.g, t),
                        Mathf.Lerp(original.b, original.b + target.b, t),
                        original.a
                    );
                case BlendMode.Mix:
                    return new UnityEngine.Color(
                        Mathf.Lerp(original.r, target.r, t),
                        Mathf.Lerp(original.g, target.g, t),
                        Mathf.Lerp(original.b, target.b, t),
                        original.a
                    );
                default:
                    return original;
            }
        }
        UnityEngine.Color SetAlpha(UnityEngine.Color c, float alpha) => new UnityEngine.Color(c.r, c.g, c.b, alpha);
        protected override IEnumerator PlayCoroutine(ImpactController controller, float strength = 1f)
        {
            var renderer = controller.GetRenderer(targetRendererName);
            if (!renderer) yield break;
            var mat = renderer.material; // automatically ensure instanced

            var originalColor = cacheOriginalValue
                ? controller.GetEffectCache<ColorCache>().GetOriginal(renderer.GetInstanceID(), mat.color)
                : mat.color;
            var blendedColor = color;

            float total = curve.GetTotalDuration();
            float elapsed = 0f;
            while (elapsed < total)
            {
                float t = curve.Evaluate(elapsed).x * strength; // only use x for blending factor
                mat.color = SetAlpha(Blend(originalColor, blendedColor, t), mat.color.a); // keep original alpha
                elapsed += Time.deltaTime;
                yield return null;
            }
            // reset on end
            mat.color = SetAlpha(originalColor, mat.color.a);
        }
    }
}
