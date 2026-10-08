using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Tests.Common;

public sealed class DependencyInjectionTests
{
    public sealed record TestCommand(string Name) : ICommand<string>;
    public sealed class TestHandler : IRequestHandler<TestCommand, Result<string>>
    {
        public bool Executed { get; private set; }
        public Task<Result<string>> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            Executed = true;
            return Task.FromResult(Result<string>.Success(request.Name));
        }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Camilo", true)]
    public async Task AddApplication_ShouldRunValidationThroughMediator(string name, bool valid)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x.Name).NotEmpty();
        services.AddSingleton<IValidator<TestCommand>>(validator);
        var handler = new TestHandler();
        services.AddSingleton<IRequestHandler<TestCommand, Result<string>>>(handler);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IValidator<PageRequest>>());
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new TestCommand(name));
        Assert.Equal(valid, result.IsSuccess);
        Assert.Equal(valid, handler.Executed);
    }
}
