using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ApexPayFactory : WebApplicationFactory<Program>
{
    private readonly int _permitLimit;

    public ApexPayFactory() : this(100000)
    {
    }

    protected ApexPayFactory(int permitLimit)
    {
        _permitLimit = permitLimit;
    }

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Auth:PermitLimit"] = _permitLimit.ToString(),
                ["RateLimit:Auth:WindowSeconds"] = "60",
                ["Brevo:ApiKey"] = "",
                ["Brevo:SenderEmail"] = ""
            });
        });

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }
}

public class LowRateLimitFactory : ApexPayFactory
{
    public LowRateLimitFactory() : base(2)
    {
    }
}

public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Snapshot() => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public CapturingLogger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }
    }
}
