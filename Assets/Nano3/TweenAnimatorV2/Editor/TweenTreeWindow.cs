using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        private const string RootFieldName = "_root";
        private const string NameFieldName = "_name";
        private const string TweenFieldName = "_tween";
        private const string DurationFieldName = "_duration";
        private const string DelayFieldName = "_delay";
        private const string DragDataKey = "TweenTreeNodeDrag";
        private const string RenameControlName = "TweenRenameField";
        private const float IndentWidth = 14f;
        private const float RowHeight = 18f;
        private const float DurationColumnWidth = 54f;

        private enum DropZone { Before, After, Inside }

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

        // Drag-and-drop reorder state.
        private string _dragPath;                 // node being dragged (candidate + active)
        private string _dropTargetPath;           // row currently hovered as a drop target
        private DropZone _dropZone;

        // Inline rename state.
        private string _renamePath;
        private bool _renameFocusPending;

        private GUIStyle _durationStyle;

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
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Domain reload / scene swap on play<->edit transitions invalidates the cached
            // SerializedObject (and may destroy the target). Re-acquire when settled.
            if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshTarget();
            }
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

        /// <summary>
        /// Rebuild the SerializedObject after a domain reload / play-mode swap. Keeps the current
        /// target if it is still alive; otherwise re-acquires from the current selection.
        /// </summary>
        private void RefreshTarget()
        {
            if (_target != null)
            {
                _serializedObject = new SerializedObject(_target);
                _rootProp = _serializedObject.FindProperty(RootPropName);
            }
            else if (!_locked)
            {
                GameObject go = Selection.activeGameObject;
                SetTarget(go != null ? go.GetComponent<TweenPlayer>() : null);
            }

            Repaint();
        }

        private void ClearSelection()
        {
            _selectedPath = null;
            _selectedParentPath = null;
            _selectedIndex = -1;
        }

        private void OnGUI()
        {
            // The target may have been destroyed by a scene/domain swap while the window kept a
            // stale SerializedObject; recover before drawing.
            if (_serializedObject != null && _serializedObject.targetObject == null)
            {
                RefreshTarget();
            }

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

            Rect durationRect = new Rect(rowRect.xMax - DurationColumnWidth, rowRect.y, DurationColumnWidth, rowRect.height);
            Rect labelRect = new Rect(x + 14f, rowRect.y, durationRect.x - (x + 14f), rowRect.height);

            if (_renamePath == nodeProp.propertyPath)
            {
                DrawRenameField(labelRect, nodeProp);
            }
            else
            {
                GUI.Label(labelRect, GetNodeLabel(nodeProp, nodes));
            }

            GUI.Label(durationRect, FormatDuration(GetNodeDuration(nodeProp)), DurationStyle());

            HandleRowEvents(rowRect, nodeProp, parentList, index, isGroup);
            DrawDropIndicator(rowRect, nodeProp.propertyPath);

            if (isGroup && nodeProp.isExpanded)
            {
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    DrawNodeRow(nodes.GetArrayElementAtIndex(i), depth + 1, nodes, i);
                }
            }
        }

        private void HandleRowEvents(Rect rowRect, SerializedProperty nodeProp, SerializedProperty parentList, int index, bool isGroup)
        {
            Event e = Event.current;
            bool over = rowRect.Contains(e.mousePosition);
            string path = nodeProp.propertyPath;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (over)
                    {
                        // Clicking a different row ends any in-progress rename.
                        if (!string.IsNullOrEmpty(_renamePath) && _renamePath != path)
                        {
                            _renamePath = null;
                            GUI.FocusControl(null);
                        }

                        Select(nodeProp, parentList, index);
                        if (e.button == 1)
                        {
                            ShowRowContextMenu(nodeProp, parentList, index, isGroup);
                            e.Use();
                        }
                        else if (e.button == 0)
                        {
                            if (e.clickCount == 2)
                            {
                                _renamePath = path; // start inline rename
                                _renameFocusPending = true;
                                e.Use();
                            }
                            else
                            {
                                _dragPath = path; // candidate; a real drag starts on MouseDrag
                            }
                        }
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (over && _dragPath == path)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.SetGenericData(DragDataKey, path);
                        DragAndDrop.objectReferences = new UnityEngine.Object[0];
                        DragAndDrop.StartDrag("Tween Node");
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    _dragPath = null;
                    break;

                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (over && IsNodeDrag())
                    {
                        DropZone zone = GetDropZone(rowRect, e.mousePosition, isGroup);
                        string sourcePath = DragAndDrop.GetGenericData(DragDataKey) as string;
                        bool valid = CanDrop(sourcePath, path, zone);

                        DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
                        _dropTargetPath = valid ? path : null;
                        _dropZone = zone;

                        if (e.type == EventType.DragPerform && valid)
                        {
                            DragAndDrop.AcceptDrag();
                            string targetPath = path;
                            _pendingChange = () => ApplyDrop(sourcePath, targetPath, zone);
                            _dropTargetPath = null;
                            _dragPath = null;
                        }
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.DragExited:
                    _dropTargetPath = null;
                    _dragPath = null;
                    Repaint();
                    break;
            }
        }

        private void DrawDropIndicator(Rect rowRect, string path)
        {
            if (Event.current.type != EventType.Repaint || path != _dropTargetPath) { return; }

            Color color = new Color(0.24f, 0.55f, 0.95f, 1f);
            if (_dropZone == DropZone.Inside)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.55f, 0.95f, 0.25f));
            }
            else
            {
                float y = _dropZone == DropZone.Before ? rowRect.y : rowRect.yMax - 2f;
                EditorGUI.DrawRect(new Rect(rowRect.x, y, rowRect.width, 2f), color);
            }
        }

        private static bool IsNodeDrag()
        {
            return DragAndDrop.GetGenericData(DragDataKey) is string;
        }

        private static DropZone GetDropZone(Rect rowRect, Vector2 mouse, bool isGroup)
        {
            float t = (mouse.y - rowRect.y) / rowRect.height;
            if (isGroup)
            {
                if (t < 0.25f) { return DropZone.Before; }
                if (t > 0.75f) { return DropZone.After; }
                return DropZone.Inside;
            }
            return t < 0.5f ? DropZone.Before : DropZone.After;
        }

        private bool CanDrop(string sourcePath, string targetPath, DropZone zone)
        {
            if (string.IsNullOrEmpty(sourcePath) || sourcePath == targetPath || _serializedObject == null)
            {
                return false;
            }

            TweenNode source = _serializedObject.FindProperty(sourcePath)?.managedReferenceValue as TweenNode;
            TweenNode target = _serializedObject.FindProperty(targetPath)?.managedReferenceValue as TweenNode;
            if (source == null || target == null || source == target) { return false; }

            if (IsDescendantOrSelf(source, target)) { return false; }
            if (zone == DropZone.Inside && GetChildList(target) == null) { return false; }

            return true;
        }

        private void ApplyDrop(string sourcePath, string targetPath, DropZone zone)
        {
            if (_serializedObject == null || _target == null) { return; }

            TweenNode source = _serializedObject.FindProperty(sourcePath)?.managedReferenceValue as TweenNode;
            TweenNode target = _serializedObject.FindProperty(targetPath)?.managedReferenceValue as TweenNode;
            if (source == null || target == null) { return; }

            Undo.RegisterCompleteObjectUndo(_target, "Move Tween Node");
            if (MoveNode(_target, source, target, zone))
            {
                EditorUtility.SetDirty(_target);
            }

            RefreshTarget();
            ClearSelection();
        }

        // ---- object-graph move (reference-based, avoids SerializedProperty index shifts) ----

        private static bool MoveNode(TweenPlayer player, TweenNode source, TweenNode target, DropZone zone)
        {
            if (source == null || target == null || source == target) { return false; }
            if (IsDescendantOrSelf(source, target)) { return false; }

            List<TweenNode> destList;
            int destIndex;
            if (zone == DropZone.Inside)
            {
                destList = GetChildList(target);
                if (destList == null) { return false; }
                destIndex = destList.Count;
            }
            else
            {
                if (!LocateContainer(player, target, out List<TweenNode> targetList, out int targetIndex, out _)
                    || targetList == null)
                {
                    return false; // target is the root (no sibling list)
                }
                destList = targetList;
                destIndex = zone == DropZone.After ? targetIndex + 1 : targetIndex;
            }

            if (!LocateContainer(player, source, out List<TweenNode> srcList, out int srcIndex, out bool srcIsRoot))
            {
                return false;
            }

            if (srcIsRoot)
            {
                SetRoot(player, null);
            }
            else
            {
                srcList.RemoveAt(srcIndex);
                if (srcList == destList && srcIndex < destIndex) { destIndex--; }
            }

            destIndex = Mathf.Clamp(destIndex, 0, destList.Count);
            destList.Insert(destIndex, source);
            return true;
        }

        private static List<TweenNode> GetChildList(TweenNode node)
        {
            if (node == null) { return null; }

            FieldInfo field = node.GetType().GetField(NodesPropName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? field.GetValue(node) as List<TweenNode> : null;
        }

        private static bool IsDescendantOrSelf(TweenNode source, TweenNode node)
        {
            if (source == node) { return true; }

            List<TweenNode> children = GetChildList(source);
            if (children == null) { return false; }

            for (int i = 0; i < children.Count; i++)
            {
                if (IsDescendantOrSelf(children[i], node)) { return true; }
            }
            return false;
        }

        private static bool LocateContainer(TweenPlayer player, TweenNode node, out List<TweenNode> list, out int index, out bool isRoot)
        {
            list = null;
            index = -1;
            isRoot = false;

            if (player.Root == node) { isRoot = true; return true; }
            return LocateIn(player.Root, node, ref list, ref index);
        }

        private static bool LocateIn(TweenNode current, TweenNode target, ref List<TweenNode> list, ref int index)
        {
            List<TweenNode> children = GetChildList(current);
            if (children == null) { return false; }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == target) { list = children; index = i; return true; }
                if (LocateIn(children[i], target, ref list, ref index)) { return true; }
            }
            return false;
        }

        private static void SetRoot(TweenPlayer player, TweenNode value)
        {
            FieldInfo field = typeof(TweenPlayer).GetField(RootFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) { field.SetValue(player, value); }
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

                SerializedProperty nameProp = selected.FindPropertyRelative(NameFieldName);
                if (nameProp != null)
                {
                    EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                    EditorGUILayout.Space(2);
                }

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
            string newPath = nodeProp.propertyPath;

            // Selecting a different node must drop keyboard focus, otherwise an active text
            // field (e.g. the inspector Name field) keeps its edit buffer on the same control.
            if (newPath != _selectedPath)
            {
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
            }

            _selectedPath = newPath;
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

            SerializedProperty nameProp = nodeProp.FindPropertyRelative(NameFieldName);
            string custom = nameProp != null ? nameProp.stringValue : null;
            string name = string.IsNullOrEmpty(custom)
                ? TweenNodeTypeMenu.GetDisplayName(value.GetType())
                : custom;

            if (nodes != null)
            {
                return $"{name}  ({nodes.arraySize})";
            }
            return name;
        }

        private void DrawRenameField(Rect rect, SerializedProperty nodeProp)
        {
            SerializedProperty nameProp = nodeProp.FindPropertyRelative(NameFieldName);
            if (nameProp == null) { _renamePath = null; return; }

            GUI.SetNextControlName(RenameControlName);
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.DelayedTextField(rect, nameProp.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                nameProp.stringValue = newName;
                _renamePath = null; // DelayedTextField commits on Enter / focus loss
            }

            if (_renameFocusPending)
            {
                EditorGUI.FocusTextInControl(RenameControlName);
                _renameFocusPending = false;
            }

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _renamePath = null;
                GUI.FocusControl(null);
                e.Use();
                Repaint();
            }
        }

        private GUIStyle DurationStyle()
        {
            if (_durationStyle == null)
            {
                _durationStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
                _durationStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            }
            return _durationStyle;
        }

        private static string FormatDuration(float seconds)
        {
            return seconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>
        /// Duration of one pass: a leaf is Delay + Duration; a Sequence sums its children; a
        /// Parallel takes the longest child. Computed recursively up the tree.
        /// </summary>
        private static float GetNodeDuration(SerializedProperty nodeProp)
        {
            object value = nodeProp != null ? nodeProp.managedReferenceValue : null;
            if (value == null) { return 0f; }

            SerializedProperty nodes = nodeProp.FindPropertyRelative(NodesPropName);
            if (nodes != null)
            {
                bool parallel = typeof(TweenParallel).IsAssignableFrom(value.GetType());
                float total = 0f;
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    float child = GetNodeDuration(nodes.GetArrayElementAtIndex(i));
                    total = parallel ? Mathf.Max(total, child) : total + child;
                }
                return total;
            }

            SerializedProperty tween = nodeProp.FindPropertyRelative(TweenFieldName);
            if (tween == null) { return 0f; }

            SerializedProperty delay = tween.FindPropertyRelative(DelayFieldName);
            SerializedProperty duration = tween.FindPropertyRelative(DurationFieldName);
            float delayValue = delay != null ? delay.floatValue : 0f;
            float durationValue = duration != null ? duration.floatValue : 0f;
            return delayValue + durationValue;
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
                if (iterator.name == NameFieldName) { continue; } // shown as a dedicated Name field
                yield return iterator.Copy();
            }
        }
    }
}
