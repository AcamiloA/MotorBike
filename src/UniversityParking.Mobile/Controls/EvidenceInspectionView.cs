using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.Controls;

public sealed class EvidenceInspectionView : ContentView
{
    public Image Image { get; } = new() { Aspect = Aspect.AspectFit, AnchorX = .5, AnchorY = .5, InputTransparent = true };
    private readonly EvidenceViewport viewport = new();
#if ANDROID
    private global::Android.Views.View? touchSurface;
#endif
    public EvidenceInspectionView()
    {
        IsClippedToBounds = true;
        Content = new Grid { IsClippedToBounds = true, Children = { Image } };
        SizeChanged += (_, _) => Reset();
        Unloaded += (_, _) => Reset();
        Image.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Image.Source)) Reset(); };
        SemanticProperties.SetDescription(this, "Mantén presionada la imagen y arrastra para inspeccionar. Suelta para volver a la imagen completa.");
    }
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        Reset();
#if ANDROID
        if (touchSurface is not null) { touchSurface.Touch -= Touch; touchSurface = null; }
#endif
        base.OnHandlerChanging(args);
    }
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if ANDROID
        if (Handler?.PlatformView is global::Android.Views.View native) { touchSurface = native; native.Touch += Touch; }
#endif
    }
    public void Reset()
    {
        viewport.Reset(); Apply();
#if ANDROID
        touchSurface?.Parent?.RequestDisallowInterceptTouchEvent(false);
#endif
    }
    private void Apply() { Image.Scale = viewport.Scale; Image.TranslationX = viewport.X; Image.TranslationY = viewport.Y; }
#if ANDROID
    private void Touch(object? sender, global::Android.Views.View.TouchEventArgs args)
    {
        var motion = args.Event;
        if (motion is null) return;
        args.Handled = true;
        var density = touchSurface?.Resources?.DisplayMetrics?.Density ?? 1;
        var x = motion.GetX() / density; var y = motion.GetY() / density;
        switch (motion.ActionMasked)
        {
            case global::Android.Views.MotionEventActions.Down:
                if (Image.Handler?.PlatformView is global::Android.Widget.ImageView native && native.Drawable is { IntrinsicWidth: > 0, IntrinsicHeight: > 0 } drawable)
                {
                    viewport.TouchDown(x, y, Width, Height, drawable.IntrinsicWidth, drawable.IntrinsicHeight);
                    touchSurface?.Parent?.RequestDisallowInterceptTouchEvent(true);
                }
                break;
            case global::Android.Views.MotionEventActions.Move:
                if (motion.PointerCount == 1) viewport.TouchMove(x, y); else Reset();
                break;
            case global::Android.Views.MotionEventActions.Up: viewport.TouchUp(); Reset(); break;
            case global::Android.Views.MotionEventActions.Cancel:
            case global::Android.Views.MotionEventActions.PointerDown:
            case global::Android.Views.MotionEventActions.PointerUp: Reset(); break;
        }
        Apply();
    }
#endif
}
