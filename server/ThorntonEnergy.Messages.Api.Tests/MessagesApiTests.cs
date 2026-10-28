using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Messages.Api.Tests;

// Real HTTP requests against the real Messages service (in memory): the rules as a client sees them.
// Ported from the monolith's ApiTests / ApiWorkflowTests message tests.
public class MessagesApiTests(MessagesApiFactory factory) : IClassFixture<MessagesApiFactory>
{
    private HttpClient Anonymous() => factory.CreateClient();

    [Fact]
    public async Task Inbox_WithoutAToken_Returns401()
    {
        // Act
        var response = await Anonymous().GetAsync("/api/messages");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ContactForm_WithABadEmail_Returns400()
    {
        // Act
        var response = await Anonymous().PostAsJsonAsync("/api/messages",
            new { name = "A", email = "not-an-email", topic = "billing", message = "Hello" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContactForm_WithValidData_Returns201_AndOnlyAdminsCanReadTheInbox()
    {
        // Act: anyone can send
        var sent = await Anonymous().PostAsJsonAsync("/api/messages",
            new { name = "A", email = "a@test.com", topic = "billing", message = "Hello" });
        var customerInbox = await factory.SignedInAs(UserRole.Customer, customerId: 1).GetAsync("/api/messages");

        // Assert
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerInbox.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadAndMarkMessagesAsRead()
    {
        // Arrange: anyone can send, the server decides the status
        var sent = await Anonymous().PostAsJsonAsync("/api/messages",
            new { name = "Sam", email = "sam@test.com", topic = "outage", message = "Hello", status = "read" });
        var sentBody = await sent.Content.ReadFromJsonAsync<JsonElement>();
        var id = sentBody.GetProperty("id").GetInt32();
        var admin = factory.SignedInAs(UserRole.Admin);

        // Act
        var read = await admin.PatchAsJsonAsync($"/api/messages/{id}", new { status = "read" });
        var one = await admin.GetFromJsonAsync<JsonElement>($"/api/messages/{id}");
        var all = await admin.GetFromJsonAsync<JsonElement>("/api/messages");

        // Assert
        Assert.Equal("new", sentBody.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("read", one.GetProperty("status").GetString());
        Assert.True(all.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Admin_GettingAMissingMessage_Returns404()
    {
        // Act
        var response = await factory.SignedInAs(UserRole.Admin).GetAsync("/api/messages/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthChecks_ArePublic()
    {
        // Act
        var live = await Anonymous().GetAsync("/health/live");
        var ready = await Anonymous().GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}
