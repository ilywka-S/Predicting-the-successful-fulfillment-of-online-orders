namespace OrderSense.Maui.Controls;

public class Skeleton : Border
{
    private const string AnimationName = "SkeletonPulse";
    private bool _running;

    public Skeleton()
    {
        this.SetDynamicResource(StyleProperty, "Skeleton");
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    private void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        var pulse = new Animation
        {
            { 0, 0.5, new Animation(v => Opacity = v, 1, 0.5) },
            { 0.5, 1, new Animation(v => Opacity = v, 0.5, 1) },
        };
        pulse.Commit(this, AnimationName, length: 1600, easing: Easing.SinInOut, repeat: () => _running);
    }

    private void Stop()
    {
        _running = false;
        this.AbortAnimation(AnimationName);
        Opacity = 1;
    }
}
