using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Outliner-style editor for a <see cref="TweenPlayer"/> tree. Shows the node hierarchy as a
    /// compact list (left) with add/remove/reorder, and the selected node's parameters (right).
    /// Edits go through a SerializedObject, so Undo/Redo work out of the box.
    /// </summary>
    public class TweenTreeWindow : EditorWindow
    {
        private const string RootPropName = "_root";
        private const string NodesPropName = "_nodes";
        private const string StatePropName = "_state";
        private const string PlayOnStartPropName = "_playOnStart";
        private const string UnscaledTimePropName = "_useUnscaledTime";
        private const string LoopModePropName = "_loopMode";
        private const string LoopsPropName = "_loops";
        private const float IndentWidth = 14f;
        private const float RowHeight = 18f;

        private TweenPlayer _target;
        private SerializedObject _serializedObject;
        private SerializedProperty _rootProp;

        private string _selectedPath;
        private string _selectedParentPath;
        private int _selectedIndex = -1;

        private bool _locked;
        private Vector2 _treeScroll;
        private Vector2 _inspectorScroll;
        private float _treeWidth = 280f;

        // Structural edits are deferred until after the tree is drawn to avoid mutating the
        // SerializedObject mid-layout.
        private Action _pendingChange;

        [MenuItem("Window/Nano3/Tween Tree Editor")]
        public static void Open()
        {
            GetWindow<TweenTreeWindow>("Tween Tree");
        }

        public static void OpenFor(TweenPlayer player)
        {
            TweenTreeWindow window = GetWindow<TweenTreeWindow>("Tween Tree");
            window._locked = false;
            window.SetTarget(player);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnInspectorUpdate()
        {
            // Keep play-mode state colours / external edits fresh.
            Repaint();
        }

        private void OnSelectionChanged()
        {
            if (_locked) { return; }

            GameObject go = Selection.activeGameObject;
            TweenPlayer player = go != null ? go.GetComponent<TweenPlayer>() : null;
            if (player != null && player != _target)
            {
                SetTarget(player);
            }

            Repaint();
        }

        private void SetTarget(TweenPlayer player)
        {
            _target = player;
            _serializedObject = player != null ? new SerializedObject(player) : null;
            _rootProp = _serializedObject != null ? _serializedObject.FindProperty(RootPropName) : null;
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedPath = null;
            _selectedParentPath = null;
            _selectedIndex = -1;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_target == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a TweenPlayer component.", MessageType.Info);
                return;
            }

            _serializedObject.Update();

            DrawPlayerOptions();

            EditorGUILayout.BeginHorizontal();
            DrawTreePanel();
            DrawInspectorPanel();
            EditorGUILayout.EndHorizontal();

            _serializedObject.ApplyModifiedProperties();

            // Direct (footer) structural edits are deferred to here, after all drawing, so we
            // never mutate the tree mid-layout. Menu-driven edits run immediately in their
            // callbacks (which fire outside OnGUI), via PerformChange.
            if (_pendingChange != null)
            {
                Action change = _pendingChange;
                _pendingChange = null;
                change.Invoke();
            }
        }

        /// <summary>Run a structural edit transactionally against the target's SerializedObject.</summary>
        private void PerformChange(Action<SerializedObject> op)
        {
            if (_serializedObject == null) { return; }

            _serializedObject.Update();
            op(_serializedObject);
            _serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            TweenPlayer picked = (TweenPlayer)EditorGUILayout.ObjectField(_target, typeof(TweenPlayer), true, GUILayout.Width(220));
            if (EditorGUI.EndChangeCheck() && picked != _target)
            {
                SetTarget(picked);
            }

            _locked = GUILayout.Toggle(_locked, "Lock", EditorStyles.toolbarButton, GUILayout.Width(48));

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_target == null || !Application.isPlaying))
            {
                if (GUILayout.Button("Play", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _target.ResetAnimation();
                    _target.Play();
                }
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _target.Stop();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerOptions()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            ToggleLeftProperty(PlayOnStartPropName,
                new GUIContent("Play On Start"), 120f);

            GUILayout.Space(12f);

            ToggleLeftProperty(UnscaledTimePropName,
                new GUIContent("Use Unscaled Time", "Keep playing while Time.timeScale = 0 (gameplay pause). Applies to the whole tree."),
                150f);

            GUILayout.Space(12f);

            SerializedProperty loopMode = _serializedObject.FindProperty(LoopModePropName);
            if (loopMode != null)
            {
                GUILayout.Label("Loop", GUILayout.Width(34f));
                EditorGUILayout.PropertyField(loopMode, GUIContent.none, GUILayout.Width(90f));

                if (loopMode.enumValueIndex != 0) // 0 == TweenLoopMode.None
                {
                    SerializedProperty loops = _serializedObject.FindProperty(LoopsPropName);
                    if (loops != null)
                    {
                        GUILayout.Label(new GUIContent("×", "How many extra times to repeat. -1 = infinite."), GUILayout.Width(12f));
                        EditorGUILayout.PropertyField(loops, GUIContent.none, GUILayout.Width(46f));
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ToggleLeftProperty(string propName, GUIContent label, float width)
        {
            SerializedProperty prop = _serializedObject.FindProperty(propName);
            if (prop == null) { return; }

            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.ToggleLeft(label, prop.boolValue, GUILayout.Width(width));
            if (EditorGUI.EndChangeCheck())
            {
                prop.boolValue = value;
            }
        }

        private void DrawTreePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_treeWidth));
            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, "box");

            if (_rootProp.managedReferenceValue == null)
            {
                EditorGUILayout.LabelField("Root is empty.", EditorStyles.miniLabel);
                if (GUILayout.Button("Set Root Node", GUILayout.Width(120)))
                {
                    TweenNodeTypeMenu.Show(false, type => PerformChange(so => AssignManagedReference(so, RootPropName, type)));
                }
            }
            else
            {
                DrawNodeRow(_rootProp, 0, null, -1);
            }

            EditorGUILayout.EndScrollView();
            DrawTreeFooter();
            EditorGUILayout.EndVertical();
        }

        private void DrawNodeRow(SerializedProperty nodeProp, int depth, SerializedProperty parentList, int index)
        {
            SerializedProperty nodes = nodeProp.FindPropertyRelative(NodesPropName);
            bool isGroup = nodes != null;
            bool isSelected = nodeProp.propertyPath == _selectedPath;

            Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
            if (Event.current.type == EventType.Repaint && isSelected)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));
            }

            float x = rowRect.x + depth * IndentWidth;

            if (isGroup)
            {
                Rect foldoutRect = new Rect(x, rowRect.y, 14f, rowRect.height);
                nodeProp.isExpanded = EditorGUI.Foldout(foldoutRect, nodeProp.isExpanded, GUIContent.none);
            }

            Rect labelRect = new Rect(x + 14f, rowRect.y, rowRect.xMax - (x + 14f), rowRect.height);
            string label = GetNodeLabel(nodeProp, nodes);
            GUI.Label(labelRect, label);

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                Select(nodeProp, parentList, index);
                if (Event.current.button == 1)
                {
                    ShowRowContextMenu(nodeProp, parentList, index, isGroup);
                }
                Event.current.Use();
            }

            if (isGroup && nodeProp.isExpanded)
            {
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    DrawNodeRow(nodes.GetArrayElementAtIndex(i), depth + 1, nodes, i);
                }
            }
        }

        private void DrawTreeFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool canAdd = SelectedIsGroupOrEmptyRoot();
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("+ Add", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    AddChildToSelection();
                }
            }

            bool hasSelection = !string.IsNullOrEmpty(_selectedPath);
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteSelection();
                }

                using (new EditorGUI.DisabledScope(_selectedIndex < 0))
                {
                    if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    {
                        MoveSelection(-1);
                    }
                    if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    {
                        MoveSelection(1);
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical();
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            SerializedProperty selected = string.IsNullOrEmpty(_selectedPath)
                ? null
                : _serializedObject.FindProperty(_selectedPath);

            if (selected == null || selected.managedReferenceValue == null)
            {
                EditorGUILayout.LabelField("Select a node to edit its parameters.", EditorStyles.miniLabel);
            }
            else
            {
                object value = selected.managedReferenceValue;
                EditorGUILayout.LabelField(TweenNodeTypeMenu.GetDisplayName(value.GetType()), EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                foreach (SerializedProperty child in EnumerateEditableChildren(selected))
                {
                    EditorGUILayout.PropertyField(child, true);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ---- selection helpers ----

        private void Select(SerializedProperty nodeProp, SerializedProperty parentList, int index)
        {
            _selectedPath = nodeProp.propertyPath;
            _selectedParentPath = parentList != null ? parentList.propertyPath : null;
            _selectedIndex = parentList != null ? index : -1;
        }

        private bool SelectedIsGroupOrEmptyRoot()
        {
            if (_rootProp.managedReferenceValue == null) { return true; }
            if (string.IsNullOrEmpty(_selectedPath)) { return false; }

            SerializedProperty selected = _serializedObject.FindProperty(_selectedPath);
            return selected != null && selected.FindPropertyRelative(NodesPropName) != null;
        }

        // ---- structural operations (deferred via _pendingChange) ----

        private void AddChildToSelection()
        {
            if (_rootProp.managedReferenceValue == null)
            {
                TweenNodeTypeMenu.Show(false, type => PerformChange(so => AssignManagedReference(so, RootPropName, type)));
                return;
            }

            string parentPath = _selectedPath;
            TweenNodeTypeMenu.Show(false, type => PerformChange(so => AddChild(so, parentPath, type)));
        }

        private void DeleteSelection()
        {
            string parentPath = _selectedParentPath;
            int index = _selectedIndex;

            _pendingChange = () => PerformChange(so =>
            {
                DeleteNode(so, parentPath, index);
                ClearSelection();
            });
        }

        private void MoveSelection(int delta)
        {
            string parentPath = _selectedParentPath;
            int index = _selectedIndex;

            _pendingChange = () => PerformChange(so =>
            {
                SerializedProperty list = so.FindProperty(parentPath);
                if (list == null) { return; }

                int target = index + delta;
                if (target < 0 || target >= list.arraySize) { return; }

                list.MoveArrayElement(index, target);
                _selectedIndex = target;
                _selectedPath = list.GetArrayElementAtIndex(target).propertyPath;
            });
        }

        private static void DeleteNode(SerializedObject so, string parentPath, int index)
        {
            if (index < 0 || string.IsNullOrEmpty(parentPath))
            {
                // Root node.
                AssignManagedReference(so, RootPropName, null);
                return;
            }

            SerializedProperty list = so.FindProperty(parentPath);
            if (list != null && index < list.arraySize)
            {
                list.DeleteArrayElementAtIndex(index);
            }
        }

        private void ShowRowContextMenu(SerializedProperty nodeProp, SerializedProperty parentList, int index, bool isGroup)
        {
            string path = nodeProp.propertyPath;
            string parentPath = parentList != null ? parentList.propertyPath : null;
            int capturedIndex = parentList != null ? index : -1;

            GenericMenu menu = new GenericMenu();

            if (isGroup)
            {
                foreach (Type type in TweenNodeTypeMenu.GetNodeTypes())
                {
                    Type captured = type;
                    menu.AddItem(new GUIContent("Add Child/" + TweenNodeTypeMenu.GetMenuPath(type)), false,
                        () => PerformChange(so => AddChild(so, path, captured)));
                }
                menu.AddSeparator(string.Empty);
            }

            foreach (Type type in TweenNodeTypeMenu.GetNodeTypes())
            {
                Type captured = type;
                menu.AddItem(new GUIContent("Change Type/" + TweenNodeTypeMenu.GetMenuPath(type)), false,
                    () => PerformChange(so => AssignManagedReference(so, path, captured)));
            }

            menu.AddSeparator(string.Empty);

            if (parentList != null)
            {
                menu.AddItem(new GUIContent("Duplicate"), false,
                    () => PerformChange(so => DuplicateNode(so, parentPath, capturedIndex)));
            }

            menu.AddItem(new GUIContent("Delete"), false, () => PerformChange(so =>
            {
                DeleteNode(so, parentPath, capturedIndex);
                ClearSelection();
            }));

            menu.ShowAsContext();
        }

        private static void DuplicateNode(SerializedObject so, string parentPath, int index)
        {
            if (string.IsNullOrEmpty(parentPath) || index < 0) { return; }

            SerializedProperty list = so.FindProperty(parentPath);
            if (list == null || index >= list.arraySize) { return; }

            object source = list.GetArrayElementAtIndex(index).managedReferenceValue;
            object clone = DeepClone(source);

            // Insert the clone directly after the source.
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).managedReferenceValue = clone;
            list.MoveArrayElement(index, index + 1);
        }

        /// <summary>
        /// Deep-copies a node. Flat serialized data (values, Object references, TweenData,
        /// UnityEvents) is cloned via EditorJsonUtility; the nested [SerializeReference] child
        /// list of groups is rebuilt recursively so subtypes are preserved.
        /// </summary>
        private static object DeepClone(object source)
        {
            if (source == null) { return null; }

            Type type = source.GetType();
            object clone = Activator.CreateInstance(type);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), clone);

            FieldInfo nodesField = type.GetField(NodesPropName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodesField != null && typeof(IList).IsAssignableFrom(nodesField.FieldType))
            {
                IList sourceList = nodesField.GetValue(source) as IList;
                IList newList = (IList)Activator.CreateInstance(nodesField.FieldType);
                if (sourceList != null)
                {
                    foreach (object child in sourceList)
                    {
                        newList.Add(DeepClone(child));
                    }
                }
                nodesField.SetValue(clone, newList);
            }

            return clone;
        }

        private static void AddChild(SerializedObject so, string groupPath, Type type)
        {
            SerializedProperty group = so.FindProperty(groupPath);
            if (group == null) { return; }

            SerializedProperty list = group.FindPropertyRelative(NodesPropName);
            if (list == null) { return; }

            int newIndex = list.arraySize;
            list.InsertArrayElementAtIndex(newIndex);
            SerializedProperty element = list.GetArrayElementAtIndex(newIndex);
            element.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            group.isExpanded = true;
        }

        private static void AssignManagedReference(SerializedObject so, string path, Type type)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property == null) { return; }

            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.isExpanded = true;
        }

        // ---- labels / iteration ----

        private static string GetNodeLabel(SerializedProperty nodeProp, SerializedProperty nodes)
        {
            object value = nodeProp.managedReferenceValue;
            if (value == null) { return "(None)"; }

            string name = TweenNodeTypeMenu.GetDisplayName(value.GetType());
            if (nodes != null)
            {
                return $"{name}  ({nodes.arraySize})";
            }
            return name;
        }

        private static IEnumerable<SerializedProperty> EnumerateEditableChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == StatePropName) { continue; }
                if (iterator.name == NodesPropName) { continue; }
                yield return iterator.Copy();
            }
        }
    }
}
