using System.Net;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat")]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ICurrentUserService currentUser,
    ILogger<ChatController> logger)
    : ControllerBase
{
    private string GetAssistantServiceUrl() =>
        configuration["Services:CustomerAssistant:Url"] ?? "http://localhost:3002";

    private HttpClient CreateConfiguredClient()
    {
        var client = httpClientFactory.CreateClient("CustomerAssistant");
        client.BaseAddress = new Uri(GetAssistantServiceUrl());
        client.Timeout = TimeSpan.FromSeconds(60);

        if (currentUser.TenantId.HasValue)
            client.DefaultRequestHeaders.Add("x-tenant-id", currentUser.TenantId.Value.ToString());

        if (currentUser.UserId.HasValue)
            client.DefaultRequestHeaders.Add("x-user-id", currentUser.UserId.Value.ToString());

        client.DefaultRequestHeaders.Add("x-actor-type", "STAFF");

        if (!string.IsNullOrEmpty(currentUser.TraceId))
            client.DefaultRequestHeaders.Add("x-trace-id", currentUser.TraceId);

        return client;
    }

    [HttpPost("conversations")]
    [ProducesResponseType(typeof(JsonElement), 201)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateConfiguredClient();
            var content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/chat/conversations", content, cancellationToken);

            var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, JsonDocument.Parse(resContent).RootElement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create conversation with CustomerAssistant service.");
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "ASSISTANT_UNAVAILABLE",
                Detail = "Customer assistant service is temporarily unreachable.",
                Status = (int)HttpStatusCode.BadGateway,
            });
        }
    }

    [HttpGet("conversations")]
    [ProducesResponseType(typeof(JsonElement), 200)]
    public async Task<IActionResult> ListConversations(CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateConfiguredClient();
            var response = await client.GetAsync("/api/chat/conversations", cancellationToken);

            var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, JsonDocument.Parse(resContent).RootElement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list conversations from CustomerAssistant service.");
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "ASSISTANT_UNAVAILABLE",
                Detail = "Customer assistant service is temporarily unreachable.",
                Status = (int)HttpStatusCode.BadGateway,
            });
        }
    }

    [HttpGet("conversations/{id}")]
    [ProducesResponseType(typeof(JsonElement), 200)]
    public async Task<IActionResult> GetConversation(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateConfiguredClient();
            var response = await client.GetAsync($"/api/chat/conversations/{id}", cancellationToken);

            var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, JsonDocument.Parse(resContent).RootElement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get conversation {ConversationId} from CustomerAssistant service.", id);
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "ASSISTANT_UNAVAILABLE",
                Detail = "Customer assistant service is temporarily unreachable.",
                Status = (int)HttpStatusCode.BadGateway,
            });
        }
    }

    [HttpPost("conversations/{id}/messages")]
    [ProducesResponseType(typeof(JsonElement), 200)]
    public async Task<IActionResult> SendMessage(
        [FromRoute] string id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateConfiguredClient();
            var content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/chat/conversations/{id}/messages", content, cancellationToken);

            var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, JsonDocument.Parse(resContent).RootElement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to conversation {ConversationId}.", id);
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "ASSISTANT_UNAVAILABLE",
                Detail = "Customer assistant service is temporarily unreachable.",
                Status = (int)HttpStatusCode.BadGateway,
            });
        }
    }
}
