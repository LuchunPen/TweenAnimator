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
        private bool _initialized;
        private bool _inPlayCall;              // true while Play() itself is on the stack
        private bool _completedSynchronously;  // the last run finished inside its own Play() call
        private float _startTime;
        private float _pausedElapsed;
        private Action _onComplete;
        private Action _onStepComplete;

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

        /// <summary>Cache initial node state (equivalent of MonoBehaviour.Start). Runs once; later calls are ignored.</summary>
        public void Init()
        {
            if (_root == null || _initialized) { return; }

            _initialized = true;
            _root.SetUnscaledTime(_useUnscaledTime);
            _root.Init();
        }

        /// <summary>
        /// Start playing from the beginning (the tree is reset first). Ignored while the clip
        /// is already playing. Returns the clip so callbacks can be chained fluently.
        /// </summary>
        public TweenClip Play()
        {
            if (_root == null || _isPlaying) { return this; }

            if (!_initialized) { Init(); }

            _onComplete = null;
            _onStepComplete = null;
            _completedSynchronously = false;
            _isPaused = false;
            _isPlaying = true;
            _startTime = Now;
            _loopsRemaining = _loops;

            // Restart from a clean state: without this, replaying a completed clip would tween
            // from end values to end values (a visually empty pass).
            _root.Reset();

            _inPlayCall = true;
            PlayInternal();
            _inPlayCall = false;
            return this;
        }

        /// <summary>Fluent: callback when the clip fully finishes (never fires on an infinite loop).</summary>
        public TweenClip OnComplete(Action callback)
        {
            // A zero-duration clip (instant-only nodes) finishes synchronously inside Play(),
            // before any fluent call can run — fire immediately so the callback isn't lost.
            if (_completedSynchronously) { callback?.Invoke(); return this; }

            _onComplete = callback;
            return this;
        }

        /// <summary>Fluent: callback fired at the end of every pass, including each loop cycle.</summary>
        public TweenClip OnStepComplete(Action callback)
        {
            if (_completedSynchronously) { callback?.Invoke(); return this; }

            _onStepComplete = callback;
            return this;
        }

        private void PlayInternal()
        {
            _root.SetUnscaledTime(_useUnscaledTime);
            _root.Play(OnRootComplete);
        }

        private void OnRootComplete()
        {
            _onStepComplete?.Invoke();

            if (_loopMode == TweenLoopMode.Restart && _loopsRemaining != 0)
            {
                // A zero-length clip (empty root or instant-only nodes) completes synchronously,
                // so looping it would recurse forever within a single frame.
                if (Duration <= 0f)
                {
                    Debug.LogWarning($"TweenClip '{_name}': loop aborted — the clip has zero duration " +
                                     "(empty or instant-only), looping it would never yield.");
                }
                else
                {
                    if (_loopsRemaining > 0) { _loopsRemaining--; }

                    _startTime = Now;
                    _root.Reset();
                    PlayInternal();
                    return;
                }
            }

            _isPlaying = false;
            _completedSynchronously = _inPlayCall;
            _onComplete?.Invoke();
            _onComplete = null;
            _onStepComplete = null;
        }

        public void Stop()
        {
            if (_root == null) { return; }

            _isPaused = false;
            _isPlaying = false;
            _loopsRemaining = 0;
            _completedSynchronously = false;
            _onComplete = null;
            _onStepComplete = null;
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
            _completedSynchronously = false;
            _onComplete = null;
            _onStepComplete = null;
            _root.Reset();
        }

        /// <summary>Instantly jump to the finished state and report completion.</summary>
        public void SetFinishState(Action onComplete = null)
        {
            if (_root == null) { return; }

            bool wasPlaying = _isPlaying;
            _isPaused = false;
            _isPlaying = false;
            _loopsRemaining = 0;
            _completedSynchronously = false;
            _root.SetFinishState(onComplete);

            // Finishing early still completes the run, so fluent callbacks fire too.
            if (wasPlaying)
            {
                _onStepComplete?.Invoke();
                _onComplete?.Invoke();
            }
            _onComplete = null;
            _onStepComplete = null;
        }
    }
}
