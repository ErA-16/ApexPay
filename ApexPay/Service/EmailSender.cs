using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApexPay.Service
{
    public class EmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;
        private readonly IWebHostEnvironment _environment;

        public EmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<EmailSender> logger, IWebHostEnvironment environment)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var senderEmail = _configuration["Brevo:SenderEmail"];

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("Email is not configured. Message for {To}: {Body}", to, body);
                }
                else
                {
                    _logger.LogError("Email is not configured, message to {To} was not sent", to);
                }

                return;
            }

            try
            {
                var payload = new
                {
                    sender = new
                    {
                        email = senderEmail,
                        name = _configuration["Brevo:SenderName"] ?? "ApexPay"
                    },
                    to = new[] { new { email = to } },
                    subject,
                    textContent = body
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("api-key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send email to {To}: {StatusCode} {Error}", to, (int)response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
            }
        }
    }
}
