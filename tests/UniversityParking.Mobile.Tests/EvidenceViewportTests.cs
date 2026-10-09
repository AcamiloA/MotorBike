using UniversityParking.Mobile.Core;
namespace UniversityParking.Mobile.Tests;
public sealed class EvidenceViewportTests
{
    [Theory]
    [InlineData(300,600,400,800)] [InlineData(600,300,800,400)]
    [InlineData(300,600,1600,900)] [InlineData(600,300,900,1600)]
    [InlineData(300,600,1000,1000)]
    public void DragClampsToRenderedAspectFitBounds(double width,double height,double iw,double ih)
    {
        var v=new EvidenceViewport();AssertReset(v);
        v.TouchDown(width/2,height/2,width,height,iw,ih);
        Assert.True(v.Pressed);Assert.Equal(2.5,v.Scale);
        var fit=Math.Min(width/iw,height/ih);
        Assert.Equal(Math.Max(0,(iw*fit*2.5-width)/2),v.LimitX);
        Assert.Equal(Math.Max(0,(ih*fit*2.5-height)/2),v.LimitY);
        foreach(var signX in new[]{-1,1})foreach(var signY in new[]{-1,1})
        {v.TouchMove(signX*100000,signY*100000);Assert.Equal(signX*v.LimitX,v.X);Assert.Equal(signY*v.LimitY,v.Y);}
        v.TouchUp();AssertReset(v);v.TouchMove(100,100);AssertReset(v);
        v.TouchDown(0,0,width,height,iw,ih);Assert.Equal(v.LimitX,v.X);Assert.Equal(v.LimitY,v.Y);
        v.TouchCancel();AssertReset(v);
    }
    [Fact] public void RepeatedPressDoesNotAccumulateAndKeepsTouchedRegion()
    {
        var v=new EvidenceViewport();
        for(var i=0;i<5;i++)
        {
            v.TouchDown(120,80,300,200,300,200);Assert.Equal(2.5,v.Scale);Assert.Equal(45,v.X);Assert.Equal(30,v.Y);
            v.TouchMove(130,90);Assert.Equal(55,v.X);Assert.Equal(40,v.Y);
            v.TouchCancel();AssertReset(v);
        }
    }
    [Fact] public void InvalidGeometryRemainsReset()
    {var v=new EvidenceViewport();v.TouchDown(0,0,0,200,300,200);AssertReset(v);v.TouchDown(double.NaN,0,300,200,300,200);AssertReset(v);}
    private static void AssertReset(EvidenceViewport v){Assert.False(v.Pressed);Assert.Equal(1,v.Scale);Assert.Equal(0,v.X);Assert.Equal(0,v.Y);}
}
