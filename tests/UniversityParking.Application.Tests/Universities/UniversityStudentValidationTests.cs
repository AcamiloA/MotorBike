using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Universities.Integration;
using UniversityParking.Domain.Universities;

namespace UniversityParking.Application.Tests.Universities;

public sealed class UniversityStudentValidationTests
{
    private readonly Catalog catalog = new();
    private UniversityStudentValidationService Service(UniversityIntegrationsOptions? options = null,
        params IUniversityStudentValidator[] providers) => new(catalog, Options.Create(options ?? new()), providers);
    private static UniversityIntegrationsOptions Enabled(Guid id) => new()
    { Universities = [new() { UniversityId = id, Enabled = true }] };

    [Fact]
    public async Task DefaultDoesNotClaimValidationOrCallCatalogOrProvider()
    {
        var provider = new Stub(UniversityIds.Etitc);
        var result = await Service(null, provider).ValidateAsync(UniversityIds.Etitc, "sensitive", default);
        Assert.Equal(UniversityStudentValidationStatus.NOT_CONFIGURED, result.Status);
        Assert.Equal(0, provider.Calls); Assert.Equal(0, catalog.Calls);
    }

    [Fact]
    public async Task DisabledUniversityNeverUsesRegisteredProvider()
    {
        var provider = new Stub(UniversityIds.Etitc);
        var options = Enabled(UniversityIds.Etitc); options.Universities[0].Enabled = false;
        Assert.Equal(UniversityStudentValidationStatus.NOT_CONFIGURED,
            (await Service(options, provider).ValidateAsync(UniversityIds.Etitc, "id", default)).Status);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(UniversityStudentValidationStatus.VALID)]
    [InlineData(UniversityStudentValidationStatus.NOT_FOUND)]
    [InlineData(UniversityStudentValidationStatus.UNAVAILABLE)]
    [InlineData(UniversityStudentValidationStatus.NOT_CONFIGURED)]
    [InlineData(UniversityStudentValidationStatus.ERROR)]
    public async Task PreservesSemanticResultAndMinimalCanonicalRequest(UniversityStudentValidationStatus status)
    {
        var provider = new Stub(UniversityIds.Etitc) { Result = new(status) };
        var other = new Stub(UniversityIds.Cmc);
        var result = await Service(Enabled(UniversityIds.Etitc), other, provider)
            .ValidateAsync(UniversityIds.Etitc, "000123", default);
        Assert.Equal(status, result.Status); Assert.Equal(1, provider.Calls); Assert.Equal(0, other.Calls);
        Assert.Equal(new(UniversityIds.Etitc, "ETITC", "000123"), provider.Request);
        Assert.Equal(new[] { "IdentificationNumber", "UniversityCode", "UniversityId" },
            typeof(UniversityStudentValidationRequest).GetProperties().Select(x => x.Name).Order());
        Assert.Single(typeof(UniversityStudentValidationResult).GetProperties());
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task CmcAndUpnDoNotUseEtitcProvider(bool cmc)
    {
        var provider = new Stub(UniversityIds.Etitc);
        var result = await Service(Enabled(UniversityIds.Etitc), provider)
            .ValidateAsync(cmc ? UniversityIds.Cmc : UniversityIds.Upn, "id", default);
        Assert.Equal(UniversityStudentValidationStatus.NOT_CONFIGURED, result.Status); Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task TwoEnabledUniversitiesRouteByIdNotNameOrOrder()
    {
        var a = new Stub(UniversityIds.Etitc); var b = new Stub(UniversityIds.Cmc);
        var options = Enabled(a.UniversityId); options.Universities.Add(new() { UniversityId = b.UniversityId, Enabled = true });
        await Service(options, a, b).ValidateAsync(b.UniversityId, "id", default);
        Assert.Equal(0, a.Calls); Assert.Equal(1, b.Calls); Assert.Equal("CMC", b.Request!.UniversityCode);
    }

    [Fact]
    public void EnabledWithoutProviderIsConfigurationFailure()
        => Assert.Throws<OptionsValidationException>(() => Service(Enabled(UniversityIds.Etitc)));

    [Fact]
    public void DuplicateProviderFailsEvenWhenDisabled()
        => Assert.Throws<OptionsValidationException>(() => Service(null, new Stub(UniversityIds.Etitc), new Stub(UniversityIds.Etitc)));

    [Fact]
    public void DuplicateConfigurationFails()
    {
        var options = Enabled(UniversityIds.Etitc); options.Universities.Add(new() { UniversityId = UniversityIds.Etitc });
        Assert.Throws<OptionsValidationException>(() => Service(options, new Stub(UniversityIds.Etitc)));
    }

    [Fact]
    public async Task UnknownConfiguredUniversityFailsBeforeTraffic()
    {
        var options = new UniversityIntegrationsOptions { Universities = [new() { UniversityId = Guid.NewGuid() }] };
        await Assert.ThrowsAsync<OptionsValidationException>(() => Service(options).ValidateConfigurationAsync(default));
    }

    [Fact]
    public async Task EnabledUnknownIdIsNotNotFoundOrValid()
    {
        var provider = new Stub(Guid.NewGuid());
        await Assert.ThrowsAsync<OptionsValidationException>(() => Service(Enabled(provider.UniversityId), provider)
            .ValidateAsync(provider.UniversityId, "id", default));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task KnownDisabledConfigurationUsesExistingCatalog()
    {
        await Service(new() { Universities = [new() { UniversityId = UniversityIds.Cmc }] }).ValidateConfigurationAsync(default);
        Assert.Equal(1, catalog.Calls);
    }

    [Fact]
    public async Task PreCanceledDisabledRequestHonorsCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().ValidateAsync(UniversityIds.Etitc, "id", source.Token));
        Assert.Equal(0, catalog.Calls);
    }

    [Fact]
    public async Task ProviderReceivesCancellationAndCallerCancellationIsNotBusinessError()
    {
        using var source = new CancellationTokenSource();
        var provider = new Stub(UniversityIds.Etitc) { Action = (_, token) => { source.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(new UniversityStudentValidationResult(UniversityStudentValidationStatus.VALID)); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(Enabled(provider.UniversityId), provider)
            .ValidateAsync(provider.UniversityId, "id", source.Token));
        Assert.Equal(source.Token, provider.Token);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task TimeoutIsUnavailableAndUnexpectedExceptionIsSanitized(bool timeout)
    {
        var provider = new Stub(UniversityIds.Etitc) { Action = (_, _) => throw (timeout ? new TimeoutException("private") : new InvalidOperationException("private")) };
        var result = await Service(Enabled(provider.UniversityId), provider).ValidateAsync(provider.UniversityId, "sensitive-id-000123", default);
        Assert.Equal(timeout ? UniversityStudentValidationStatus.UNAVAILABLE : UniversityStudentValidationStatus.ERROR, result.Status);
        Assert.DoesNotContain("private", result.ToString()); Assert.DoesNotContain("sensitive-id-000123", result.ToString());
    }

    [Fact]
    public async Task InvalidProviderResultIsError()
    {
        var provider = new Stub(UniversityIds.Etitc) { Result = new((UniversityStudentValidationStatus)999) };
        Assert.Equal(UniversityStudentValidationStatus.ERROR,
            (await Service(Enabled(provider.UniversityId), provider).ValidateAsync(provider.UniversityId, "id", default)).Status);
    }

    [Fact]
    public void RequestStringDoesNotExposeIdentification()
        => Assert.DoesNotContain("sensitive-id", new UniversityStudentValidationRequest(UniversityIds.Etitc, "ETITC", "sensitive-id").ToString());

    [Fact]
    public async Task ProviderTimeoutCancellationWithoutCallerCancellationIsUnavailable()
    {
        var provider = new Stub(UniversityIds.Etitc) { Action = (_, _) => throw new OperationCanceledException() };
        Assert.Equal(UniversityStudentValidationStatus.UNAVAILABLE,
            (await Service(Enabled(provider.UniversityId), provider).ValidateAsync(provider.UniversityId, "sensitive", default)).Status);
    }

    [Fact]
    public async Task LateProviderSuccessCannotIgnoreCallerCancellation()
    {
        using var source = new CancellationTokenSource();
        var provider = new Stub(UniversityIds.Etitc) { Action = (_, _) =>
        { source.Cancel(); return Task.FromResult(new UniversityStudentValidationResult(UniversityStudentValidationStatus.VALID)); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(Enabled(provider.UniversityId), provider)
            .ValidateAsync(provider.UniversityId, "sensitive", source.Token));
    }

    [Fact]
    public void EmptyProviderIdentityFailsConfiguration()
        => Assert.Throws<OptionsValidationException>(() => Service(null, new Stub(Guid.Empty)));

    [Fact]
    public async Task ConfigurationCheckHonorsCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().ValidateConfigurationAsync(source.Token));
    }

    private sealed class Stub(Guid id) : IUniversityStudentValidator
    {
        public Guid UniversityId => id;
        public int Calls { get; private set; }
        public UniversityStudentValidationRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public UniversityStudentValidationResult Result { get; init; } = new(UniversityStudentValidationStatus.VALID);
        public Func<UniversityStudentValidationRequest, CancellationToken, Task<UniversityStudentValidationResult>>? Action { get; init; }
        public Task<UniversityStudentValidationResult> ValidateAsync(UniversityStudentValidationRequest request, CancellationToken token)
        { Calls++; Request = request; Token = token; return Action?.Invoke(request, token) ?? Task.FromResult(Result); }
    }

    private sealed class Catalog : IUniversityRepository
    {
        public int Calls { get; private set; }
        public Task<University?> GetByIdAsync(Guid id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Calls++;
            var code = id == UniversityIds.Etitc ? "ETITC" : id == UniversityIds.Cmc ? "CMC" : id == UniversityIds.Upn ? "UPN" : null;
            return Task.FromResult(code is null ? null : new University(id, code, "Same name", DateTimeOffset.UtcNow));
        }
        public Task<IReadOnlyList<University>> GetActiveAsync(CancellationToken token) => throw new NotSupportedException();
    }
}
