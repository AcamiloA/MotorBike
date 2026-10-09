using System.Net;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.News;
using UniversityParking.Mobile.Core;
using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Tests;

public sealed partial class UserFeatureTests
{
    private static NewsResponse Communique()=>new(Guid.NewGuid(),"Comunicado","Contenido completo del comunicado","PUBLISHED",DateTimeOffset.UtcNow,null,Guid.NewGuid(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
    [Fact] public async Task HomeOnlyLoadsNewsAndOpensExistingDetailRoute()
    {
        var news=Communique();var service=Service(r=>{Assert.Equal("/api/v1/news",r.RequestUri!.AbsolutePath);return Task.FromResult(Ok(new PagedResponse<NewsResponse>([news],1,20,1,1)));});
        var navigation=new Navigation();var vm=new UserHomeViewModel(service.Api,navigation,await Session());await vm.LoadCommand.ExecuteAsync(null);
        var card=Assert.Single(vm.RecentNews);Assert.Equal(news.Title,card.Title);Assert.Equal(news.Content,card.Item.Content);Assert.NotEmpty(card.Published);
        Assert.False(vm.CanRetry);Assert.False(vm.IsNewsEmpty);Assert.Equal(1,service.Transport.Count);
        await vm.OpenNewsCommand.ExecuteAsync(card);Assert.Equal("news-detail",navigation.Route);
        Assert.Contains(NavigationMenu.Catalog,x=>x.Key=="my-vehicles");Assert.Contains(NavigationMenu.Catalog,x=>x.Key=="my-history");Assert.Contains(NavigationMenu.Catalog,x=>x.Key=="user-profile");
    }
    [Fact] public async Task EmptyNewsHasEmptyStateWithoutRetry()
    {
        var service=Service(_=>Task.FromResult(Ok(new PagedResponse<NewsResponse>([],1,20,0,0))));
        var vm=new UserHomeViewModel(service.Api,new Navigation(),await Session());await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.IsNewsEmpty);Assert.False(vm.CanRetry);Assert.Empty(vm.ErrorMessage);Assert.Empty(vm.RecentNews);
    }
    [Fact] public async Task NewsErrorEnablesRetryAndRecoveryRemovesIt()
    {
        var recover=false;var service=Service(_=>Task.FromResult(recover?Ok(new PagedResponse<NewsResponse>([Communique()],1,20,1,1)):new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var vm=new UserHomeViewModel(service.Api,new Navigation(),await Session());await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.CanRetry);Assert.False(vm.IsNewsEmpty);Assert.NotEmpty(vm.ErrorMessage);
        recover=true;await vm.LoadCommand.ExecuteAsync(null);Assert.False(vm.CanRetry);Assert.Empty(vm.ErrorMessage);Assert.Single(vm.RecentNews);
    }
    private sealed class RoutedPicker:IAttachmentPicker
    {
        public int Guided,Normal;
        public Task<PickedAttachment?> TransitLicenseAsync(){Guided++;return Task.FromResult<PickedAttachment?>(Photo with{FileName="crop.png"});}
        public Task<PickedAttachment?> PhotoAsync(bool camera){Normal++;return Task.FromResult<PickedAttachment?>(Photo);}
        public Task<PickedAttachment?> DocumentAsync()=>Task.FromResult<PickedAttachment?>(null);
    }
    [Theory] [InlineData("CAR",true,1,0)] [InlineData("MOTORCYCLE",true,1,0)] [InlineData("BICYCLE",true,0,1)] [InlineData("MOTORCYCLE",false,0,1)]
    public async Task GuidedCaptureIsExclusiveToMotorVehicleCamera(string type,bool camera,int guided,int normal)
    {
        var picker=new RoutedPicker();var service=Service(_=>throw new Exception());var vm=new RegisterVehicleViewModel(service.Api,await Session("STAFF"),picker,new Navigation());
        vm.SelectedType=vm.Types.Single(x=>x.Code==type);await vm.PickPhotoCommand.ExecuteAsync(camera);
        Assert.Equal(guided,picker.Guided);Assert.Equal(normal,picker.Normal);Assert.Equal(guided==1?"crop.png":"photo.png",vm.VerificationImage!.FileName);
    }
}
