using UnityEngine;
using Nano3.TweenAnimator;

/// <summary>
/// Minimal example: drives a TweenPlayer from the keyboard.
///   Q     - play (restarts from the beginning)
///   E     - stop
///   Space - toggle pause / resume
/// </summary>
public class TweenPlayerExample : MonoBehaviour
{
    [SerializeField] private TweenPlayer _player;

    private void Update()
    {
        if (_player == null) { return; }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _player.ResetAnimation();
            _player.Play(OnComplete);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            _player.Stop();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_player.IsPaused) { _player.Resume(); }
            else { _player.Pause(); }
        }
    }

    private void OnComplete()
    {
        Debug.Log("Tween complete");
    }
}
