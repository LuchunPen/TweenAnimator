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
        private const string NodesPropName = "_nodes";
        private const string StatePropName = "_state";
        private const string PlayOnStartPropName = "_playOnStart";
        private const string UnscaledTimePropName = "_useUnscaledTime";
        private const string LoopModePropName = "_loopMode";
        private const string LoopsPropName = "_loops";
        private const string RootFieldName = "_root";
        private const string ClipsPropName = "_clips";
        private const string NameFieldName = "_name";
        private const string TweenFieldName = "_tween";
        private const string InstantFieldName = "_instant";
        private const string DurationFieldName = "_duration";
        private const string DelayFieldName = "_delay";
        private const string DragDataKey = "TweenTreeNodeDrag";
        private const string RenameControlName = "TweenRenameField";
        private const float IndentWidth = 14f;
        private const float RowHeight = 18f;
        private const float DurationColumnWidth = 54f;
        private const float IconColumnWidth = 16f;

        private enum DropZone { Before, After, Inside }

        private TweenPlayer _target;
        private SerializedObject _serializedObject;
        private SerializedProperty _clipsProp;
        private SerializedProperty _rootProp;   // selected clip's root, recomputed each OnGUI
        private int _selectedClipIndex;

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
        private GUIContent _missingIcon;

        // Cut/copy/paste clipboard for node branches (a deep-cloned subtree). Session-only.
        private static TweenNode s_clipboard;

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
            _clipsProp = _serializedObject != null ? _serializedObject.FindProperty(ClipsPropName) : null;
            _selectedClipIndex = 0;
            UpdateRootProp();
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
                _clipsProp = _serializedObject.FindProperty(ClipsPropName);
                UpdateRootProp();
            }
            else if (!_locked)
            {
                GameObject go = Selection.activeGameObject;
                SetTarget(go != null ? go.GetComponent<TweenPlayer>() : null);
            }

            Repaint();
        }

        /// <summary>Point <see cref="_rootProp"/> at the selected clip's root (or null if no clips).</summary>
        private void UpdateRootProp()
        {
            _rootProp = null;
            if (_clipsProp == null) { return; }

            int count = _clipsProp.arraySize;
            if (count == 0) { _selectedClipIndex = 0; return; }

            _selectedClipIndex = Mathf.Clamp(_selectedClipIndex, 0, count - 1);
            _rootProp = _clipsProp.GetArrayElementAtIndex(_selectedClipIndex).FindPropertyRelative(RootFieldName);
        }

        private SerializedProperty SelectedClipProp()
        {
            if (_clipsProp == null || _selectedClipIndex < 0 || _selectedClipIndex >= _clipsProp.arraySize)
            {
                return null;
            }
            return _clipsProp.GetArrayElementAtIndex(_selectedClipIndex);
        }

        private TweenClip SelectedClipObject()
        {
            if (_target == null) { return null; }

            IReadOnlyList<TweenClip> clips = _target.Clips;
            if (_selectedClipIndex < 0 || _selectedClipIndex >= clips.Count) { return null; }
            return clips[_selectedClipIndex];
        }

        private string SelectedClipName()
        {
            SerializedProperty clip = SelectedClipProp();
            return clip != null ? clip.FindPropertyRelative(NameFieldName).stringValue : null;
        }

        private string RootPath()
        {
            return _rootProp != null ? _rootProp.propertyPath : null;
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
            UpdateRootProp();

            DrawClipBar();

            if (_clipsProp == null || _clipsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No clips yet. Add a clip to start building an animation.", MessageType.Info);
            }
            else
            {
                HandleShortcuts();
                DrawClipOptions();

                EditorGUILayout.BeginHorizontal();
                DrawTreePanel();
                DrawInspectorPanel();
                EditorGUILayout.EndHorizontal();
            }

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

            // Keep the progress bar / Play highlight smooth while a clip is playing.
            if (Application.isPlaying)
            {
                TweenClip clip = SelectedClipObject();
                if (clip != null && clip.IsPlaying) { Repaint(); }
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

        /// <summary>Keyboard: Delete removes the selected node; Ctrl+C/X/V copy/cut/paste a branch.
        /// The root is not deletable. Ignored while typing / renaming.</summary>
        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) { return; }
            if (EditorGUIUtility.editingTextField || !string.IsNullOrEmpty(_renamePath)) { return; }

            bool hasSel = !string.IsNullOrEmpty(_selectedPath);
            bool rootSel = hasSel && _selectedIndex < 0;

            if (e.keyCode == KeyCode.Delete)
            {
                if (hasSel && !rootSel) { DeleteSelection(); e.Use(); }
                return;
            }

            if (!e.control && !e.command) { return; }

            if (e.keyCode == KeyCode.C && hasSel)
            {
                CopyToClipboard(_serializedObject, _selectedPath);
                e.Use();
            }
            else if (e.keyCode == KeyCode.X && hasSel && !rootSel)
            {
                string p = _selectedPath, pp = _selectedParentPath, rp = RootPath();
                int idx = _selectedIndex;
                _pendingChange = () => PerformChange(so =>
                {
                    CopyToClipboard(so, p);
                    DeleteNode(so, pp, idx, rp);
                    ClearSelection();
                });
                e.Use();
            }
            else if (e.keyCode == KeyCode.V && s_clipboard != null)
            {
                string p = _selectedPath, pp = _selectedParentPath, rp = RootPath();
                int idx = _selectedIndex;
                _pendingChange = () => PerformChange(so => DoPaste(so, p, pp, idx, rp));
                e.Use();
            }
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

            string clipName = SelectedClipName();
            bool clipPlaying = Application.isPlaying && _target != null
                && !string.IsNullOrEmpty(clipName) && _target.IsPlaying(clipName);
            bool clipPaused = clipPlaying && _target.IsPaused(clipName);

            using (new EditorGUI.DisabledScope(_target == null || !Application.isPlaying || string.IsNullOrEmpty(clipName)))
            {
                Color prevBg = GUI.backgroundColor;
                if (clipPlaying && !clipPaused) { GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f); }
                if (GUILayout.Button("Play", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _target.ResetAnimation(clipName);
                    _target.Play(clipName);
                }
                GUI.backgroundColor = prevBg;

                using (new EditorGUI.DisabledScope(!clipPlaying))
                {
                    if (clipPaused) { GUI.backgroundColor = new Color(0.9f, 0.8f, 0.35f); }
                    if (GUILayout.Button(clipPaused ? "Resume" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        if (clipPaused) { _target.Resume(clipName); }
                        else { _target.Pause(clipName); }
                    }
                    GUI.backgroundColor = prevBg;
                }

                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _target.Stop(clipName);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawClipBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int count = _clipsProp != null ? _clipsProp.arraySize : 0;
            if (count > 0)
            {
                string[] names = new string[count];
                for (int i = 0; i < count; i++)
                {
                    string clipName = _clipsProp.GetArrayElementAtIndex(i).FindPropertyRelative(NameFieldName).stringValue;
                    names[i] = string.IsNullOrEmpty(clipName) ? $"Clip {i}" : clipName;
                }

                GUILayout.Label("Clip", GUILayout.Width(30f));
                int newIndex = EditorGUILayout.Popup(_selectedClipIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(170f));
                if (newIndex != _selectedClipIndex)
                {
                    _selectedClipIndex = newIndex;
                    UpdateRootProp();
                    ClearSelection();
                }
            }
            else
            {
                GUILayout.Label("No clips", EditorStyles.miniLabel, GUILayout.Width(170f));
            }

            if (GUILayout.Button("+ Clip", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                _pendingChange = AddClip;
            }
            using (new EditorGUI.DisabledScope(count == 0))
            {
                if (GUILayout.Button("- Clip", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    _pendingChange = RemoveSelectedClip;
                }
            }

            TweenClip clip = SelectedClipObject();
            if (Application.isPlaying && clip != null && clip.IsPlaying)
            {
                GUILayout.Space(10f);
                Rect barRect = GUILayoutUtility.GetRect(220f, 14f, GUILayout.Width(220f));
                barRect.y += 2f;
                EditorGUI.ProgressBar(barRect, clip.Progress,
                    $"{clip.Elapsed.ToString("0.0", CultureInfo.InvariantCulture)} / {clip.Duration.ToString("0.0", CultureInfo.InvariantCulture)}s");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawClipOptions()
        {
            SerializedProperty clip = SelectedClipProp();
            if (clip == null) { return; }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("Name", GUILayout.Width(40f));
            EditorGUILayout.PropertyField(clip.FindPropertyRelative(NameFieldName), GUIContent.none, GUILayout.Width(150f));

            GUILayout.Space(12f);
            ToggleLeftClipProp(clip, PlayOnStartPropName, new GUIContent("Play On Start"), 110f);
            GUILayout.Space(8f);
            ToggleLeftClipProp(clip, UnscaledTimePropName,
                new GUIContent("Unscaled Time", "Keep playing while Time.timeScale = 0 (gameplay pause)."), 130f);
            GUILayout.Space(8f);

            SerializedProperty loopMode = clip.FindPropertyRelative(LoopModePropName);
            if (loopMode != null)
            {
                GUILayout.Label("Loop", GUILayout.Width(34f));
                EditorGUILayout.PropertyField(loopMode, GUIContent.none, GUILayout.Width(84f));

                if (loopMode.enumValueIndex != 0) // 0 == TweenLoopMode.None
                {
                    SerializedProperty loops = clip.FindPropertyRelative(LoopsPropName);
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

        private void ToggleLeftClipProp(SerializedProperty clip, string relativeName, GUIContent label, float width)
        {
            SerializedProperty prop = clip.FindPropertyRelative(relativeName);
            if (prop == null) { return; }

            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.ToggleLeft(label, prop.boolValue, GUILayout.Width(width));
            if (EditorGUI.EndChangeCheck())
            {
                prop.boolValue = value;
            }
        }

        private void AddClip()
        {
            PerformChange(so =>
            {
                SerializedProperty clips = so.FindProperty(ClipsPropName);
                int i = clips.arraySize;
                clips.InsertArrayElementAtIndex(i);

                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                clip.FindPropertyRelative(NameFieldName).stringValue = $"Clip {i + 1}";
                clip.FindPropertyRelative(RootFieldName).managedReferenceValue = new TweenParallel { Name = "Root" };
                clip.FindPropertyRelative(PlayOnStartPropName).boolValue = false;
                clip.FindPropertyRelative(UnscaledTimePropName).boolValue = false;
                clip.FindPropertyRelative(LoopModePropName).enumValueIndex = 0;
                clip.FindPropertyRelative(LoopsPropName).intValue = -1;

                _selectedClipIndex = i;
                SelectRootNode(clip.FindPropertyRelative(RootFieldName).propertyPath);
            });
        }

        private void RemoveSelectedClip()
        {
            PerformChange(so =>
            {
                SerializedProperty clips = so.FindProperty(ClipsPropName);
                if (_selectedClipIndex < 0 || _selectedClipIndex >= clips.arraySize) { return; }

                clips.DeleteArrayElementAtIndex(_selectedClipIndex);
                if (_selectedClipIndex >= clips.arraySize) { _selectedClipIndex = clips.arraySize - 1; }
                ClearSelection();
            });
        }

        private void DrawTreePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_treeWidth));
            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, "box");

            if (_rootProp.managedReferenceValue == null)
            {
                EditorGUILayout.LabelField("Root is empty.", EditorStyles.miniLabel);
                string rootPath = RootPath();
                if (GUILayout.Button("Set Root Node", GUILayout.Width(120)))
                {
                    TweenNodeTypeMenu.Show(false, type => PerformChange(so =>
                    {
                        AssignManagedReference(so, rootPath, type);
                        SelectRootNode(rootPath);
                    }));
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

            float x = rowRect.x + IconColumnWidth + depth * IndentWidth;

            if (isGroup)
            {
                Rect foldoutRect = new Rect(x, rowRect.y, 14f, rowRect.height);
                nodeProp.isExpanded = EditorGUI.Foldout(foldoutRect, nodeProp.isExpanded, GUIContent.none);
            }

            Rect durationRect = new Rect(rowRect.xMax - DurationColumnWidth, rowRect.y, DurationColumnWidth, rowRect.height);
            Rect labelRect = new Rect(x + 14f, rowRect.y, durationRect.x - (x + 14f), rowRect.height);

            // Missing-target icon lives in a fixed left gutter, so it never shifts the name label.
            if (_renamePath != nodeProp.propertyPath && HasMissingTarget(nodeProp))
            {
                Rect iconRect = new Rect(rowRect.x, rowRect.y + 1f, 16f, 16f);
                GUI.Label(iconRect, MissingTargetIcon());
            }

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

            TweenClip clip = SelectedClipObject();
            if (clip == null) { return; }

            Undo.RegisterCompleteObjectUndo(_target, "Move Tween Node");
            if (MoveNode(clip, source, target, zone))
            {
                EditorUtility.SetDirty(_target);
            }

            RefreshTarget();
            ClearSelection();
        }

        // ---- object-graph move (reference-based, avoids SerializedProperty index shifts) ----

        private static bool MoveNode(TweenClip clip, TweenNode source, TweenNode target, DropZone zone)
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
                if (!LocateContainer(clip, target, out List<TweenNode> targetList, out int targetIndex, out _)
                    || targetList == null)
                {
                    return false; // target is the root (no sibling list)
                }
                destList = targetList;
                destIndex = zone == DropZone.After ? targetIndex + 1 : targetIndex;
            }

            if (!LocateContainer(clip, source, out List<TweenNode> srcList, out int srcIndex, out bool srcIsRoot))
            {
                return false;
            }

            if (srcIsRoot)
            {
                SetRoot(clip, null);
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

        private static bool LocateContainer(TweenClip clip, TweenNode node, out List<TweenNode> list, out int index, out bool isRoot)
        {
            list = null;
            index = -1;
            isRoot = false;

            if (clip.Root == node) { isRoot = true; return true; }
            return LocateIn(clip.Root, node, ref list, ref index);
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

        private static void SetRoot(TweenClip clip, TweenNode value)
        {
            FieldInfo field = typeof(TweenClip).GetField(RootFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) { field.SetValue(clip, value); }
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
            bool isRootSelected = hasSelection && _selectedIndex < 0;

            using (new EditorGUI.DisabledScope(!hasSelection || isRootSelected))
            {
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteSelection();
                }
            }

            using (new EditorGUI.DisabledScope(!hasSelection || _selectedIndex < 0))
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

                SerializedProperty instantProp = selected.FindPropertyRelative(InstantFieldName);
                bool instant = instantProp != null && instantProp.boolValue;

                foreach (SerializedProperty child in EnumerateEditableChildren(selected))
                {
                    // When Instant is on, timing params are ignored — hide them to avoid confusion.
                    if (instant && child.name == TweenFieldName) { continue; }

                    if (child.propertyType == SerializedPropertyType.ObjectReference && child.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            $"Assign a target for \"{child.displayName}\" — playing without it throws a NullReferenceException.",
                            MessageType.Error);
                    }

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
                string rootPath = RootPath();
                TweenNodeTypeMenu.Show(false, type => PerformChange(so =>
                {
                    AssignManagedReference(so, rootPath, type);
                    SelectRootNode(rootPath);
                }));
                return;
            }

            string parentPath = _selectedPath;
            TweenNodeTypeMenu.Show(false, type => PerformChange(so => AddChild(so, parentPath, type)));
        }

        private void DeleteSelection()
        {
            string parentPath = _selectedParentPath;
            int index = _selectedIndex;
            string rootPath = RootPath();

            _pendingChange = () => PerformChange(so =>
            {
                DeleteNode(so, parentPath, index, rootPath);
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

        private static void DeleteNode(SerializedObject so, string parentPath, int index, string rootPath)
        {
            if (index < 0 || string.IsNullOrEmpty(parentPath))
            {
                // Root node.
                AssignManagedReference(so, rootPath, null);
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
            string rootPath = RootPath();

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

            bool isRoot = parentList == null;

            if (!isRoot)
            {
                menu.AddItem(new GUIContent("Duplicate"), false,
                    () => PerformChange(so => DuplicateNode(so, parentPath, capturedIndex)));
            }

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Copy"), false, () => CopyToClipboard(_serializedObject, path));

            if (!isRoot)
            {
                menu.AddItem(new GUIContent("Cut"), false, () => PerformChange(so =>
                {
                    CopyToClipboard(so, path);
                    DeleteNode(so, parentPath, capturedIndex, rootPath);
                    ClearSelection();
                }));
            }

            if (s_clipboard != null)
            {
                menu.AddItem(new GUIContent("Paste"), false,
                    () => PerformChange(so => DoPaste(so, path, parentPath, capturedIndex, rootPath)));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            if (!isRoot)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete"), false, () => PerformChange(so =>
                {
                    DeleteNode(so, parentPath, capturedIndex, rootPath);
                    ClearSelection();
                }));
            }

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

        private static void CopyToClipboard(SerializedObject so, string path)
        {
            TweenNode node = so.FindProperty(path)?.managedReferenceValue as TweenNode;
            s_clipboard = node != null ? DeepClone(node) as TweenNode : null;
        }

        /// <summary>
        /// Paste a deep clone of the clipboard branch relative to the given node: into it when it's
        /// a group, after it when it's a leaf, or into the root's children as a fallback.
        /// </summary>
        private void DoPaste(SerializedObject so, string selPath, string selParentPath, int selIndex, string rootPath)
        {
            if (s_clipboard == null) { return; }

            SerializedProperty targetList;
            int insertAt;

            SerializedProperty sel = string.IsNullOrEmpty(selPath) ? null : so.FindProperty(selPath);
            if (sel != null && sel.FindPropertyRelative(NodesPropName) != null)
            {
                targetList = sel.FindPropertyRelative(NodesPropName);
                insertAt = targetList.arraySize;
                sel.isExpanded = true;
            }
            else if (sel != null && !string.IsNullOrEmpty(selParentPath))
            {
                targetList = so.FindProperty(selParentPath);
                insertAt = selIndex + 1;
            }
            else
            {
                SerializedProperty root = so.FindProperty(rootPath);
                targetList = root != null ? root.FindPropertyRelative(NodesPropName) : null;
                if (root != null) { root.isExpanded = true; }
                insertAt = targetList != null ? targetList.arraySize : 0;
            }

            if (targetList == null) { return; }

            insertAt = Mathf.Clamp(insertAt, 0, targetList.arraySize);
            if (insertAt == targetList.arraySize) { targetList.arraySize++; }
            else { targetList.InsertArrayElementAtIndex(insertAt); }

            SerializedProperty element = targetList.GetArrayElementAtIndex(insertAt);
            element.managedReferenceValue = DeepClone(s_clipboard);

            _selectedPath = element.propertyPath;
            _selectedParentPath = targetList.propertyPath;
            _selectedIndex = insertAt;
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

        private void AddChild(SerializedObject so, string groupPath, Type type)
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

            // Select the freshly added node so it's easy to find and edit.
            _selectedPath = element.propertyPath;
            _selectedParentPath = list.propertyPath;
            _selectedIndex = newIndex;
        }

        private void SelectRootNode(string rootPath)
        {
            _selectedPath = rootPath;
            _selectedParentPath = null;
            _selectedIndex = -1;
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

        private GUIContent MissingTargetIcon()
        {
            if (_missingIcon == null)
            {
                _missingIcon = new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml"))
                {
                    tooltip = "A target object is not assigned — playing will throw a NullReferenceException."
                };
            }
            return _missingIcon;
        }

        /// <summary>
        /// True if this node (or, for groups, any descendant) is a leaf with an unassigned
        /// object-reference target. Event-only leaves (SetBool/Trigger) have no such field.
        /// </summary>
        private static bool HasMissingTarget(SerializedProperty nodeProp)
        {
            object value = nodeProp != null ? nodeProp.managedReferenceValue : null;
            if (value == null) { return false; }

            SerializedProperty nodes = nodeProp.FindPropertyRelative(NodesPropName);
            if (nodes != null)
            {
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    if (HasMissingTarget(nodes.GetArrayElementAtIndex(i))) { return true; }
                }
                return false;
            }

            foreach (SerializedProperty child in EnumerateEditableChildren(nodeProp))
            {
                if (child.propertyType == SerializedPropertyType.ObjectReference && child.objectReferenceValue == null)
                {
                    return true;
                }
            }
            return false;
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

            SerializedProperty instant = nodeProp.FindPropertyRelative(InstantFieldName);
            if (instant != null && instant.boolValue) { return 0f; }

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
