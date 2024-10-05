## Tween animator

It's a DoTween wrapper for configuring simple animations in the inspector (mainly designed for UI)


![Image alt](https://github.com/LuchunPen/TweenAnimator/blob/master/Pic2.gif)

Supports parrallel and sequence animations in any combinations. All of the animations are inherited from AnimationBase class.

![Image alt](https://github.com/LuchunPen/TweenAnimator/blob/master/Pic1.png)

Animation example:
```C#
public class TweenAnimationScale : TweenAnimation
{
    [Space(10)]
    [SerializeField] private Transform _trans;
    [SerializeField] private float _startValue;
    [SerializeField] private float _endValue;
    
    protected override void UpdateAnimator()
    {
        float scale =  _startValue + (_tweenerValue * (_endValue - _startValue));
        _trans.localScale = new Vector3(scale, scale, scale);
    }
}
```

How to use:
```C#
public class Test_TweenAnimationController : MonoBehaviour
{
    [SerializeField] private AnimationBase _anim;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            _anim.ResetAnimation();
            _anim.PlayAnimation(OnComplete);
        }

        if (Input.GetKey(KeyCode.E))
        {
            _anim.StopAnimation();
        }
    }

    private void OnComplete()
    {
        UnityEngine.Debug.Log("Complete animation");
    }
}
```