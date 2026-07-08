using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// The single component that drives a whole tween tree. The entire animation
    /// (leaves + Sequence/Parallel groups) is stored in one serialized reference,
    /// so a complex animation no longer needs a pile of components.
    /// </summary>
    public class TweenPlayer : MonoBehaviour
    {
        [SerializeReference] private TweenNode _root;
        [SerializeField] private bool _playOnStart;

        public TweenNode Root { get { return _root; } }
        public bool PlayOnStart { get { return _playOnStart; } set { _playOnStart = value; } }

        private void Start()
        {
            if (_root == null) { return; }

            _root.Init();
            if (_playOnStart) { Play(); }
        }

        public void Play(Action onComplete = null)
        {
            if (_root == null) { return; }
            _root.Play(onComplete);
        }

        public void Stop()
        {
            if (_root == null) { return; }
            _root.Stop();
        }

        public void ResetAnimation()
        {
            if (_root == null) { return; }
            _root.Reset();
        }

        public void SetFinishState(Action onComplete = null)
        {
            if (_root == null) { return; }
            _root.SetFinishState(onComplete);
        }
    }
}
