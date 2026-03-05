#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ImpactFeel
{

    [CustomEditor(typeof(ImpactController), true)]
    public class ImpactControllerEditor : Editor
    {
        private bool showImpactSet = true;
        private Editor impactSetEditor;

        private void OnDisable()
        {
            if (impactSetEditor != null)
                DestroyImmediate(impactSetEditor);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawImpactSet(((ImpactController)target).impactSet);
        }

        private void DrawImpactSet(ImpactSet impactSet)
        {
            if (impactSet == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showImpactSet = EditorGUILayout.Foldout(showImpactSet, "Impact Set", true);
            if (showImpactSet)
            {
                Editor.CreateCachedEditor(impactSet, null, ref impactSetEditor);
                impactSetEditor.OnInspectorGUI();
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
