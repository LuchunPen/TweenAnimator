using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Shared helper that discovers all concrete <see cref="TweenNode"/> types via
    /// <see cref="TypeCache"/> and builds a categorised picker menu. Used by both the
    /// inspector drawer and the tree editor window, so new node classes appear everywhere
    /// automatically without manual registration.
    /// </summary>
    public static class TweenNodeTypeMenu
    {
        /// <summary>All instantiable node types, sorted by their menu path.</summary>
        public static List<Type> GetNodeTypes()
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

        /// <summary>Full submenu path, e.g. "Animations/Tween Scale".</summary>
        public static string GetMenuPath(Type type)
        {
            TweenNodeMenuAttribute menu = type.GetCustomAttribute<TweenNodeMenuAttribute>();
            if (menu != null && !string.IsNullOrEmpty(menu.Path))
            {
                return menu.Path;
            }

            string category = typeof(TweenAnimation).IsAssignableFrom(type) ? "Animations" : "Groups";
            return category + "/" + ObjectNames.NicifyVariableName(type.Name);
        }

        /// <summary>Short label (last path segment), e.g. "Tween Scale".</summary>
        public static string GetDisplayName(Type type)
        {
            string path = GetMenuPath(type);
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        /// <summary>
        /// Show the type picker as a context menu. <paramref name="onSelected"/> receives the
        /// chosen type (or null when <paramref name="includeNone"/> is true and "(None)" is picked).
        /// </summary>
        public static void Show(bool includeNone, Action<Type> onSelected)
        {
            GenericMenu menu = new GenericMenu();

            if (includeNone)
            {
                menu.AddItem(new GUIContent("(None)"), false, () => onSelected(null));
                menu.AddSeparator(string.Empty);
            }

            foreach (Type type in GetNodeTypes())
            {
                Type captured = type;
                menu.AddItem(new GUIContent(GetMenuPath(type)), false, () => onSelected(captured));
            }

            menu.ShowAsContext();
        }
    }
}
