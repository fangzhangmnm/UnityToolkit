using UnityEngine;
using System;
using System.Collections.Generic;
namespace ImpactFeel
{
    public class GlobalImpactController : ImpactController // a centralized variant for time, camera, etc
    {
        public ITimeController timeController;
        void Awake()
        {
            if (globalInstance != null) Debug.LogWarning($"Multiple global ImpactControllers detected! {globalInstance.name} and {name}.");
            globalInstance = this;
            if (timeController == null)
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb is ITimeController tc)
                    {
                        timeController = tc; break;
                    }
        }
    }
}