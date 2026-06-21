using System.Net;
using GrantAI.Application.Specialties;
using GrantAI.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GrantAI.Tests.Api;

/// <summary>
/// Exercises the real HTTP pipeline (routing, controllers, serialisation, status
/// codes) end to end, with the read facade swapped for an in-memory fake so no
/// database or cache is required. MongoDB index creation at startup is pointed
/// at a fast-failing connection string and is swallowed by the API on purpose.
/// </summary>
public class ApiSmokeTests : IClassFixture<ApiSmokeTests.Factory>
{
    private readonly Factory _factory;

    public ApiSmokeTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task GetStatistics_ReturnsOkWithOverview()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/statistics");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("totalRecords", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSpecialities_ReturnsOkWithList()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/specialities");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(FakeSpecialtyQueryService.KnownCode, body);
    }

    [Fact]
    public async Task GetForecast_KnownCode_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/forecast/{FakeSpecialtyQueryService.KnownCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetForecast_UnknownCode_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/forecast/ZZZ999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetChance_KnownCode_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/chance/{FakeSpecialtyQueryService.KnownCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("passProbabilityPercent", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetChance_UnknownCode_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/chance/ZZZ999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_UnknownCode_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/history/ZZZ999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGrants_ReturnsOkWithList()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/grants");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(FakeGrantQueryService.KnownCode, body);
    }

    [Fact]
    public async Task GetGrantForecast_KnownCode_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/grants/{FakeGrantQueryService.KnownCode}/forecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("predictedCutoff", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetGrantForecast_UnknownCode_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/grants/ZZZ999/forecast");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Fail fast if the (unused) Mongo bootstrap is reached, so tests stay quick.
            builder.UseSetting("Mongo:ConnectionString",
                "mongodb://localhost:27017/?serverSelectionTimeoutMS=300&connectTimeoutMS=300");

            // Make the rate limiter effectively a no-op for the smoke tests so
            // running them in parallel does not trigger 429s.
            builder.UseSetting("RateLimit:Global:PermitLimit", "10000");
            builder.UseSetting("RateLimit:Strict:PermitLimit", "10000");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpecialtyQueryService>();
                services.AddSingleton<ISpecialtyQueryService, FakeSpecialtyQueryService>();
                services.RemoveAll<IGrantQueryService>();
                services.AddSingleton<IGrantQueryService, FakeGrantQueryService>();
            });
        }
    }
}
