using System;
using UnityEngine;
using System.Collections;

namespace ImpactFeel
{
    [CreateAssetMenu(fileName = "ImpactSet", menuName = "Scriptable Objects/ImpactSet")]
    public class ImpactSet : ScriptableObject
    {
        public Impact[] impacts = Array.Empty<Impact>();

        [Serializable]
        public class Impact
        {
            public string name;
            [SerializeReference] public Effect[] effects;
        }

        public float Play(string impactName, ImpactController controller, float strength = 1f)
        {
            float maxDuration = 0f;
            foreach (var impact in impacts)
                if (impact.name == impactName)
                {
                    foreach (var effect in impact.effects)
                    {
                        float duration = effect.Play(controller, strength);
                        if (duration > maxDuration)
                            maxDuration = duration;
                    }
                    return maxDuration;
                }
            return 0f;
        }
    }

}