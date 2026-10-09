using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using UniversityParking.Mobile.Core;
using AView = Android.Views.View;
using AColor = Android.Graphics.Color;
using AButton = Android.Widget.Button;
using Paint = Android.Graphics.Paint;
using CaptureResolution = AndroidX.Camera.Core.ResolutionSelector.ResolutionSelector;
using CaptureStrategy = AndroidX.Camera.Core.ResolutionSelector.ResolutionStrategy;

namespace UniversityParking.Mobile.Platforms.Android;

internal static class TransitLicenseCapture
{
    private static readonly object Gate = new();
    private static TaskCompletionSource<PickedAttachment?>? pending;
    private static string? requestId;
    public static async Task<PickedAttachment?> CaptureAsync()
    {
        var completion = new TaskCompletionSource<PickedAttachment?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = Guid.NewGuid().ToString("N");
        lock (Gate)
        {
            if (pending is not null) throw new UserInputException("Ya hay una captura en curso.");
            pending = completion; requestId = id;
        }
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? throw new UserInputException("La cámara no está disponible.");
                activity.StartActivity(new Intent(activity, typeof(TransitLicenseCaptureActivity)).PutExtra("request", id));
            });
            return await completion.Task;
        }
        catch { Complete(id, null); throw; }
    }
    internal static void Complete(string? id, PickedAttachment? image)
    {
        TaskCompletionSource<PickedAttachment?>? completion;
        lock (Gate)
        {
            if (requestId != id) return;
            completion = pending; pending = null; requestId = null;
        }
        completion?.TrySetResult(image);
    }
}

