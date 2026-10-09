using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ShopFlow.IntegrationTests.Identity;

public class IdentityTests : IClassFixture<ShopFlowApplicationFactory>
{
    private readonly ShopFlowApplicationFactory _factory;

    public IdentityTests(ShopFlowApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Valid_Returns201()
    {
        var client = _factory.CreateClient();
        var email = $"test{Guid.NewGuid()}@example.com";
        var res = await client.PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });
        Assert.Equal(System.Net.HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"duplicate{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });
        
        var res = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password = "Password123!" });
        Assert.Equal(System.Net.HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Login_Valid_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var email = $"login{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });
        
        var res = await client.PostAsJsonAsync("/auth/login", new { email, password = "Password123!" });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        
        var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = json.GetProperty("accessToken").GetString();
        Assert.NotNull(token);
        
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"invalid{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });
        
        var res = await client.PostAsJsonAsync("/auth/login", new { email, password = "WrongPassword!" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Register_ConcurrentRequests_OnlyOneSucceeds()
    {
        var client = _factory.CreateClient();
        var email = $"concurrent{Guid.NewGuid()}@example.com";
        var payload = new { email, password = "Password123!" };
        
        var tasks = Enumerable.Range(0, 20).Select(_ => client.PostAsJsonAsync("/auth/register", payload));
        var results = await Task.WhenAll(tasks);
        
        Assert.Single(results, r => r.StatusCode == System.Net.HttpStatusCode.Created);
        Assert.Equal(19, results.Count(r => r.StatusCode == System.Net.HttpStatusCode.Conflict));
    }
}
