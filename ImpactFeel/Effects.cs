using System;
using System.Collections;
using UnityEngine;

namespace ImpactFeel
{
    [Serializable]
    public abstract class Effect
    {
        public abstract float Play(ImpactController controller, float strength = 1f);
    }

    [Serializable]
    public class AudioImpact : Effect
    {
        public string taregetSourceName;
        public AudioClip[] clips;
        public float volume = 1f;
        public float minPitch = 1f;
        public float maxPitch = 1f;

        public override float Play(ImpactController controller, float strength = 1f)
        {
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            AudioSource audio = controller.GetAudioSource(taregetSourceName);
            audio.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            audio.PlayOneShot(clip, volume * strength);
            return clip.length / audio.pitch;
        }
    }

    [Serializable]
    public class SpawnImpact : Effect
    {
        public string targetTransformName;
        public GameObject prefab;
        public float lifetime = 1f;

        public override float Play(ImpactController controller, float strength = 1f)
        {
            Transform targetTransform = controller.GetTransform(targetTransformName);
            GameObject spawned = GameObject.Instantiate(prefab, targetTransform.position, targetTransform.rotation);
            GameObject.Destroy(spawned, lifetime);
            return lifetime;
        }
    }

    [Serializable]
    public class AnimationImpact : Effect
    {
        public string targetAnimatorName;
        public string animationStateName;
        public float transitionDuration = 0.0f;

        public override float Play(ImpactController controller, float strength = 1f)
        {
            var anim = controller.GetAnimator(targetAnimatorName);
            anim.CrossFadeInFixedTime(animationStateName, transitionDuration);
            return GetAnimationDuration(anim, animationStateName);
        }
        private float GetAnimationDuration(Animator anim, string stateName)
        {
            var runtimeController = anim.runtimeAnimatorController;
            if (runtimeController == null) return 0f;
            foreach (var clip in runtimeController.animationClips)
                if (clip != null && clip.name == stateName)
                    return clip.length;
            return 0f;
        }
    }

    [Serializable]
    public class TriggerGlobalImpact : Effect
    {
        public string impactName;
        public float strengthMultiplier = 1f;
        public override float Play(ImpactController controller, float strength = 1)
        {
            if (ImpactController.globalInstance != null)
                ImpactController.globalInstance.Play(impactName, strength: strengthMultiplier * strength);
            return 0f; // this is a trigger, dont inherit duration from global impact
        }
    }

    public interface ICameraController
    {
        public void AddShake(Vector3 shakeOffset); // called each frame before LateUpdate, reset after LateUpdate
        public void AddFovKick(float fovOffset); // called each frame before LateUpdate, reset after LateUpdate
    }

    [Serializable]
    public class CameraShake : CoroutineAnimation
    {
        public float duration = 0.2f;
        public float amplitude = 0.15f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        protected override float GetDuration() => duration;

        protected override IEnumerator PlayCoroutine(ImpactController controller, float strength)
        {
            float elapsed = 0f;
            float seedX = UnityEngine.Random.Range(-1000f, 1000f);
            float seedY = UnityEngine.Random.Range(-1000f, 1000f);

            var camera = controller.GetMainCameraController();

            while (elapsed < duration && camera != null)
            {
                float shakeAmount = curve.Evaluate(elapsed / duration) * amplitude * strength;
                float shakeX = Mathf.PerlinNoise(seedX, elapsed * 10f) * 2f - 1f;
                float shakeY = Mathf.PerlinNoise(seedY, elapsed * 10f) * 2f - 1f;
                Vector3 shakeOffset = new Vector3(shakeX, shakeY, 0f) * shakeAmount;
                camera.AddShake(shakeOffset);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    [Serializable]
    public class CameraFOVKick : CoroutineAnimation
    {
        public float duration = 0.2f;
        public float fovIncrease = 5f;
        public AnimationCurve curve = new(new Keyframe(0, 0), new Keyframe(0.2f, 1.0f), new Keyframe(1, 0));

        protected override float GetDuration() => duration;

        protected override IEnumerator PlayCoroutine(ImpactController controller, float strength)
        {
            float elapsed = 0f;
            var camera = controller.GetMainCameraController();

            while (elapsed < duration && camera != null)
            {
                float fovOffset = curve.Evaluate(elapsed / duration) * fovIncrease * strength;
                camera.AddFovKick(fovOffset);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    public interface ITimeController
    {
        public void SetTimeScale(float timeScale);
        public int PauseTime();
        public void ResumeTime(int token);
    }

    [Serializable]
    public class TimePause : CoroutineAnimation
    {
        public float duration = 2/60f;
        protected override float GetDuration() => 0 * duration; // haha GetDuration returns scaled time
        protected override IEnumerator PlayCoroutine(ImpactController controller, float strength)
        {
            if (controller != ImpactController.globalInstance)
            {
                Debug.LogError("TimePause effect can only be played by the global ImpactController, because time is global.");
                yield break;
            }
            var timeController = controller.GetTimeController();
            var token = timeController.PauseTime();
            yield return new WaitForSecondsRealtime(duration * strength);
            timeController.ResumeTime(token);
        }
    }
}