using System;
using System.Collections.Generic;
using System.Reflection;
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
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == StateFieldName) { continue; }
                yield return iterator.Copy();
            }
        }

        private static GUIContent GetCurrentTypeLabel(SerializedProperty property)
        {
            object value = property.managedReferenceValue;
            if (value == null) { return new GUIContent("(None)"); }

            Type type = value.GetType();
            TweenNodeMenuAttribute menu = type.GetCustomAttribute<TweenNodeMenuAttribute>();
            if (menu != null && !string.IsNullOrEmpty(menu.Path))
            {
                int slash = menu.Path.LastIndexOf('/');
                return new GUIContent(slash >= 0 ? menu.Path.Substring(slash + 1) : menu.Path);
            }

            return new GUIContent(ObjectNames.NicifyVariableName(type.Name));
        }

        private static void ShowTypeMenu(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            bool isNull = property.managedReferenceValue == null;

            menu.AddItem(new GUIContent("(None)"), isNull, () => AssignType(serializedObject, path, null));
            menu.AddSeparator(string.Empty);

            foreach (Type type in GetNodeTypes())
            {
                Type captured = type;
                menu.AddItem(new GUIContent(GetMenuPath(type)), false, () => AssignType(serializedObject, path, captured));
            }

            menu.ShowAsContext();
        }

        private static void AssignType(SerializedObject serializedObject, string path, Type type)
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.FindProperty(path);
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.isExpanded = true;

            serializedObject.ApplyModifiedProperties();
        }

        private static List<Type> GetNodeTypes()
        {
            List<Type> types = new List<Type>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<TweenNode>())
            {
                if (type.IsAbstract || type.IsGenericType || !type.IsClass) { continue; }
                types.Add(type);
            }

            types.Sort((a, b) => string.Compare(GetMenuPath(a), GetMenuPath(b), StringComparison.Ordinal));
            return types;
        }

        private static string GetMenuPath(Type type)
        {
            TweenNodeMenuAttribute menu = type.GetCustomAttribute<TweenNodeMenuAttribute>();
            if (menu != null && !string.IsNullOrEmpty(menu.Path))
            {
                return menu.Path;
            }

            string category = typeof(TweenAnimation).IsAssignableFrom(type) ? "Animations" : "Groups";
            return category + "/" + ObjectNames.NicifyVariableName(type.Name);
        }
    }
}
