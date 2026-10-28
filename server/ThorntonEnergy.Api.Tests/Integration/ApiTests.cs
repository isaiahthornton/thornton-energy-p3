using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Api.Tests.Helpers;

namespace ThorntonEnergy.Api.Tests.Integration;

// Real HTTP requests against the real app (in memory): the security rules as a client sees them.
// One factory (one in-memory database) is shared by all tests in this class, so each test that
// changes data uses data no other test touches.
public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Anonymous() => factory.CreateClient();

    // Logging in is rate limited (10 per minute), so each user logs in once and the token is reused
    private static readonly Dictionary<string, string> Tokens = [];

    // Logs in through the real endpoint and returns a client that sends the token
    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = factory.CreateClient();

        string? token;
        lock (Tokens) Tokens.TryGetValue(email, out token);

        if (token is null)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestData.Password });
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            token = body.GetProperty("token").GetString()!;
            lock (Tokens) Tokens[email] = token;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- authentication ----------

    [Fact]
    public async Task Request_WithoutAToken_Returns401()
    {
        // Act
        var response = await Anonymous().GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAGarbageToken_Returns401()
    {
        // Arrange
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        // Act
        var response = await client.GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_Returns401()
    {
        // Act
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = "jane@test.com", password = "wrong" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsATokenAndTheUser()
    {
        // Act
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = "jane@test.com", password = TestData.Password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("jane@test.com", body.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("customer", body.GetProperty("user").GetProperty("role").GetString()); // lower-case, like Angular expects
    }

    [Fact]
    public async Task Me_ReturnsTheSignedInUser()
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var body = await jane.GetFromJsonAsync<JsonElement>("/api/auth/me");

        // Assert
        Assert.Equal("jane@test.com", body.GetProperty("email").GetString());
        Assert.Equal(1, body.GetProperty("customerId").GetInt32());
    }

    // ---------- authorization: roles ----------

    [Fact]
    public async Task Customer_CallingAnAdminEndpoint_Returns403()
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var response = await jane.GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CallingAnAdminEndpoint_Returns200()
    {
        // Arrange
        var admin = await SignedInAsync("admin@test.com");

        // Act
        var response = await admin.GetAsync("/api/customers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customers = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, customers.GetArrayLength());
    }

    // The admin insight endpoints read a SQL Server view and procedure, which SQLite does not have.
    // Their access rules are checked before any database work, so we can still test who gets in.
    [Theory]
    [InlineData("/api/admin/kpis")]
    [InlineData("/api/admin/usage-trend")]
    [InlineData("/api/admin/activity")]
    public async Task AdminInsightEndpoints_RefuseCustomersAndAnonymousCallers(string url)
    {
        // Arrange
        var jane = await SignedInAsync("jane@test.com");

        // Act
        var asCustomer = await jane.GetAsync(url);
        var anonymous = await Anonymous().GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task RatePlans_ArePublic()
    {
        // Act: no token at all
        var response = await Anonymous().GetAsync("/api/ratePlans");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- rules the server enforces ----------

    [Fact]
    public async Task Register_WithAMalformedAccountNumber_Returns400()
    {
        // Act
        var response = await Anonymous().PostAsJsonAsync("/api/auth/register",
            new { accountNumber = "12345", email = "new@test.com", password = "abcdef" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Responses_CarryACorrelationId()
    {
        // Act
        var response = await Anonymous().GetAsync("/api/ratePlans");

        // Assert
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
