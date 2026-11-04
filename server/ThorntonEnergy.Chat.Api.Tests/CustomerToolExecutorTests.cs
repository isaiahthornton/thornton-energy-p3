using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using ThorntonEnergy.Chat.Api;
using ThorntonEnergy.Chat.Api.Services;

namespace ThorntonEnergy.Chat.Api.Tests;

public class CustomerToolExecutorTests
{
    [Fact]
    public async Task OutageCreationRequiresExactConfirmationBeforeCallingApi()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);
        var call = new AgentToolCall("call-1", "report_outage",
            """{"address":"12 Utility Road","description":"No power at the house"}""");

        var result = await executor.ExecuteAsync(call, 42, "customer-jwt", null, CancellationToken.None);

        Assert.NotNull(result.ConfirmationRequired);
        Assert.Equal("12 Utility Road", result.ConfirmationRequired.Address);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ConfirmedOutageForwardsCustomerBearerAndOmitsCustomerId()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);
        var call = new AgentToolCall("call-2", "report_outage",
            """{"address":"12 Utility Road","description":"No power at the house"}""");
        var confirmation = new OutageConfirmation(true, "12 Utility Road", "No power at the house");

        var result = await executor.ExecuteAsync(call, 42, "customer-jwt", confirmation, CancellationToken.None);

        Assert.Null(result.ConfirmationRequired);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://outages:8080/api/outages", handler.Request?.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Request?.Headers.Authorization?.Scheme);
        Assert.Equal("customer-jwt", handler.Request?.Headers.Authorization?.Parameter);
        Assert.Contains("\"customerId\":null", handler.Body);
    }

    [Fact]
    public async Task ConfirmationForDifferentDetailsNeverCallsApi()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);
        var call = new AgentToolCall("call-3", "report_outage",
            """{"address":"12 Utility Road","description":"No power at the house"}""");
        var confirmation = new OutageConfirmation(true, "12 Utility Road", "Different issue");

        var result = await executor.ExecuteAsync(call, 42, "customer-jwt", confirmation, CancellationToken.None);

        Assert.NotNull(result.ConfirmationRequired);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task RatePlansToolReadsThePublicPlanList()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);

        await executor.ExecuteAsync(
            new AgentToolCall("call-6", "get_rate_plans", "{}"), 42, "customer-jwt", null, CancellationToken.None);

        Assert.Equal("http://customers:8080/api/ratePlans", handler.Request?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ConfirmationIgnoresExtraSpacesAndCapitalLetters()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);
        var call = new AgentToolCall("call-7", "report_outage",
            """{"address":"12 Utility Road","description":"No power at the house"}""");
        var confirmation = new OutageConfirmation(true, "12  utility road ", "No power  AT the house");

        var result = await executor.ExecuteAsync(call, 42, "customer-jwt", confirmation, CancellationToken.None);

        Assert.Null(result.ConfirmationRequired);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
    }

    [Theory]
    [InlineData("create_meter_reading")]
    [InlineData("mark_bill_paid")]
    [InlineData("get_admin_outage_map")]
    public async Task NonAllowListedToolsAreDenied(string name)
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);

        var result = await executor.ExecuteAsync(
            new AgentToolCall("call-4", name, "{}"), 42, "customer-jwt", null, CancellationToken.None);

        Assert.Contains("not available", result.Content);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ProfileIdIsDerivedByExecutorRatherThanToolArguments()
    {
        var handler = new RecordingHandler();
        var executor = CreateExecutor(handler);

        await executor.ExecuteAsync(
            new AgentToolCall("call-5", "get_profile", """{"customerId":9000}"""),
            42, "customer-jwt", null, CancellationToken.None);

        Assert.Equal("http://customers:8080/api/customers/42", handler.Request?.RequestUri?.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "customer-jwt"),
            handler.Request?.Headers.Authorization);
    }

    private static CustomerToolExecutor CreateExecutor(RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:Customers"] = "http://customers:8080",
            ["Services:Metering"] = "http://metering:8080",
            ["Services:Billing"] = "http://billing:8080",
            ["Services:Outages"] = "http://outages:8080"
        }).Build();
        return new CustomerToolExecutor(new HttpClient(handler), configuration);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"id":123}""")
            };
        }
    }
}
