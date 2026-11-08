using SmartAgro.IoT.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace SmartAgro.IoT
{
    public class JwtAuthenticationHandler : DelegatingHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtAuthenticationHandler> _logger;
        private string? _currentToken;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public JwtAuthenticationHandler(IConfiguration configuration, ILogger<JwtAuthenticationHandler> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 1. If the token is missing, we retrieve it
            if (string.IsNullOrEmpty(_currentToken))
            {
                await AuthenticateAsync(cancellationToken);
            }

            // 2. Adding a token to the request
            if (!string.IsNullOrEmpty(_currentToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken);
            }

            var response = await base.SendAsync(request, cancellationToken);

            // 3.  Fallback/Retry: if a 401 Unauthorized response is received, refresh the token and retry the request
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized. Refreshing JWT token...");

                await AuthenticateAsync(cancellationToken);

                if (!string.IsNullOrEmpty(_currentToken))
                {
                    // creating a new request (because the old one has already been sent)
                    var newRequest = await CloneHttpRequestMessageAsync(request);
                    newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken);

                    response = await base.SendAsync(newRequest, cancellationToken);
                }
            }

            return response;
        }

        private async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var baseUrl = _configuration["IotSettings:BaseUrl"];
                var username = _configuration["IotSettings:Username"];
                var password = _configuration["IotSettings:Password"];

                using var authClient = new HttpClient();
                var loginPayload = new LoginRequest { Username = username!, Password = password! };

                var response = await authClient.PostAsJsonAsync($"{baseUrl}/api/auth/login", loginPayload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
                    _currentToken = result?.Token;
                    _logger.LogInformation("Successfully authenticated. Token retrieved.");
                }
                else
                {
                    _logger.LogError("Authentication failed with status code: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during authentication.");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            if (request.Content != null)
            {
                var ms = new MemoryStream();
                await request.Content.CopyToAsync(ms);
                ms.Position = 0;

                clone.Content = new StreamContent(ms);
                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.Add(header.Key, header.Value);
                }
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
