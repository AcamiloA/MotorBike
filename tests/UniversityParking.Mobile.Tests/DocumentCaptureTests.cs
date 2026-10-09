using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.Tests;

public sealed class DocumentCaptureTests
{
    [Theory]
    [InlineData(4000,3000,1000,600)] [InlineData(1920,1080,1280,720)]
    [InlineData(8000,6000,2200,800)] [InlineData(1200,800,600,400)]
    public void CenteredGuideMapsToBoundedDocumentWithCardAspect(int iw,int ih,int pw,int ph)
    {
        var guide=DocumentCaptureGeometry.Guide(pw,ph);var crop=DocumentCaptureGeometry.Map(iw,ih,pw,ph,guide);
        Assert.InRange(crop.X,0,iw-1);Assert.InRange(crop.Y,0,ih-1);Assert.InRange(crop.X+crop.Width,1,iw);Assert.InRange(crop.Y+crop.Height,1,ih);
        Assert.InRange(Math.Abs((double)crop.Width/crop.Height-DocumentCaptureGeometry.CardAspect),0,.01);
        Assert.InRange(Math.Abs(crop.X*2+crop.Width-iw),0,2);Assert.InRange(Math.Abs(crop.Y*2+crop.Height-ih),0,2);
    }
    [Fact] public void AspectFillAccountsForPreviewCropping()
        => Assert.Equal(new PixelCrop(0,500,2000,1000),DocumentCaptureGeometry.Map(2000,2000,1000,500,new(0,0,1000,500)));
    [Fact] public void AspectFitAccountsForLetterboxing()
        => Assert.Equal(new PixelCrop(0,0,2000,2000),DocumentCaptureGeometry.Map(2000,2000,1000,500,new(250,0,500,500),PreviewScaling.AspectFit));
    [Fact] public void DensityDoesNotChangeImageRegion()
    {
        var a=DocumentCaptureGeometry.Map(4000,3000,1000,600,DocumentCaptureGeometry.Guide(1000,600));
        var b=DocumentCaptureGeometry.Map(4000,3000,3000,1800,DocumentCaptureGeometry.Guide(3000,1800));
        Assert.InRange(Math.Abs(a.X-b.X),0,1);Assert.InRange(Math.Abs(a.Y-b.Y),0,1);Assert.InRange(Math.Abs(a.Width-b.Width),0,1);Assert.InRange(Math.Abs(a.Height-b.Height),0,1);
    }
    [Theory] [InlineData(0,3000,4000)] [InlineData(90,4000,3000)] [InlineData(180,3000,4000)] [InlineData(270,4000,3000)]
    public void SensorRotationNormalizesDimensions(int rotation,int width,int height)
        => Assert.Equal((width,height),DocumentCaptureGeometry.UprightSize(3000,4000,rotation));
    [Theory] [InlineData(90,270)] [InlineData(180,180)] [InlineData(270,90)]
    public void CropRotationRoundTripsWithoutLosingCoordinates(int rotate,int undo)
    {
        var raw=new PixelCrop(100,200,300,400);var moved=DocumentCaptureGeometry.Rotate(raw,1000,800,rotate);
        var size=DocumentCaptureGeometry.UprightSize(1000,800,rotate);
        Assert.Equal(raw,DocumentCaptureGeometry.Rotate(moved,size.Width,size.Height,undo));
    }
    [Theory] [InlineData(4000,2522,2048,1291)] [InlineData(1000,630,1000,630)]
    public void OutputDimensionsArePredictableAndNeverUpscaled(int width,int height,int outputWidth,int outputHeight)
        => Assert.Equal((outputWidth,outputHeight),DocumentCaptureGeometry.OutputSize(width,height));
    [Fact] public void InvalidAndNonIntersectingInputsAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>DocumentCaptureGeometry.Guide(0,100));
        Assert.Throws<ArgumentException>(()=>DocumentCaptureGeometry.Map(100,100,100,100,new(200,200,10,10)));
        Assert.Throws<ArgumentException>(()=>DocumentCaptureGeometry.Map(100,100,100,100,new(double.NaN,0,10,10)));
        Assert.Throws<ArgumentException>(()=>DocumentCaptureGeometry.Rotation(45));
        Assert.Throws<ArgumentException>(()=>DocumentCaptureGeometry.Rotate(new(99,99,10,10),100,100,0));
    }
    [Fact] public void PartiallyOutsideGuideNeverExceedsBitmapBounds()
        => Assert.Equal(new PixelCrop(0,0,100,80),DocumentCaptureGeometry.Map(100,80,200,160,new(-20,-20,240,200)));
    [Fact] public void OnlyExplicitlyConfirmedCropLeavesSessionAndRepeatClearsPreview()
    {
        var session=new DocumentCaptureSession();var cropped=new PickedAttachment("crop.png","image/png",[137,80,78,71,13,10,26,10]);
        Assert.Throws<InvalidOperationException>(()=>session.Confirm());session.ShowCrop(cropped);Assert.Equal(DocumentCaptureState.PREVIEW,session.State);
        session.Repeat();Assert.Null(session.Preview);Assert.Equal(DocumentCaptureState.CAPTURING,session.State);
        session.ShowCrop(cropped);Assert.Equal(cropped,session.Confirm());Assert.Equal(DocumentCaptureState.CONFIRMED,session.State);
        Assert.Throws<InvalidOperationException>(()=>session.ShowCrop(cropped));
    }
    [Fact] public void CancelDropsCropAndRejectsLateCapture()
    {
        var session=new DocumentCaptureSession();session.Cancel();Assert.Null(session.Preview);
        Assert.Throws<InvalidOperationException>(()=>session.ShowCrop(new("late.png","image/png",[137,80,78,71,13,10,26,10])));
    }
}
