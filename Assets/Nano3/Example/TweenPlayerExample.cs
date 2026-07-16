using UnityEngine;
using Nano3.TweenAnimator;

/// <summary>
/// Minimal example: drives one named clip of a TweenPlayer from the keyboard.
///   Q     - play the clip (restarts from the beginning)
///   E     - stop it
///   Space - toggle pause / resume
/// </summary>
public class TweenPlayerExample : MonoBehaviour
{
    [SerializeField] private TweenPlayer _player;
    [SerializeField] private string _clipName = "Open";

    private void Update()
    {
        if (_player == null) { return; }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _player.ResetAnimation(_clipName);
            _player.Play(_clipName, OnComplete);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            _player.Stop(_clipName);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_player.IsPaused(_clipName)) { _player.Resume(_clipName); }
            else { _player.Pause(_clipName); }
        }
    }

    private void OnComplete()
    {
        Debug.Log($"Tween clip '{_clipName}' complete");
    }
}
