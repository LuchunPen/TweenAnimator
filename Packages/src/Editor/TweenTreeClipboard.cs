using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Session-only copy/paste buffer for the tree editor. Holds a deep-cloned node branch and a
    /// deep-cloned whole clip, and owns the deep-clone logic itself. Fully self-contained (no
    /// window state), so clipboard behaviour lives in one place and survives across windows and
    /// players. Copy stores a snapshot; each paste hands back a fresh independent clone.
    /// </summary>
    internal static class TweenTreeClipboard
    {
        private const string NodesField = "_nodes";
        private const string RootField = "_root";

        private static TweenNode s_node;
        private static TweenClip s_clip;

        public static bool HasNode { get { return s_node != null; } }
        public static bool HasClip { get { return s_clip != null; } }

        /// <summary>Copy a node branch (stores a deep snapshot).</summary>
        public static void CopyNode(TweenNode node)
        {
            s_node = Clone(node) as TweenNode;
        }

        /// <summary>A fresh deep clone of the copied branch, or null if the buffer is empty.</summary>
        public static TweenNode PasteNode()
        {
            return Clone(s_node) as TweenNode;
        }

        /// <summary>Copy a whole clip (stores a deep snapshot: options + tree).</summary>
        public static void CopyClip(TweenClip clip)
        {
            s_clip = CloneClip(clip);
        }

        /// <summary>A fresh deep clone of the copied clip, or null if the buffer is empty.</summary>
        public static TweenClip PasteClip()
        {
            return CloneClip(s_clip);
        }

        /// <summary>
        /// Deep-copies a node. Flat serialized data (values, Object references, TweenData,
        /// UnityEvents) is cloned via EditorJsonUtility; the nested [SerializeReference] child
        /// list of groups is rebuilt recursively so subtypes are preserved.
        /// </summary>
        public static object Clone(object source)
        {
            if (source == null) { return null; }

            Type type = source.GetType();
            object clone = Activator.CreateInstance(type);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), clone);

            FieldInfo nodesField = type.GetField(NodesField, BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodesField != null && typeof(IList).IsAssignableFrom(nodesField.FieldType))
            {
                IList sourceList = nodesField.GetValue(source) as IList;
                IList newList = (IList)Activator.CreateInstance(nodesField.FieldType);
                if (sourceList != null)
                {
                    foreach (object child in sourceList)
                    {
                        newList.Add(Clone(child));
                    }
                }
                nodesField.SetValue(clone, newList);
            }

            return clone;
        }

        /// <summary>Snapshot a clip: scalar options via accessors, tree via a deep node clone.</summary>
        private static TweenClip CloneClip(TweenClip source)
        {
            if (source == null) { return null; }

            TweenClip clone = new TweenClip
            {
                Name = source.Name,
                PlayOnStart = source.PlayOnStart,
                UseUnscaledTime = source.UseUnscaledTime,
                LoopMode = source.LoopMode,
                Loops = source.Loops
            };
            SetRoot(clone, Clone(source.Root) as TweenNode);
            return clone;
        }

        private static void SetRoot(TweenClip clip, TweenNode root)
        {
            FieldInfo field = typeof(TweenClip).GetField(RootField, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) { field.SetValue(clip, root); }
        }
    }
}