// A dedicated Activity owns its landscape policy; finishing restores the existing MAUI Activity.
[Activity(Theme = "@style/Maui.MainTheme", Exported = false, ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class TransitLicenseCaptureActivity : AppCompatActivity
{
    private PreviewView preview = null!;
    private GuideView guide = null!;
    private ImageView result = null!;
    private AButton shutter = null!, use = null!, repeat = null!;
    private TextView instruction = null!;
    private ProcessCameraProvider? provider;
    private AndroidX.Camera.Core.Preview? cameraPreview;
    private ImageCapture? capture;
    private Bitmap? previewBitmap;
    private readonly DocumentCaptureSession session = new();
    private string? request;
    private bool initializing, processing, destroyed;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SupportActionBar?.Hide();
        request = Intent?.GetStringExtra("request");
        if (savedInstanceState is not null || string.IsNullOrEmpty(request)) { Finish(); return; }
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(AColor.Black);
        instruction = new TextView(this) { Text = "Gira el celular horizontalmente y ubica la Licencia de Tránsito dentro del recuadro.", Gravity = GravityFlags.Center, TextSize = 16 };
        instruction.SetTextColor(AColor.White); instruction.SetPadding(12, 6, 12, 6); root.AddView(instruction);
        var holder = new FrameLayout(this); root.AddView(holder, new LinearLayout.LayoutParams(-1, 0, 1));
        preview = new PreviewView(this); preview.SetScaleType(PreviewView.ScaleType.FillCenter!);
        // Texture-based compatible preview supports overlays reliably across Android devices.
        preview.SetImplementationMode(PreviewView.ImplementationMode.Compatible!);
        guide = new GuideView(this);
        result = new ImageView(this); result.SetScaleType(ImageView.ScaleType.FitCenter); result.Visibility = ViewStates.Gone;
        holder.AddView(preview, new FrameLayout.LayoutParams(-1,-1)); holder.AddView(guide, new FrameLayout.LayoutParams(-1,-1)); holder.AddView(result, new FrameLayout.LayoutParams(-1,-1));
        var buttons = new LinearLayout(this) { Orientation = Orientation.Horizontal }; buttons.SetGravity(GravityFlags.Center);
        root.AddView(buttons);
        shutter = Button("CAPTURAR", () => Capture());
        use = Button("USAR FOTO", Confirm); repeat = Button("REPETIR", Repeat);
        var cancel = Button("CANCELAR", Finish);
        foreach (var button in new[] { shutter, use, repeat, cancel }) buttons.AddView(button, new LinearLayout.LayoutParams(0,-2,1));
        use.Visibility = repeat.Visibility = ViewStates.Gone; shutter.Enabled = false;
        SetContentView(root);
        preview.LayoutChange += (_, _) => { if (preview.Width > 0 && preview.Height > 0 && capture is null && !initializing && !processing) BindCamera(); };
    }
    private AButton Button(string title, Action action)
    { var button = new AButton(this) { Text = title }; button.Click += (_, _) => action(); return button; }
    private void BindCamera()
    {
        if (destroyed || session.State != DocumentCaptureState.CAPTURING || preview.ViewPort is null) return;
        initializing = true;
        var future = ProcessCameraProvider.GetInstance(this);
        future.AddListener(new Java.Lang.Runnable(() =>
        {
            initializing = false;
            if (destroyed || IsFinishing) return;
            try
            {
                provider = future.Get()!.JavaCast<ProcessCameraProvider>();
                cameraPreview = new AndroidX.Camera.Core.Preview.Builder().Build();
                cameraPreview.SetSurfaceProvider(ContextCompat.GetMainExecutor(this)!, preview.SurfaceProvider);
                var resolution = new CaptureResolution.Builder().SetResolutionStrategy(new CaptureStrategy(new global::Android.Util.Size(3264,2448),CaptureStrategy.FallbackRuleClosestLowerThenHigher)).Build();
                capture = new ImageCapture.Builder().SetResolutionSelector(resolution).SetTargetRotation((int)(preview.Display?.Rotation ?? SurfaceOrientation.Rotation0)).Build();
                var group = new UseCaseGroup.Builder().SetViewPort(preview.ViewPort!).AddUseCase(cameraPreview).AddUseCase(capture).Build();
                provider.BindToLifecycle(this, CameraSelector.DefaultBackCamera!, group);
                shutter.Enabled = true;
            }
            catch (Exception)
            {
                capture = null; instruction.Text = "No fue posible abrir la cámara. Cancela y selecciona una imagen.";
            }
        }), ContextCompat.GetMainExecutor(this));
    }
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (!processing && session.State == DocumentCaptureState.CAPTURING)
        { Unbind(); preview.Post(() => BindCamera()); }
    }
    private void Capture()
    {
        if (capture is null || processing || session.State != DocumentCaptureState.CAPTURING || guide.Width <= 0 || guide.Height <= 0) return;
        processing = true; shutter.Enabled = false;
        RequestedOrientation = ScreenOrientation.Locked;
        var width = guide.Width; var height = guide.Height; var rectangle = guide.Rectangle;
        capture.TargetRotation = (int)(preview.Display?.Rotation ?? SurfaceOrientation.Rotation0);
        capture.TakePicture(ContextCompat.GetMainExecutor(this)!, new Captured(this, width, height, rectangle));
    }
    private sealed class Captured(TransitLicenseCaptureActivity activity, int width, int height, CaptureRect guide) : ImageCapture.OnImageCapturedCallback
    {
        public override void OnCaptureSuccess(IImageProxy image)
        {
            // All camera metadata and pixels belong to the same exposure. Never save the original.
            _ = activity.ProcessAsync(image, width, height, guide);
        }
        public override void OnError(ImageCaptureException exception) => activity.Failed();
    }
    private async Task ProcessAsync(IImageProxy image, int width, int height, CaptureRect rectangle)
    {
        try
        {
            var rotation = DocumentCaptureGeometry.Rotation(image.ImageInfo!.RotationDegrees);
            var sourceCrop = image.CropRect!;
            var x = sourceCrop.Left; var y = sourceCrop.Top; var w = sourceCrop.Width(); var h = sourceCrop.Height();
            var sourceWidth = image.Width; var sourceHeight = image.Height;
            var processed = await Task.Run(() =>
            {
                // ToBitmap returns untransformed pixels. CameraX supplies sensor/display orientation
                // and the crop shared with PreviewView, independent of the JPEG's EXIF representation.
                using var raw = image.ToBitmap();
                if (raw.Width != sourceWidth || raw.Height != sourceHeight) throw new InvalidOperationException("Dimensiones de cámara inconsistentes.");
                using var viewport = Bitmap.CreateBitmap(raw, x, y, w, h)!;
                using var matrix = new Matrix(); matrix.PostRotate(rotation);
                using var upright = Bitmap.CreateBitmap(viewport,0,0,viewport.Width,viewport.Height,matrix,true)!;
                var crop = DocumentCaptureGeometry.Map(upright.Width,upright.Height,width,height,rectangle);
                using var document = Bitmap.CreateBitmap(upright,crop.X,crop.Y,crop.Width,crop.Height)!;
                var size = DocumentCaptureGeometry.OutputSize(document.Width,document.Height);
                using var scaled = Bitmap.CreateScaledBitmap(document,size.Width,size.Height,true)!;
                using var output = new MemoryStream();
                if (!scaled.Compress(Bitmap.CompressFormat.Jpeg!,90,output)) throw new InvalidOperationException("No se pudo codificar el recorte.");
                var photo = AttachmentValidation.Validate("transit-license-front-"+Guid.NewGuid().ToString("N")+".jpg","image/jpeg",output.ToArray(),true);
                return photo;
            });
            await MainThread.InvokeOnMainThreadAsync(() => PresentCrop(processed));
        }
        catch (Exception) { MainThread.BeginInvokeOnMainThread(() => { if (!destroyed && !IsFinishing) Failed(); }); }
        finally { image.Close(); image.Dispose(); }
    }
    private void PresentCrop(PickedAttachment processed)
    {
        if (destroyed || IsFinishing || session.State != DocumentCaptureState.CAPTURING) return;
        session.ShowCrop(processed); Unbind();
        previewBitmap = BitmapFactory.DecodeByteArray(processed.Bytes,0,processed.Bytes.Length);
        if (previewBitmap is null) throw new InvalidOperationException("No se pudo mostrar el recorte.");
        result.SetImageBitmap(previewBitmap); result.Visibility = ViewStates.Visible;
        guide.Visibility = preview.Visibility = shutter.Visibility = ViewStates.Gone;
        use.Visibility = repeat.Visibility = ViewStates.Visible;
        instruction.Text = "Revisa la imagen recortada. Usa la foto o repite la captura.";
        processing = false;
    }
    private void Failed()
    {
        processing = false; RequestedOrientation = ScreenOrientation.SensorLandscape;
        if (session.State == DocumentCaptureState.PREVIEW) session.Repeat();
        instruction.Text = "No fue posible recortar la foto. Repite la captura o cancela.";
        shutter.Enabled = capture is not null;
        if (capture is null) BindCamera();
    }
    private void Confirm()
    {
        if (session.State != DocumentCaptureState.PREVIEW || processing) return;
        TransitLicenseCapture.Complete(request, session.Confirm()); Finish();
    }
    private void Repeat()
    {
        if (processing || session.State != DocumentCaptureState.PREVIEW) return;
        session.Repeat(); ClearPreview(); RequestedOrientation = ScreenOrientation.SensorLandscape;
        result.Visibility = use.Visibility = repeat.Visibility = ViewStates.Gone;
        preview.Visibility = guide.Visibility = shutter.Visibility = ViewStates.Visible;
        instruction.Text = "Gira el celular horizontalmente y ubica la Licencia de Tránsito dentro del recuadro.";
        BindCamera();
    }
    private void Unbind()
    {
        if (provider is not null && cameraPreview is not null && capture is not null) provider.Unbind(cameraPreview,capture);
        cameraPreview?.Dispose(); capture?.Dispose(); cameraPreview = null; capture = null;
    }
    private void ClearPreview() { result?.SetImageDrawable(null); previewBitmap?.Dispose(); previewBitmap = null; }
    protected override void OnDestroy()
    {
        destroyed = true; Unbind(); ClearPreview();
        if (session.State != DocumentCaptureState.CONFIRMED) session.Cancel();
        TransitLicenseCapture.Complete(request,null); base.OnDestroy();
    }
    private sealed class GuideView(Context context) : AView(context)
    {
        public CaptureRect Rectangle => DocumentCaptureGeometry.Guide(Width,Height);
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas); if (Width <= 0 || Height <= 0) return;
            var r = Rectangle;
            using var shade = new Paint { Color = new AColor(0,0,0,150) };
            canvas.DrawRect(0,0,Width,(float)r.Y,shade); canvas.DrawRect(0,(float)(r.Y+r.Height),Width,Height,shade);
            canvas.DrawRect(0,(float)r.Y,(float)r.X,(float)(r.Y+r.Height),shade); canvas.DrawRect((float)(r.X+r.Width),(float)r.Y,Width,(float)(r.Y+r.Height),shade);
            using var border = new Paint { Color = AColor.White, StrokeWidth = 3 * Resources!.DisplayMetrics!.Density, AntiAlias = true };
            border.SetStyle(Paint.Style.Stroke);
            canvas.DrawRect((float)r.X,(float)r.Y,(float)(r.X+r.Width),(float)(r.Y+r.Height),border);
        }
    }
}
