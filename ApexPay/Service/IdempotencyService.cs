using ApexPay.Database;
using ApexPay.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApexPay.Service
{
    public class IdempotencyService
    {
        private readonly TimeSpan _staleAfter;
        private readonly ApexPayDbContext _db;

        public IdempotencyService(ApexPayDbContext db, IConfiguration configuration)
        {
            _db = db;
            _staleAfter = TimeSpan.FromMinutes(configuration.GetValue("Idempotency:StaleAfterMinutes", 2));
        }

        public async Task<Idempotency?> GetStoredResponseAsync(string requestPath, Guid key, CancellationToken cancellationToken = default)
        {
            var keyString = key.ToString();

            return await _db.Idempotencies.AsNoTracking()
                            .FirstOrDefaultAsync(i =>
                            i.RequestPath == requestPath
                            && i.Key == keyString
                            && i.Status != IdempotencyStatus.InProgress, cancellationToken);
        }

        public async Task<Idempotency?> WaitForStoredResponseAsync(string requestPath, Guid key, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                var stored = await GetStoredResponseAsync(requestPath, key, cancellationToken);

                if (stored != null)
                {
                    return stored;
                }

                await Task.Delay(pollInterval, cancellationToken);
            }

            return null;
        }

        public async Task<bool> CreateInProgressAsync(string requestPath, Guid key, CancellationToken cancellationToken = default)
        {
            var keyString = key.ToString();

            var newRecord = new Idempotency
            {
                RequestPath = requestPath,
                Key = keyString,
                Status = IdempotencyStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _db.Idempotencies.Add(newRecord);
                await _db.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                _db.Entry(newRecord).State = EntityState.Detached;

                var cutoff = DateTime.UtcNow - _staleAfter;


                var taken = await _db.Idempotencies
                    .Where(i => i.Key == keyString
                             && i.RequestPath == requestPath
                             && i.Status == IdempotencyStatus.InProgress
                             && i.CreatedAt < cutoff)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAt, DateTime.UtcNow), cancellationToken);

                return taken == 1;
            }
        }

        public async Task MarkCompletedAsync(Guid key, int responseCode, string responseBody, CancellationToken cancellationToken = default)
        {
            var keyString = key.ToString();
            var record = await _db.Idempotencies
                .FirstOrDefaultAsync(i => i.Key == keyString, cancellationToken);

            if (record == null)
            {
                return;
            }

            record.ResponseCode = responseCode;
            record.ResponseBody = responseBody;
            record.Status = IdempotencyStatus.Completed;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFailedAsync(Guid key, int responseCode, string responseBody, CancellationToken cancellationToken = default)
        {
            var keyString = key.ToString();
            var record = await _db.Idempotencies.FirstOrDefaultAsync(i => i.Key == keyString, cancellationToken);

            if (record == null) return;

            record.ResponseCode = responseCode;
            record.ResponseBody = responseBody;
            record.Status = IdempotencyStatus.Failed;

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
