using System;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Optional metadata controlling how a <see cref="TweenNode"/> subtype appears in the
    /// inspector type picker. Without it the picker falls back to the nicified class name,
    /// auto-categorised by base type (Animations/Groups). Use '/' to nest submenus.
    /// Example: [TweenNodeMenu("Move/To Position")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweenNodeMenuAttribute : Attribute
    {
        private readonly string _path;

        public string Path { get { return _path; } }

        public TweenNodeMenuAttribute(string path)
        {
            _path = path;
        }
    }
}
