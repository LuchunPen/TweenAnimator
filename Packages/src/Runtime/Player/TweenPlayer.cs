using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// The single component that owns several named animations (<see cref="TweenClip"/>) and
    /// drives them by name, so one object no longer needs a component (or a player) per animation.
    /// Clips are independent — you can run more than one at a time.
    /// </summary>
    public class TweenPlayer : MonoBehaviour
    {
        [SerializeField] private List<TweenClip> _clips = new List<TweenClip>();

        public IReadOnlyList<TweenClip> Clips { get { return _clips; } }

        private void Start()
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                _clips[i].Init();
                if (_clips[i].PlayOnStart) { _clips[i].Play(); }
            }
        }

        private void OnDestroy()
        {
            // DOTween tweens are global (not tied to this GameObject); kill them here so a
            // destroyed player doesn't keep applying values to destroyed targets.
            Stop();
        }

        /// <summary>
        /// Play the clip with the given name. <paramref name="onComplete"/> fires once when the
        /// clip fully finishes (never on an infinite loop); <paramref name="onStepComplete"/>
        /// fires at the end of every pass, including each loop cycle. Ignored (callbacks
        /// discarded) while the clip is already playing.
        /// </summary>
        public void Play(string name, Action onComplete = null, Action onStepComplete = null)
        {
            FindOrWarn(name)?.Play(onComplete, onStepComplete);
        }

        public void Stop(string name)
        {
            FindOrWarn(name)?.Stop();
        }

        /// <summary>Stop every clip on this player.</summary>
        public void Stop()
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                _clips[i].Stop();
            }
        }

        public void Pause(string name)
        {
            FindOrWarn(name)?.Pause();
        }

        public void Resume(string name)
        {
            FindOrWarn(name)?.Resume();
        }

        public void ResetAnimation(string name)
        {
            FindOrWarn(name)?.ResetAnimation();
        }

        /// <summary>Instantly jump the named clip to its finished state; onComplete still fires.</summary>
        public void SetFinishState(string name, Action onComplete = null)
        {
            FindOrWarn(name)?.SetFinishState(onComplete);
        }

        public bool IsPlaying(string name)
        {
            TweenClip clip = Find(name);
            return clip != null && clip.IsPlaying;
        }

        public bool IsPaused(string name)
        {
            TweenClip clip = Find(name);
            return clip != null && clip.IsPaused;
        }

        public TweenClip GetClip(string name)
        {
            return Find(name);
        }

        private TweenClip Find(string name)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].Name == name) { return _clips[i]; }
            }
            return null;
        }

        /// <summary>Find used by command methods: a missing clip is a call-site bug, so it warns.</summary>
        private TweenClip FindOrWarn(string name)
        {
            TweenClip clip = Find(name);
            if (clip == null)
            {
                Debug.LogWarning($"TweenPlayer: no clip named '{name}' on '{gameObject.name}'.", this);
            }
            return clip;
        }
    }
}
