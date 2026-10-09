using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Auth.Registration;

namespace UniversityParking.Infrastructure.Tests;

public sealed class StudentRegistrationOptionsTests
{
    private static bool Resolve(IConfiguration configuration)
    {
        var services=new ServiceCollection();services.AddInfrastructure(configuration);
        using var provider=services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<StudentRegistrationOptions>>().Value.AutoApprove;
    }

    [Theory][InlineData(null,false)][InlineData("false",false)][InlineData("true",true)]
    public void BindingSupportsDefaultAndExplicitConfiguration(string? setting,bool expected)
    {
        var values=new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]="Host=localhost;Database=options_test;Username=test" };
        if(setting is not null)values["StudentRegistration:AutoApprove"]=setting;
        Assert.Equal(expected,Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Fact] public void EnvironmentDoubleUnderscoreOverridesDefault()
    {
        var prefix="MOTORBIKE_R1_TEST_"+Guid.NewGuid().ToString("N")+"_";
        var key=prefix+"StudentRegistration__AutoApprove";
        try
        {
            Environment.SetEnvironmentVariable(key,"true");
            var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:DefaultConnection"]="Host=localhost;Database=options_test;Username=test",["StudentRegistration:AutoApprove"]="false" })
                .AddEnvironmentVariables(prefix).Build();
            Assert.True(Resolve(configuration));
        }
        finally { Environment.SetEnvironmentVariable(key,null); }
    }

    [Fact] public void InvalidBooleanConfigurationFailsInsteadOfApproving()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:DefaultConnection"]="Host=localhost;Database=options_test;Username=test",["StudentRegistration:AutoApprove"]="invalid" }).Build();
        Assert.Throws<InvalidOperationException>(()=>Resolve(configuration));
    }
}
