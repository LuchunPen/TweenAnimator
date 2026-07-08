using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// The single component that drives a whole tween tree. The entire animation
    /// (leaves + Sequence/Parallel groups) is stored in one serialized reference,
    /// so a complex animation no longer needs a pile of components.
    /// </summary>
    public enum TweenLoopMode
    {
        None,
        Restart
    }

    public class TweenPlayer : MonoBehaviour
    {
        [SerializeReference] private TweenNode _root;
        [SerializeField] private bool _playOnStart;
        [Tooltip("Play using unscaled time, so the animation keeps running while Time.timeScale = 0 " +
                 "(e.g. a gameplay pause). Propagates to every node in the tree.")]
        [SerializeField] private bool _useUnscaledTime;
        [Tooltip("Loop the whole animation. Restart replays it from the start each cycle.")]
        [SerializeField] private TweenLoopMode _loopMode = TweenLoopMode.None;
        [Tooltip("How many extra times to repeat when looping. -1 = infinite.")]
        [SerializeField] private int _loops = -1;

        private int _loopsRemaining;

        public TweenNode Root { get { return _root; } }
        public bool PlayOnStart { get { return _playOnStart; } set { _playOnStart = value; } }
        public bool UseUnscaledTime { get { return _useUnscaledTime; } set { _useUnscaledTime = value; } }
        public TweenLoopMode LoopMode { get { return _loopMode; } set { _loopMode = value; } }
        public int Loops { get { return _loops; } set { _loops = value; } }

        private void Start()
        {
            if (_root == null) { return; }

            _root.SetUnscaledTime(_useUnscaledTime);
            _root.Init();
            if (_playOnStart) { Play(); }
        }

        public void Play(Action onComplete = null)
        {
            if (_root == null) { return; }

            _loopsRemaining = _loops;
            PlayInternal(onComplete);
        }

        private void PlayInternal(Action onComplete)
        {
            _root.SetUnscaledTime(_useUnscaledTime);
            _root.Play(() => OnRootComplete(onComplete));
        }

        private void OnRootComplete(Action onComplete)
        {
            if (_loopMode == TweenLoopMode.Restart && _loopsRemaining != 0)
            {
                if (_loopsRemaining > 0) { _loopsRemaining--; }

                _root.Reset();
                PlayInternal(onComplete);
                return;
            }

            onComplete?.Invoke();
        }

        public void Stop()
        {
            if (_root == null) { return; }

            _loopsRemaining = 0;
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
