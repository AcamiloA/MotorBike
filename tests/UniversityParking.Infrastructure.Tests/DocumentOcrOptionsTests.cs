using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Documents;

namespace UniversityParking.Infrastructure.Tests;

public sealed class DocumentOcrOptionsTests
{
    private static bool Resolve(IConfiguration config)
    {
        var services=new ServiceCollection();services.AddInfrastructure(config);using var provider=services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<DocumentOcrOptions>>().Value.Enabled;
    }
    private static Dictionary<string,string?> Values()=>new(){["ConnectionStrings:DefaultConnection"]="Host=localhost;Database=options_test;Username=test"};
    [Theory] [InlineData(null,false)] [InlineData("false",false)] [InlineData("true",true)]
    public void TypedBindingDefaultsOffAndHonorsExplicitValues(string? value,bool expected)
    {var values=Values();if(value is not null)values["DocumentOcr:Enabled"]=value;Assert.Equal(expected,Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));}
    [Fact] public void EnvironmentDoubleUnderscoreCanEnableOcr()
    {
        var prefix="MOTORBIKE_OCR_TEST_"+Guid.NewGuid().ToString("N")+"_";var key=prefix+"DocumentOcr__Enabled";
        try{Environment.SetEnvironmentVariable(key,"true");var values=Values();values["DocumentOcr:Enabled"]="false";Assert.True(Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).AddEnvironmentVariables(prefix).Build()));}
        finally{Environment.SetEnvironmentVariable(key,null);}
    }
    [Fact] public void InvalidBooleanFailsInsteadOfSilentlyDisablingOcr()
    {var values=Values();values["DocumentOcr:Enabled"]="invalid";Assert.Throws<InvalidOperationException>(()=>Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));}
}
