using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Inspector drawer for any [SerializeReference] <see cref="TweenNode"/> field (and list
    /// element). Adds a type-picker dropdown whose entries are gathered automatically via
    /// <see cref="TypeCache"/>, so new node classes appear without any manual registration.
    /// This is the minimal editing UI; the tree/graph editor will be layered on top later.
    /// </summary>
    [CustomPropertyDrawer(typeof(TweenNode), true)]
    public class TweenNodeDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const string StateFieldName = "_state";
        private const string InstantFieldName = "_instant";
        private const string TweenFieldName = "_tween";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            bool hasValue = property.managedReferenceValue != null;

            Rect foldoutRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, line);
            if (hasValue)
            {
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            }
            else
            {
                EditorGUI.LabelField(foldoutRect, label);
            }

            Rect dropdownRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                position.y,
                position.width - EditorGUIUtility.labelWidth,
                line);

            if (GUI.Button(dropdownRect, GetCurrentTypeLabel(property), EditorStyles.popup))
            {
                ShowTypeMenu(property);
            }

            if (hasValue && property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + line + Spacing;

                foreach (SerializedProperty child in EnumerateChildren(property))
                {
                    float height = EditorGUI.GetPropertyHeight(child, true);
                    Rect childRect = new Rect(position.x, y, position.width, height);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += height + Spacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.managedReferenceValue != null && property.isExpanded)
            {
                foreach (SerializedProperty child in EnumerateChildren(property))
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
                }
            }

            return height;
        }

        private static IEnumerable<SerializedProperty> EnumerateChildren(SerializedProperty property)
        {
            SerializedProperty instant = property.FindPropertyRelative(InstantFieldName);
            bool hideTiming = instant != null && instant.boolValue;

            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == StateFieldName) { continue; }
                if (hideTiming && iterator.name == TweenFieldName) { continue; } // Instant ignores timing params
                yield return iterator.Copy();
            }
        }

        private static GUIContent GetCurrentTypeLabel(SerializedProperty property)
        {
            object value = property.managedReferenceValue;
            if (value == null) { return new GUIContent("(None)"); }

            return new GUIContent(TweenNodeTypeMenu.GetDisplayName(value.GetType()));
        }

        private static void ShowTypeMenu(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;

            TweenNodeTypeMenu.Show(true, type => AssignType(serializedObject, path, type));
        }

        private static void AssignType(SerializedObject serializedObject, string path, Type type)
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.FindProperty(path);
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.isExpanded = true;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
