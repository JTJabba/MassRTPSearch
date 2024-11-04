using System.Net.Http.Json;

public record PerplexityRequest(
    string Model,
    IReadOnlyList<Message> Messages,
    int? MaxTokens = null,
    double Temperature = 0.2,
    double TopP = 0.9,
    bool ReturnCitations = true,
    IReadOnlyList<string>? SearchDomainFilter = null,
    bool ReturnImages = false,
    bool ReturnRelatedQuestions = false,
    string? SearchRecencyFilter = null,
    int TopK = 0,
    bool Stream = false,
    int PresencePenalty = 0,
    int FrequencyPenalty = 1
);

public record Message(string Role, string Content);

public record PerplexityResponse(
    string Id,
    string Model,
    string Object,
    long Created,
    IReadOnlyList<Choice> Choices,
    Usage Usage
);

public record Choice(
    int Index,
    string FinishReason,
    Message Message,
    Delta Delta
);

public record Delta(string Role, string Content);

public record Usage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);

public record ErrorResponse(IReadOnlyList<ErrorDetail> Detail);

public record ErrorDetail(
    IReadOnlyList<string> Loc,
    string Msg,
    string Type
);

public class PerplexityClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.perplexity.ai";

    public PerplexityClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<PerplexityResponse> CompleteAsync(PerplexityRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/chat/completions", request);
        Console.WriteLine($"Response Status Code: {response.StatusCode}");
        
        var rawContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Raw Response Content: {rawContent}");
        
        if (response.IsSuccessStatusCode)
        {
            var perplexityResponse = await response.Content.ReadFromJsonAsync<PerplexityResponse>();
            if (perplexityResponse == null)
                throw new InvalidOperationException($"Received null response from API. Raw content: {rawContent}");
                
            return perplexityResponse;
        }

        throw new HttpRequestException(
            $"API request failed ({response.StatusCode}). Response: {rawContent}"
        );
    }
}
