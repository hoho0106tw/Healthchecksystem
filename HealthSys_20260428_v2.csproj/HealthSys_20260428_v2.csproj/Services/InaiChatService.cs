using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace HealthSys_20260428_v2.csproj.Services;

public sealed class InaiOptions
{
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiKeyEnvironmentVariable { get; set; } = "INAI_API_KEY";
    public string AuthMode { get; set; } = "ApiKeyOnly";
    public string ApiKeyFormat { get; set; } = "Plain";
    public int RequestTimeoutSeconds { get; set; } = 90;
    public bool ChatLogEnabled { get; set; }
    public bool ChatLogDbWriteEnabled { get; set; }
}

public sealed record InaiMessage(string Role, string Content);
public sealed record InaiRequest(List<InaiMessage> Messages);

public sealed class InaiChatService(IOptionsMonitor<InaiOptions> settings)
{
    public async Task<string> ReplyAsync(IEnumerable<InaiMessage> history, CancellationToken cancellationToken, string? systemPrompt = null)
    {
        _ = settings;
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        if (systemPrompt is not null)
            return "{\"action\":\"clarify\",\"message\":\"目前為假資料示範模式，請使用頁面上的查詢欄位操作。\",\"fields\":{}}";
        return "目前為假資料示範模式。可從預約管理選取示範受檢者，操作掛號、報到及查詢檢查報告。所有資料皆為虛構，重啟後會重設。";
#if false // Original hospital integration retained for reference.

        var options = settings.CurrentValue;
        var key = string.IsNullOrWhiteSpace(options.ApiKeyEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(options.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(key)) key = options.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("INAI credential is not configured.");
        key = options.ApiKeyFormat switch
        {
            "Plain" => key,
            "Base64Decoded" => Encoding.UTF8.GetString(Convert.FromBase64String(key)),
            "Base64CredentialSecret" => ExtractSecret(key),
            _ => throw new InvalidOperationException("Unsupported INAI key format.")
        };
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"
            || string.IsNullOrWhiteSpace(options.Model)) throw new InvalidOperationException("Invalid INAI configuration.");
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint,
            NetworkTimeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 180)),
            RetryPolicy = new ClientRetryPolicy(0)
        };
        // The SDK creates the request; this transport policy adapts the gateway authentication.
        clientOptions.AddPolicy(new GatewayAuthenticationPolicy(key, options.AuthMode), PipelinePosition.BeforeTransport);
        var client = new ChatClient(options.Model, new ApiKeyCredential(key), clientOptions);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt ?? "你是健檢系統的智能查詢助理。請使用繁體中文回答。你只能根據使用者提供的內容回答，沒有系統資料庫或病歷的存取能力，不得宣稱已查詢即時資料。資訊不足時請明確說明並請使用者補充。")
        };
        foreach (var message in history)
            messages.Add(message.Role == "user" ? new UserChatMessage(message.Content) : new AssistantChatMessage(message.Content));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 180)));
        ChatCompletion result = await client.CompleteChatAsync(messages, cancellationToken: timeout.Token);
        var reply = string.Concat(result.Content.Select(part => part.Text));
        if (string.IsNullOrWhiteSpace(reply)) throw new InvalidOperationException("INAI returned an empty response.");
        return reply;
    
#endif
    }

    private static string ExtractSecret(string key)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(key));
        var colon = decoded.IndexOf(':');
        if (colon < 0 || colon == decoded.Length - 1) throw new InvalidOperationException("Invalid INAI credential format.");
        return decoded[(colon + 1)..];
    }

    private sealed class GatewayAuthenticationPolicy(string key, string mode) : PipelinePolicy
    {
        private void Apply(PipelineMessage message)
        {
            message.Request.Headers.Remove("Authorization");
            message.Request.Headers.Remove("api-key");
            switch (mode)
            {
                case "ApiKeyOnly": message.Request.Headers.Set("api-key", key); break;
                case "ApiKeyAndBearer":
                    message.Request.Headers.Set("api-key", key);
                    message.Request.Headers.Set("Authorization", "Bearer " + key); break;
                case "BearerOnly": message.Request.Headers.Set("Authorization", "Bearer " + key); break;
                case "Basic": message.Request.Headers.Set("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(key))); break;
                default: throw new InvalidOperationException("Unsupported INAI authentication mode.");
            }
        }
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            ProcessNext(message, pipeline, currentIndex);
        }
        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
        }
    }
}
