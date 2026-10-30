using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Restaurant.Aqa.Configuration;

namespace Restaurant.Aqa.Api;

public sealed class AqaApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri($"{AqaConfiguration.Current.ApiUrl}/")
    };

    public async Task<SignInResponse> SignInAsync(string email, string password)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "signin",
            new { email, password });

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Sign-in failed with {(int)response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<SignInResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Sign-in response was empty.");
    }

    public async Task<T> GetJsonAsync<T>(string path, string? bearerToken = null)
    {
        using var response = await SendAsync(HttpMethod.Get, path, bearerToken: bearerToken);
        return await ReadRequiredJsonAsync<T>(response);
    }

    public async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object body,
        string? bearerToken = null)
    {
        using var response = await SendAsync(method, path, body, bearerToken);
        return await ReadRequiredJsonAsync<T>(response);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        try
        {
            return await _httpClient.SendAsync(request);
        }
        finally
        {
            request.Dispose();
        }
    }

    public async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API request failed with {(int)response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("API response was empty.");
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record SignInResponse(string IdToken, string AccessToken);
