using UnityEditor;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Adds an "Open Tween Tree Editor" button above the default TweenPlayer inspector.
    /// The default inspector (with the SerializeReference type-picker) still works as a fallback.
    /// </summary>
    [CustomEditor(typeof(TweenPlayer))]
    public class TweenPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Tween Tree Editor"))
            {
                TweenTreeWindow.OpenFor((TweenPlayer)target);
            }

            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }
    }
}
