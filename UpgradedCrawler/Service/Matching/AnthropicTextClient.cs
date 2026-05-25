using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace UpgradedCrawler.Service.Matching;

public class AnthropicTextClient(string apiKey) : IAiTextClient
{
    private readonly AnthropicClient _client = new(apiKey);

    public async Task<string> CompleteAsync(string model, string system, string user, int maxTokens = 2000)
    {
        var parameters = new MessageParameters
        {
            Model = model,
            MaxTokens = maxTokens,
            System = [new SystemMessage(system)],
            Messages =
            [
                new Message { Role = RoleType.User, Content = [new TextContent { Text = user }] }
            ]
        };
        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        return response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
