using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ApexPay.Service
{
    public class EmailSender
    {
        private const int SendTimeoutSeconds = 5;

        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;
        private readonly IWebHostEnvironment _environment;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            var host = _configuration["Email:Host"];
            var fromAddress = _configuration["Email:FromAddress"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
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
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "ApexPay", fromAddress));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;
                message.Body = new TextPart("plain") { Text = body };

                using var client = new SmtpClient
                {
                    Timeout = SendTimeoutSeconds * 1000
                };

                var useStartTls = _configuration.GetValue("Email:UseStartTls", true);

                await client.ConnectAsync(host, _configuration.GetValue("Email:Port", 587),
                    useStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None, cancellationToken);

                var userName = _configuration["Email:UserName"];

                if (!string.IsNullOrWhiteSpace(userName))
                {
                    await client.AuthenticateAsync(userName, _configuration["Email:Password"] ?? string.Empty, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
            }
        }
    }
}
