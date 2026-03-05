#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ImpactFeel
{
    [CustomPropertyDrawer(typeof(Curve3))]
    public class Curve3Drawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new Rect(position.x, position.y, position.width, line);
            bool hideRepeats = ShouldHideRepeats(property);
            if (hideRepeats)
                property.FindPropertyRelative("repeats").intValue = 1;

            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }

            EditorGUI.indentLevel++;
            SerializedProperty useSingleCurve = property.FindPropertyRelative("useSingleCurve");
            DrawRow(ref row, gap, useSingleCurve);

            if (useSingleCurve.boolValue)
                DrawRow(ref row, gap, property.FindPropertyRelative("curve"));
            else
            {
                DrawRow(ref row, gap, property.FindPropertyRelative("xCurve"));
                DrawRow(ref row, gap, property.FindPropertyRelative("yCurve"));
                DrawRow(ref row, gap, property.FindPropertyRelative("zCurve"));
            }

            DrawOtherFields(ref row, gap, property, hideRepeats);
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            if (!property.isExpanded) return line;

            float height = line; // foldout
            bool single = property.FindPropertyRelative("useSingleCurve").boolValue;
            bool hideRepeats = ShouldHideRepeats(property);

            height += gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("useSingleCurve"), true);
            height += gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(single ? "curve" : "xCurve"), true);
            if (!single)
            {
                height += gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("yCurve"), true);
                height += gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("zCurve"), true);
            }

            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (IsCurveField(iterator.name) || hideRepeats && iterator.name == "repeats")
                    continue;
                height += gap + EditorGUI.GetPropertyHeight(iterator, true);
            }

            return height;
        }

        private static void DrawRow(ref Rect row, float gap, SerializedProperty field)
        {
            float h = EditorGUI.GetPropertyHeight(field, true);
            row.y += row.height + gap;
            row.height = h;
            EditorGUI.PropertyField(row, field, true);
        }

        private static void DrawOtherFields(ref Rect row, float gap, SerializedProperty property, bool hideRepeats)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (IsCurveField(iterator.name) || hideRepeats && iterator.name == "repeats")
                    continue;
                DrawRow(ref row, gap, iterator);
            }
        }

        private static bool ShouldHideRepeats(SerializedProperty property) => property.FindPropertyRelative("repeatMode").enumValueIndex == 0;

        private static bool IsCurveField(string name)
        {
            return name == "useSingleCurve"
                || name == "curve"
                || name == "xCurve"
                || name == "yCurve"
                || name == "zCurve";
        }
    }
}
#endif
