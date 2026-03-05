#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ImpactFeel
{

    [CustomEditor(typeof(ImpactSet))]
    public class ImpactSetEditor : Editor
    {
        private static Effect copiedImpact;
        private static ImpactSet.Impact copiedImpactSet;

        private SerializedProperty impactsProperty;
        private Type[] effectTypes = Array.Empty<Type>();
        private string[] effectTypeNames = Array.Empty<string>();

        private void OnEnable()
        {
            impactsProperty = serializedObject.FindProperty("impacts");
            CacheEffectTypes();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawImpacts();

            serializedObject.ApplyModifiedProperties();
        }

        private void CacheEffectTypes()
        {
            effectTypes = TypeCache.GetTypesDerivedFrom<Effect>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();

            effectTypeNames = new string[effectTypes.Length + 1];
            effectTypeNames[0] = "None";
            for (int i = 0; i < effectTypes.Length; i++)
                effectTypeNames[i + 1] = effectTypes[i].Name;
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ImpactSet)target), typeof(MonoScript), false);
        }

        private void DrawImpacts()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Impacts", EditorStyles.boldLabel);

            for (int i = 0; i < impactsProperty.arraySize; i++)
                if (DrawImpact(i))
                    break;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Impact"))
                AddImpact();
            if (GUILayout.Button("Paste Impact") && copiedImpactSet != null)
                AddImpact(copiedImpactSet);
            EditorGUILayout.EndHorizontal();
        }

        private bool DrawImpact(int impactIndex)
        {
            SerializedProperty impactProperty = impactsProperty.GetArrayElementAtIndex(impactIndex);
            SerializedProperty nameProperty = impactProperty.FindPropertyRelative("name");
            SerializedProperty effectsProperty = impactProperty.FindPropertyRelative("effects");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string impactLabel = string.IsNullOrEmpty(nameProperty.stringValue) ? $"Impact {impactIndex}" : nameProperty.stringValue;
            impactProperty.isExpanded = EditorGUILayout.Foldout(impactProperty.isExpanded, impactLabel, true);

            if (GUILayout.Button("Up", GUILayout.Width(38)) && impactIndex > 0)
            {
                impactsProperty.MoveArrayElement(impactIndex, impactIndex - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }

            if (GUILayout.Button("Down", GUILayout.Width(48)) && impactIndex < impactsProperty.arraySize - 1)
            {
                impactsProperty.MoveArrayElement(impactIndex, impactIndex + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }

            if (GUILayout.Button("Copy", GUILayout.Width(46)))
                copiedImpactSet = CopyImpact(impactProperty);

            if (GUILayout.Button("Paste", GUILayout.Width(46)) && copiedImpactSet != null)
                PasteImpact(impactProperty, copiedImpactSet);

            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                impactsProperty.DeleteArrayElementAtIndex(impactIndex);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }

            EditorGUILayout.EndHorizontal();

            if (impactProperty.isExpanded)
            {
                DrawFieldsExcept(impactProperty, "effects");

                for (int i = 0; i < effectsProperty.arraySize; i++)
                    if (DrawImpact(effectsProperty, i))
                        break;

                if (GUILayout.Button("Add Effect"))
                    ShowAddEffectMenu(effectsProperty);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);

            return false;
        }

        private bool DrawImpact(SerializedProperty effectsProperty, int effectIndex)
        {
            SerializedProperty effectProperty = effectsProperty.GetArrayElementAtIndex(effectIndex);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string effectLabel = effectProperty.managedReferenceValue == null
                ? $"Effect {effectIndex}"
                : effectProperty.managedReferenceValue.GetType().Name;
            effectProperty.isExpanded = EditorGUILayout.Foldout(effectProperty.isExpanded, effectLabel, true);

            if (GUILayout.Button("Up", GUILayout.Width(38)) && effectIndex > 0)
            {
                effectsProperty.MoveArrayElement(effectIndex, effectIndex - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }

            if (GUILayout.Button("Down", GUILayout.Width(48)) && effectIndex < effectsProperty.arraySize - 1)
            {
                effectsProperty.MoveArrayElement(effectIndex, effectIndex + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }

            if (GUILayout.Button("Copy", GUILayout.Width(46)))
                copiedImpact = CloneEffect((Effect)effectProperty.managedReferenceValue);

            if (GUILayout.Button("Paste", GUILayout.Width(46)) && copiedImpact != null)
                effectProperty.managedReferenceValue = CloneEffect(copiedImpact);

            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                effectsProperty.DeleteArrayElementAtIndex(effectIndex);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }

            EditorGUILayout.EndHorizontal();

            if (effectProperty.isExpanded)
            {
                DrawTypeDropdown(effectProperty);

                if (effectProperty.managedReferenceValue != null)
                    DrawManagedReferenceChildren(effectProperty);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);

            return false;
        }

        private void DrawTypeDropdown(SerializedProperty effectProperty)
        {
            Type currentType = effectProperty.managedReferenceValue?.GetType();
            int currentTypeIndex = 0;
            if (currentType != null)
            {
                int found = Array.IndexOf(effectTypes, currentType);
                if (found >= 0)
                    currentTypeIndex = found + 1;
            }

            int selectedTypeIndex = EditorGUILayout.Popup("Type", currentTypeIndex, effectTypeNames);
            if (selectedTypeIndex == currentTypeIndex)
                return;

            effectProperty.managedReferenceValue = selectedTypeIndex == 0
                ? null
                : Activator.CreateInstance(effectTypes[selectedTypeIndex - 1]);
        }

        private void ShowAddEffectMenu(SerializedProperty effectsProperty)
        {
            GenericMenu menu = new GenericMenu();
            foreach (Type effectType in effectTypes)
            {
                string effectsPath = effectsProperty.propertyPath;
                menu.AddItem(new GUIContent(effectType.Name), false, () => AddEffect(effectsPath, effectType));
            }
            menu.ShowAsContext();
        }

        private void AddEffect(string effectsPath, Type effectType)
        {
            serializedObject.Update();

            SerializedProperty effectsProperty = serializedObject.FindProperty(effectsPath);
            int index = effectsProperty.arraySize;
            effectsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty effectProperty = effectsProperty.GetArrayElementAtIndex(index);
            effectProperty.managedReferenceValue = Activator.CreateInstance(effectType);
            effectProperty.isExpanded = true;

            serializedObject.ApplyModifiedProperties();
        }

        private void AddImpact()
        {
            int index = impactsProperty.arraySize;
            impactsProperty.InsertArrayElementAtIndex(index);

            SerializedProperty impactProperty = impactsProperty.GetArrayElementAtIndex(index);
            impactProperty.FindPropertyRelative("name").stringValue = string.Empty;
            impactProperty.FindPropertyRelative("effects").arraySize = 0;
            impactProperty.isExpanded = true;
        }

        private void AddImpact(ImpactSet.Impact source)
        {
            AddImpact();
            SerializedProperty impactProperty = impactsProperty.GetArrayElementAtIndex(impactsProperty.arraySize - 1);
            PasteImpact(impactProperty, source);
        }

        private static ImpactSet.Impact CopyImpact(SerializedProperty impactProperty)
        {
            ImpactSet.Impact impact = new ImpactSet.Impact();
            impact.name = impactProperty.FindPropertyRelative("name").stringValue;

            SerializedProperty effectsProperty = impactProperty.FindPropertyRelative("effects");
            impact.effects = new Effect[effectsProperty.arraySize];
            for (int i = 0; i < effectsProperty.arraySize; i++)
                impact.effects[i] = CloneEffect((Effect)effectsProperty.GetArrayElementAtIndex(i).managedReferenceValue);

            return impact;
        }

        private static void PasteImpact(SerializedProperty impactProperty, ImpactSet.Impact source)
        {
            impactProperty.FindPropertyRelative("name").stringValue = source.name;

            SerializedProperty effectsProperty = impactProperty.FindPropertyRelative("effects");
            effectsProperty.arraySize = source.effects.Length;
            for (int i = 0; i < effectsProperty.arraySize; i++)
                effectsProperty.GetArrayElementAtIndex(i).managedReferenceValue = CloneEffect(source.effects[i]);
        }

        private static Effect CloneEffect(Effect source)
        {
            if (source == null)
                return null;

            Effect clone = (Effect)Activator.CreateInstance(source.GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            return clone;
        }

        private static void DrawFieldsExcept(SerializedProperty property, string excludedFieldName)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.name == excludedFieldName)
                {
                    enterChildren = false;
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        private static void DrawManagedReferenceChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }
    }
}
#endif
