using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    public enum TweenLoopMode
    {
        None,
        Restart
    }

    /// <summary>
    /// A named, self-contained animation: a tween tree (<see cref="Root"/>) plus its own
    /// playback options and runtime state. A <see cref="TweenPlayer"/> holds several of these
    /// and drives them by name, so one component can own many independent animations.
    /// </summary>
    [Serializable]
    public class TweenClip
    {
        [SerializeField] private string _name = "New Clip";
        [SerializeReference] private TweenNode _root;
        [SerializeField] private bool _playOnStart;
        [Tooltip("Play using unscaled time, so the clip keeps running while Time.timeScale = 0 " +
                 "(e.g. a gameplay pause). Propagates to every node in the tree.")]
        [SerializeField] private bool _useUnscaledTime;
        [Tooltip("Loop the clip. Restart replays it from the start each cycle.")]
        [SerializeField] private TweenLoopMode _loopMode = TweenLoopMode.None;
        [Tooltip("How many extra times to repeat when looping. -1 = infinite.")]
        [SerializeField] private int _loops = -1;

        private int _loopsRemaining;
        private bool _isPaused;
        private bool _isPlaying;
        private float _startTime;
        private float _pausedElapsed;

        public string Name { get { return _name; } set { _name = value; } }
        public TweenNode Root { get { return _root; } }
        public bool PlayOnStart { get { return _playOnStart; } set { _playOnStart = value; } }
        public bool UseUnscaledTime { get { return _useUnscaledTime; } set { _useUnscaledTime = value; } }
        public TweenLoopMode LoopMode { get { return _loopMode; } set { _loopMode = value; } }
        public int Loops { get { return _loops; } set { _loops = value; } }
        public bool IsPaused { get { return _isPaused; } }
        public bool IsPlaying { get { return _isPlaying; } }

        private float Now { get { return _useUnscaledTime ? Time.unscaledTime : Time.time; } }

        /// <summary>Total duration of one pass, in seconds.</summary>
        public float Duration { get { return _root != null ? _root.GetDuration() : 0f; } }

        /// <summary>Seconds elapsed in the current pass (frozen while paused).</summary>
        public float Elapsed
        {
            get
            {
                if (!_isPlaying) { return 0f; }
                return _isPaused ? _pausedElapsed : Now - _startTime;
            }
        }

        /// <summary>Time-based playback progress in [0..1].</summary>
        public float Progress
        {
            get
            {
                if (!_isPlaying) { return 0f; }
                float duration = Duration;
                return duration > 0f ? Mathf.Clamp01(Elapsed / duration) : 1f;
            }
        }

        /// <summary>Cache initial node state (equivalent of MonoBehaviour.Start). Called once by the player.</summary>
        public void Init()
        {
            if (_root == null) { return; }

            _root.SetUnscaledTime(_useUnscaledTime);
            _root.Init();
        }

        public void Play(Action onComplete = null)
        {
            if (_root == null) { return; }

            _isPaused = false;
            _isPlaying = true;
            _startTime = Now;
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

                _startTime = Now;
                _root.Reset();
                PlayInternal(onComplete);
                return;
            }

            _isPlaying = false;
            onComplete?.Invoke();
        }

        public void Stop()
        {
            if (_root == null) { return; }

            _isPaused = false;
            _isPlaying = false;
            _loopsRemaining = 0;
            _root.Stop();
        }

        /// <summary>Pause playback, keeping progress. Resume with <see cref="Resume"/>.</summary>
        public void Pause()
        {
            if (_root == null || _isPaused || !_isPlaying) { return; }

            _pausedElapsed = Now - _startTime;
            _isPaused = true;
            _root.SetPaused(true);
        }

        /// <summary>Resume playback after <see cref="Pause"/>.</summary>
        public void Resume()
        {
            if (_root == null || !_isPaused) { return; }

            _startTime = Now - _pausedElapsed;
            _isPaused = false;
            _root.SetPaused(false);
        }

        public void ResetAnimation()
        {
            if (_root == null) { return; }

            _isPaused = false;
            _isPlaying = false;
            _loopsRemaining = 0;
            _root.Reset();
        }

        /// <summary>Instantly jump to the finished state and report completion.</summary>
        public void SetFinishState(Action onComplete = null)
        {
            if (_root == null) { return; }

            _isPaused = false;
            _isPlaying = false;
            _loopsRemaining = 0;
            _root.SetFinishState(onComplete);
        }
    }
}
