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

        /// <summary>
        /// Play the clip with the given name. Returns the clip so callbacks can be chained:
        /// <c>player.Play("Show").OnComplete(cb).OnStepComplete(cb)</c>. Null if the clip is missing.
        /// </summary>
        public TweenClip Play(string name)
        {
            TweenClip clip = Find(name);
            if (clip == null)
            {
                Debug.LogWarning($"TweenPlayer: no clip named '{name}' on '{gameObject.name}'.", this);
                return null;
            }
            return clip.Play();
        }

        /// <summary>Convenience overload: play and register an OnComplete callback.</summary>
        public TweenClip Play(string name, Action onComplete)
        {
            TweenClip clip = Play(name);
            return clip != null ? clip.OnComplete(onComplete) : null;
        }

        public void Stop(string name)
        {
            Find(name)?.Stop();
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
            Find(name)?.Pause();
        }

        public void Resume(string name)
        {
            Find(name)?.Resume();
        }

        public void ResetAnimation(string name)
        {
            Find(name)?.ResetAnimation();
        }

        /// <summary>Instantly jump the named clip to its finished state; onComplete still fires.</summary>
        public void SetFinishState(string name, Action onComplete = null)
        {
            Find(name)?.SetFinishState(onComplete);
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
    }
}
