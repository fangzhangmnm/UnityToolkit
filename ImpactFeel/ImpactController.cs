using UnityEngine;
using System;
using System.Collections.Generic;
namespace ImpactFeel
{
    public class ImpactController : MonoBehaviour
    {
        public AudioSource[] targetAudioSources;
        public Transform[] targetTransforms;
        public Animator[] targetAnimators;
        public Renderer[] targetRenderers;
        public ImpactSet impactSet;
        public static GlobalImpactController globalInstance { get; protected set; }

        public float Play(string impactName, float strength = 1f)
        {
            return impactSet.Play(impactName, this, strength: strength);
        }


        private TTarget FindTarget<TTarget>(TTarget[] targets, string preferredName) where TTarget : Component
        {
            foreach (var target in targets)
                if (target.name == preferredName)
                    return target;
            return targets.Length > 0 ? targets[0] : GetComponentInChildren<TTarget>();
        }

        private readonly Dictionary<Type, EffectCache> effectCaches = new();
        public TCache GetEffectCache<TCache>() where TCache : EffectCache, new()
        {
            var type = typeof(TCache);
            if (!effectCaches.TryGetValue(type, out var cache))
                effectCaches[type] = cache = new TCache();
            return (TCache)cache;
        }


        public AudioSource GetAudioSource(string preferredName) => FindTarget(targetAudioSources, preferredName);
        public Transform GetTransform(string preferredName) => FindTarget(targetTransforms, preferredName);
        public Animator GetAnimator(string preferredName) => FindTarget(targetAnimators, preferredName);
        public Renderer GetRenderer(string preferredName) => FindTarget(targetRenderers, preferredName);
        public ICameraController GetMainCameraController() => Camera.main.GetComponent<ICameraController>();
        public ITimeController GetTimeController() => globalInstance != null ? globalInstance.timeController : null;
    }

    public abstract class EffectCache { }

    public abstract class EffectCache<T> : EffectCache
    {
        public Dictionary<int, T> cache = new();
        public T GetOriginal(int id, T current)
        {
            if (cache.TryGetValue(id, out var cached)) return cached;
            return cache[id] = current;
        }
    }
}