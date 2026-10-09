using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Infrastructure.Email;
namespace UniversityParking.Infrastructure.Tests;
public sealed class SmtpOptionsTests
{
    [Fact] public async Task DisabledDeliveryDoesNotConnectEvenWithInvalidHost()
    {
        var services=new ServiceCollection();EmailRegistration.AddEmailDelivery(services,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["EmailDelivery:Enabled"]="false",["Smtp:Host"]="must-not-connect.invalid"}).Build());
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var sender=scope.ServiceProvider.GetRequiredService<IEmailSender>();Assert.False(sender.IsEnabled);await Assert.ThrowsAsync<EmailDeliveryException>(()=>sender.EnsureAvailableAsync(default));
        await Assert.ThrowsAsync<EmailDeliveryException>(()=>sender.SendAsync("unused@example.org","unused","unused",default));
        Assert.Equal("",provider.GetRequiredService<IOptions<SmtpOptions>>().Value.Password);
    }
    [Fact] public void EnabledDeliveryRejectsMissingConfiguration()
    {
        var services=new ServiceCollection();EmailRegistration.AddEmailDelivery(services,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["EmailDelivery:Enabled"]="true"}).Build());using var provider=services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(()=>provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }
    [Fact] public void ExactRailwayEnvironmentNamesBindAndResolveSmtpWithoutConnecting()
    {
        var prefix="MotorBikeSmtpTest_"+Guid.NewGuid().ToString("N")+"_";
        var values=new Dictionary<string,string>{["EmailDelivery__Enabled"]="true",["EmailDelivery__Provider"]="Smtp",["Smtp__Host"]="smtp.gmail.com",["Smtp__Port"]="587",["Smtp__UseStartTls"]="true",["Smtp__Username"]="synthetic@example.org",["Smtp__Password"]="synthetic-test-value",["Smtp__FromEmail"]="synthetic@example.org",["Smtp__FromName"]="MotorBike Park"};
        try
        {
            foreach(var pair in values)Environment.SetEnvironmentVariable(prefix+pair.Key,pair.Value);
            var services=new ServiceCollection();EmailRegistration.AddEmailDelivery(services,new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build());
            using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
            Assert.IsType<SmtpEmailSender>(scope.ServiceProvider.GetRequiredService<IEmailSender>());
            Assert.True(scope.ServiceProvider.GetRequiredService<IEmailSender>().IsEnabled);
            var smtp=provider.GetRequiredService<IOptions<SmtpOptions>>().Value;
            Assert.Equal("smtp.gmail.com",smtp.Host);Assert.Equal(587,smtp.Port);Assert.True(smtp.UseStartTls);
            Assert.Equal(values["Smtp__Username"],smtp.Username);Assert.Equal(values["Smtp__Password"],smtp.Password);
            Assert.Equal(values["Smtp__FromEmail"],smtp.FromEmail);Assert.Equal(values["Smtp__FromName"],smtp.FromName);
        }
        finally{foreach(var key in values.Keys)Environment.SetEnvironmentVariable(prefix+key,null);}
    }
    [Theory] [InlineData("Host","")] [InlineData("Port","0")] [InlineData("Port","65536")]
    [InlineData("Username","")] [InlineData("Password","")] [InlineData("FromEmail","invalid")]
    public void InvalidEnabledConfigurationHasSanitizedErrors(string key,string value)
    {
        var data=new Dictionary<string,string?>{["EmailDelivery:Enabled"]="true",["EmailDelivery:Provider"]="Smtp",["Smtp:Host"]="synthetic.invalid",["Smtp:Port"]="587",["Smtp:Username"]="synthetic-user",["Smtp:Password"]="synthetic-test-secret",["Smtp:FromEmail"]="synthetic@example.org"};data["Smtp:"+key]=value;
        var services=new ServiceCollection();EmailRegistration.AddEmailDelivery(services,new ConfigurationBuilder().AddInMemoryCollection(data).Build());using var provider=services.BuildServiceProvider();
        var error=Assert.Throws<OptionsValidationException>(()=>provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
        Assert.DoesNotContain("synthetic-test-secret",error.ToString());Assert.DoesNotContain("synthetic-user",error.ToString());
        Assert.DoesNotContain("synthetic",new EmailDeliveryException().ToString());
    }
    [Fact] public void UnknownEnabledProviderIsRejected()
    {
        var services=new ServiceCollection();EmailRegistration.AddEmailDelivery(services,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["EmailDelivery:Enabled"]="true",["EmailDelivery:Provider"]="Unknown"}).Build());using var provider=services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(()=>provider.GetRequiredService<IOptions<EmailDeliveryOptions>>().Value);
    }
}
