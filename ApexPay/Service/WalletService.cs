using ApexPay.Database;
using ApexPay.Dto;
using ApexPay.Models;
using ApexPay.Service.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApexPay.Service
{
    public class WalletService : IWalletService
    {
        private const int MaxAccountNumberAttempts = 5;
        private const int MaxReferenceAttempts = 5;
        private const string RefundWalletName = "Refund";
        private readonly ApexPayDbContext _db;
        private readonly ILogger<WalletService> _logger;
        private readonly IdempotencyService _idempotencyService;

        public WalletService(ApexPayDbContext db, ILogger<WalletService> logger, IdempotencyService idempotencyService)
        {
            _db = db;
            _logger = logger;
            _idempotencyService = idempotencyService;
        }

        public static string GenerateAccountNumber()
        {
            var result = new StringBuilder(10);
            for (int i = 0; i < 10; i++)
            {
                result.Append(RandomNumberGenerator.GetInt32(0, 10));
            }
            return result.ToString();
        }

        private static string GenerateRandomAlphanumeric(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return RandomNumberGenerator.GetString(chars, length);
        }

        private static string CreateTransactionReference(string prefix = "TXN-FUND", int randomSuffixLength = 6)
        {
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string randomPart = GenerateRandomAlphanumeric(randomSuffixLength);
            return $"{prefix}-{datePart}-{randomPart}";
        }

        private async Task<int> DebitWalletAsync(Guid walletId, Guid userId, decimal amount, CancellationToken cancellationToken)
        {
            return await _db.Wallets
                .Where(w => w.Id == walletId
                         && w.AccountId == userId
                         && w.Status == WalletStatus.Active
                         && w.Balance >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - amount), cancellationToken);
        }

        private async Task<int> DebitWalletAsync(Guid walletId, decimal amount, CancellationToken cancellationToken)
        {
            return await _db.Wallets
                .Where(w => w.Id == walletId
                         && w.Status == WalletStatus.Active
                         && w.Balance >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - amount), cancellationToken);
        }

        private async Task<int> CreditWalletAsync(Guid walletId, decimal amount, CancellationToken cancellationToken)
        {
            return await _db.Wallets
                .Where(w => w.Id == walletId && w.Status == WalletStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance + amount), cancellationToken);
        }

        private async Task<int> CreditMerchantWalletAsync(Guid walletId, decimal amount, CancellationToken cancellationToken)
        {
            return await _db.Wallets
                .Where(w => w.Id == walletId
                         && w.Status == WalletStatus.Active
                         && w.Role == WalletRole.Merchant)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance + amount), cancellationToken);
        }

        public async Task<WalletResponse> CreateWalletAsync(Guid userId, CreateWalletRequest request, CancellationToken cancellationToken = default)
        {
            var name = request.Name.Trim();

            var accountExists = await _db.Accounts.AnyAsync(a => a.Id == userId, cancellationToken);

            if (!accountExists)
            {
                throw new AccountNotFoundException();
            }

            _logger.LogInformation("Creating wallet for account {accountId}", userId);

            var nameTaken = await _db.Wallets.AnyAsync(w => w.AccountId == userId
                                             && w.Name == name
                                             && w.Status != WalletStatus.Closed, cancellationToken);

            if (nameTaken)
            {
                throw new WalletNameConflictException();
            }

            for (int attempt = 1; attempt <= MaxAccountNumberAttempts; attempt++)
            {
                var wallet = new Wallet
                {
                    AccountNumber = GenerateAccountNumber(),
                    Name = name,
                    AccountId = userId
                };

                _db.Wallets.Add(wallet);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    return new WalletResponse
                    {
                        Id = wallet.Id,
                        AccountNumber = wallet.AccountNumber,
                        Name = wallet.Name,
                        Currency = wallet.Currency,
                        Balance = wallet.Balance,
                        Status = wallet.Status.ToString().ToUpperInvariant(),
                        CreatedAt = wallet.CreatedAt
                    };
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
                {
                    _db.Entry(wallet).State = EntityState.Detached;

                    if (pg.ConstraintName == "uq_wallet_account_id_name")
                    {
                        throw new WalletNameConflictException();
                    }

                    if (pg.ConstraintName != "uq_wallet_account_number")
                    {
                        throw;
                    }

                    _logger.LogWarning("Account number collision on attempt {attempt}", attempt);
                }
            }

            _logger.LogError("Could not generate a unique account number after {max} attempts", MaxAccountNumberAttempts);
            throw new WalletCreationFailedException();
        }

        public async Task<WalletListResponse> GetAllWalletsAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default)
        {
            var query = _db.Wallets.AsNoTracking()
                .Where(w => w.AccountId == userId && w.Status != WalletStatus.Closed);

            var totalWallets = await query.CountAsync(cancellationToken);

            var wallets = await query
                .OrderBy(w => w.CreatedAt)
                .ThenBy(w => w.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(w => new
                {
                    w.Id,
                    w.AccountNumber,
                    w.Name,
                    w.Currency,
                    w.Balance,
                    w.Status,
                    w.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new WalletListResponse
            {
                UserId = userId,
                Page = page,
                Limit = limit,
                TotalWallets = totalWallets,
                TotalPages = (int)Math.Ceiling(totalWallets / (double)limit),
                Wallets = wallets.Select(w => new WalletResponse
                {
                    Id = w.Id,
                    AccountNumber = w.AccountNumber,
                    Name = w.Name,
                    Currency = w.Currency,
                    Balance = w.Balance,
                    Status = w.Status.ToString().ToUpperInvariant(),
                    CreatedAt = w.CreatedAt
                }).ToList()
            };
        }

        public async Task<WalletResponse> GetWalletByIdAsync(Guid userId, Guid walletId, CancellationToken cancellationToken = default)
        {
            var wallet = await _db.Wallets
                .AsNoTracking()
                .Where(w => w.Id == walletId && w.AccountId == userId && w.Status != WalletStatus.Closed)
                .Select(w => new
                {
                    w.Id,
                    w.AccountNumber,
                    w.Name,
                    w.Currency,
                    w.Balance,
                    w.Status,
                    w.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                throw new WalletNotOwnedException();
            }

            return new WalletResponse
            {
                Id = wallet.Id,
                AccountNumber = wallet.AccountNumber,
                Name = wallet.Name,
                Currency = wallet.Currency,
                Balance = wallet.Balance,
                Status = wallet.Status.ToString().ToUpperInvariant(),
                CreatedAt = wallet.CreatedAt
            };
        }

        public async Task<WalletHistoryResponse> GetWalletHistoryAsync(Guid userId, Guid walletId, int page, int limit, CancellationToken cancellationToken = default)
        {
            var walletStoredBalance = await _db.Wallets.AsNoTracking()
                .Where(w => w.Id == walletId && w.AccountId == userId && w.Status != WalletStatus.Closed)
                .Select(w => (decimal?)w.Balance)
                .FirstOrDefaultAsync(cancellationToken);

            if (walletStoredBalance == null)
            {
                _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                throw new WalletNotOwnedException();
            }

            var credits = await _db.LedgerEntries.AsNoTracking()
                .Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Credit)
                .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

            var debits = await _db.LedgerEntries.AsNoTracking()
                .Where(e => e.WalletId == walletId && e.EntryType == LedgerEntryType.Debit)
                .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

            var totalEntries = await _db.LedgerEntries.AsNoTracking()
                .CountAsync(e => e.WalletId == walletId, cancellationToken);

            var ledgerCalculatedBalance = credits - debits;

            var entries = await _db.LedgerEntries.AsNoTracking()
                .Where(e => e.WalletId == walletId)
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(e => new
                {
                    Reference = e.Transaction.Reference,
                    e.Transaction.Type,
                    e.EntryType,
                    e.Amount,
                    Timestamp = e.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new WalletHistoryResponse
            {
                WalletId = walletId,
                Page = page,
                Limit = limit,
                TotalEntries = totalEntries,
                TotalPages = (int)Math.Ceiling(totalEntries / (double)limit),
                LedgerCalculatedBalance = ledgerCalculatedBalance,
                WalletStoredBalance = walletStoredBalance.Value,
                IsConsistent = ledgerCalculatedBalance == walletStoredBalance.Value,
                Entries = entries.Select(e => new WalletHistoryEntry
                {
                    Reference = e.Reference,
                    Type = e.Type.ToString().ToUpperInvariant(),
                    EntryType = e.EntryType.ToString().ToUpperInvariant(),
                    Amount = e.Amount,
                    Timestamp = e.Timestamp
                }).ToList()
            };
        }

        public async Task<AccountHistoryResponse> GetAccountHistoryAsync(Guid userId, Guid? walletId, int page, int limit, CancellationToken cancellationToken = default)
        {
            var walletRows = await _db.Wallets.AsNoTracking()
                .Where(w => w.AccountId == userId)
                .OrderBy(w => w.CreatedAt)
                .ThenBy(w => w.Id)
                .Select(w => new { w.Id, w.Name, w.AccountNumber, w.Status })
                .ToListAsync(cancellationToken);

            if (walletId.HasValue && walletRows.All(w => w.Id != walletId.Value))
            {
                _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                throw new WalletNotOwnedException();
            }

            var query = _db.LedgerEntries.AsNoTracking()
                .Where(e => e.Wallet.AccountId == userId);

            if (walletId.HasValue)
            {
                query = query.Where(e => e.WalletId == walletId.Value);
            }

            var totalEntries = await query.CountAsync(cancellationToken);

            var rows = await query
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(e => new
                {
                    e.WalletId,
                    e.Wallet.Name,
                    e.Wallet.AccountNumber,
                    e.Wallet.Status,
                    Reference = e.Transaction.Reference,
                    e.Transaction.Type,
                    e.EntryType,
                    e.Amount,
                    Timestamp = e.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new AccountHistoryResponse
            {
                WalletId = walletId,
                Page = page,
                Limit = limit,
                TotalEntries = totalEntries,
                TotalPages = (int)Math.Ceiling(totalEntries / (double)limit),
                Wallets = walletRows.Select(w => new AccountHistoryWallet
                {
                    Id = w.Id,
                    Name = w.Name,
                    AccountNumber = w.AccountNumber,
                    Status = w.Status.ToString().ToUpperInvariant()
                }).ToList(),
                Entries = rows.Select(e => new AccountHistoryEntry
                {
                    WalletId = e.WalletId,
                    WalletName = e.Name,
                    AccountNumber = e.AccountNumber,
                    WalletStatus = e.Status.ToString().ToUpperInvariant(),
                    Reference = e.Reference,
                    Type = e.Type.ToString().ToUpperInvariant(),
                    EntryType = e.EntryType.ToString().ToUpperInvariant(),
                    Amount = e.Amount,
                    Timestamp = e.Timestamp
                }).ToList()
            };
        }

        public async Task<WalletResponse> GetWalletByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            var wallet = await _db.Wallets
                .AsNoTracking()
                .Where(w => w.AccountNumber == accountNumber && w.Status == WalletStatus.Active)
                .Select(w => new
                {
                    w.Id,
                    w.AccountNumber,
                    w.Name,
                    w.Currency,
                    w.Status,
                    OwnerFullName = w.UserAccount.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                throw new WalletNotFoundException("No active wallet with that account number.");
            }

            return new WalletResponse
            {
                Id = wallet.Id,
                AccountNumber = wallet.AccountNumber,
                Name = wallet.Name,
                OwnerName = wallet.OwnerFullName,
                Currency = wallet.Currency,
                Status = wallet.Status.ToString().ToUpperInvariant()
            };
        }

        public async Task<WalletResponse> GetMerchantWalletAsync(CancellationToken cancellationToken = default)
        {
            var wallet = await _db.Wallets
                .AsNoTracking()
                .Where(w => w.Role == WalletRole.Merchant && w.Status == WalletStatus.Active)
                .Select(w => new
                {
                    w.Id,
                    w.AccountNumber,
                    w.Name,
                    w.Currency,
                    w.Status,
                    OwnerFullName = w.UserAccount.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                throw new WalletNotFoundException("Merchant wallet not found.");
            }

            return new WalletResponse
            {
                Id = wallet.Id,
                AccountNumber = wallet.AccountNumber,
                Name = wallet.Name,
                OwnerName = wallet.OwnerFullName,
                Currency = wallet.Currency,
                Status = wallet.Status.ToString().ToUpperInvariant()
            };
        }

        public async Task CloseWalletAsync(Guid userId, Guid walletId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Closing wallet {WalletId} for account {UserId}", walletId, userId);

            var walletRow = await _db.Wallets.Where(w => w.Id == walletId
                                                      && w.AccountId == userId
                                                      && w.Status == WalletStatus.Active
                                                      && w.Balance == 0)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WalletStatus.Closed), cancellationToken);

            if (walletRow == 1)
            {
                return;
            }

            var wallet = await _db.Wallets.AsNoTracking().Where(w => w.Id == walletId && w.AccountId == userId)
                            .Select(w => new { w.Status, w.Balance })
                            .FirstOrDefaultAsync(cancellationToken);
            if (wallet == null)
            {
                _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                throw new WalletNotFoundException("Wallet not found");
            }

            if (wallet.Status == WalletStatus.Closed)
            {
                return;
            }

            throw new WalletConflictException("Move or withdraw the remaining balance before closing this wallet.");
        }

        public async Task RenameWalletAsync(Guid userId, Guid walletId, RenameWalletRequest request, CancellationToken cancellationToken = default)
        {
            var name = request.Name.Trim();

            var wallet = await _db.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletId
                                       && w.AccountId == userId
                                       && w.Status != WalletStatus.Closed, cancellationToken);

            if (wallet == null)
            {
                _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                throw new WalletNotFoundException("Wallet not found");
            }

            if (wallet.Name == name)
            {
                return;
            }

            var nameTaken = await _db.Wallets.AnyAsync(w => w.AccountId == userId
                                                         && w.Name == name
                                                         && w.Status != WalletStatus.Closed
                                                         && w.Id != walletId, cancellationToken);
            if (nameTaken)
            {
                throw new WalletNameConflictException();
            }

            wallet.Name = name;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                                               && pg.ConstraintName == "uq_wallet_account_id_name")
            {
                throw new WalletNameConflictException();
            }
        }

        public async Task<FundWalletResponse> FundWalletAsync(Guid userId, FundWalletRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            var narration = string.IsNullOrWhiteSpace(request.Narration) ? null : request.Narration.Trim();

            for (int attempt = 1; attempt <= MaxReferenceAttempts; attempt++)
            {
                await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var rowsChanged = await _db.Wallets
                    .Where(w => w.Id == request.WalletId && w.AccountId == userId && w.Status == WalletStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance + request.Amount), cancellationToken);

                if (rowsChanged == 0)
                {
                    var existing = await _db.Wallets
                        .AsNoTracking()
                        .Where(w => w.Id == request.WalletId && w.AccountId == userId)
                        .Select(w => new { w.Status })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existing == null)
                    {
                        throw new WalletNotFoundException("Wallet does not exist");
                    }

                    throw new WalletAlreadyClosedException("This wallet is closed.");
                }

                var transaction = new Transaction
                {
                    Reference = CreateTransactionReference(),
                    Type = TransactionType.Funding,
                    Status = TransactionStatus.Successful,
                    Amount = request.Amount,
                    DestinationWalletId = request.WalletId,
                    Narration = narration
                };

                var ledgerEntry = new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = request.WalletId,
                    EntryType = LedgerEntryType.Credit,
                    Amount = request.Amount
                };

                _db.Transactions.Add(transaction);
                _db.LedgerEntries.Add(ledgerEntry);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    var wallet = await _db.Wallets
                        .AsNoTracking()
                        .Where(w => w.Id == request.WalletId)
                        .Select(w => new { w.Balance, w.AccountNumber, w.UserAccount.FullName })
                        .FirstAsync(cancellationToken);


                    var response = new FundWalletResponse
                    {
                        TransactionId = transaction.Id,
                        Reference = transaction.Reference,
                        WalletId = request.WalletId,
                        AccountNumber = wallet.AccountNumber,
                        AccountName = wallet.FullName,
                        Amount = request.Amount,
                        NewBalance = wallet.Balance,
                        State = transaction.Status.ToString(),
                        Narration = narration
                    };

                    await _idempotencyService.MarkCompletedAsync(idempotencyKey, 200,
                        JsonSerializer.Serialize(response, JsonSerializerOptions.Web), cancellationToken);

                    await dbTransaction.CommitAsync(cancellationToken);
                    return response;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_transaction_reference"
                })
                {
                    _db.ChangeTracker.Clear();
                    _logger.LogWarning("Transaction reference collision on attempt {attempt}", attempt);
                }
            }

            _logger.LogError("Could not generate a unique transaction reference after {max} attempts", MaxReferenceAttempts);
            throw new WalletProcessingFailedException("Could not process funding. Please retry with a new Idempotency-Key.");
        }

        public async Task<WalletTransferResponse> TransferAsync(Guid userId, Guid walletId, WalletTransferRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            var destinationWalletId = request.DestinationWalletId!.Value;

            var narration = string.IsNullOrWhiteSpace(request.Narration) ? null : request.Narration.Trim();

            for (int attempt = 1; attempt <= MaxReferenceAttempts; attempt++)
            {
                await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var debitFirst = walletId.CompareTo(destinationWalletId) < 0;

                int debited;
                int credited;

                if (debitFirst)
                {
                    debited = await DebitWalletAsync(walletId, userId, request.Amount, cancellationToken);
                    credited = await CreditWalletAsync(destinationWalletId, request.Amount, cancellationToken);
                }
                else
                {
                    credited = await CreditWalletAsync(destinationWalletId, request.Amount, cancellationToken);
                    debited = await DebitWalletAsync(walletId, userId, request.Amount, cancellationToken);
                }

                if (debited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);

                    var source = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == walletId && w.AccountId == userId)
                        .Select(w => new { w.Status, w.Balance })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (source == null)
                    {
                        _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", walletId, userId);
                        throw new WalletNotFoundException("Wallet not found");
                    }

                    if (source.Status != WalletStatus.Active)
                    {
                        throw new WalletAlreadyClosedException("This wallet is closed.");
                    }

                    _logger.LogWarning("Transfer failed, insufficient balance in wallet {WalletId}", walletId);
                    throw new InsufficientBalanceException("Insufficient balance.");
                }

                if (credited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);

                    var destination = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == destinationWalletId)
                        .Select(w => new { w.Status })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (destination == null)
                    {
                        throw new WalletNotFoundException("Destination wallet not found");
                    }

                    throw new WalletAlreadyClosedException("Destination wallet is closed.");
                }

                var transaction = new Transaction
                {
                    Reference = CreateTransactionReference("TXN-TRF"),
                    Type = TransactionType.Transfer,
                    Status = TransactionStatus.Successful,
                    Amount = request.Amount,
                    SourceWalletId = walletId,
                    DestinationWalletId = destinationWalletId,
                    Narration = narration
                };

                _db.Transactions.Add(transaction);

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = walletId,
                    EntryType = LedgerEntryType.Debit,
                    Amount = request.Amount
                });

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = destinationWalletId,
                    EntryType = LedgerEntryType.Credit,
                    Amount = request.Amount
                });

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    var sourceWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == walletId)
                        .Select(w => new { w.Balance, w.AccountNumber, w.UserAccount.FullName })
                        .FirstAsync(cancellationToken);

                    var destinationWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == destinationWalletId)
                        .Select(w => new { w.AccountNumber, w.UserAccount.FullName })
                        .FirstAsync(cancellationToken);

                    var response = new WalletTransferResponse
                    {
                        TransactionId = transaction.Id,
                        Reference = transaction.Reference,
                        SourceWalletId = walletId,
                        SourceAccountNumber = sourceWallet.AccountNumber,
                        SourceAccountName = sourceWallet.FullName,
                        DestinationWalletId = destinationWalletId,
                        DestinationAccountNumber = destinationWallet.AccountNumber,
                        DestinationAccountName = destinationWallet.FullName,
                        Amount = request.Amount,
                        NewBalance = sourceWallet.Balance,
                        Narration = narration,
                        State = transaction.Status.ToString()
                    };

                    await _idempotencyService.MarkCompletedAsync(idempotencyKey, 200,
                        JsonSerializer.Serialize(response, JsonSerializerOptions.Web), cancellationToken);

                    await dbTransaction.CommitAsync(cancellationToken);
                    return response;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_transaction_reference"
                })
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    _logger.LogWarning("Transaction reference collision on attempt {attempt}", attempt);
                }
            }

            _logger.LogError("Could not generate a unique transaction reference after {max} attempts", MaxReferenceAttempts);
            throw new WalletProcessingFailedException("Could not process transfer. Please retry with a new Idempotency-Key.");
        }

        public async Task<WalletCheckoutResponse> CheckoutAsync(Guid userId, WalletCheckoutRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            var userWalletId = request.UserWalletId!.Value;
            var merchantWalletId = request.MerchantWalletId!.Value;
            var orderId = request.OrderId.Trim();

            for (int attempt = 1; attempt <= MaxReferenceAttempts; attempt++)
            {
                await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var debitFirst = userWalletId.CompareTo(merchantWalletId) < 0;

                int debited;
                int credited;

                if (debitFirst)
                {
                    debited = await DebitWalletAsync(userWalletId, userId, request.Amount, cancellationToken);
                    credited = await CreditMerchantWalletAsync(merchantWalletId, request.Amount, cancellationToken);
                }
                else
                {
                    credited = await CreditMerchantWalletAsync(merchantWalletId, request.Amount, cancellationToken);
                    debited = await DebitWalletAsync(userWalletId, userId, request.Amount, cancellationToken);
                }

                if (debited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);

                    var source = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == userWalletId && w.AccountId == userId)
                        .Select(w => new { w.Status, w.Balance })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (source == null)
                    {
                        _logger.LogWarning("Wallet {WalletId} not found for account {UserId}", userWalletId, userId);
                        throw new WalletNotFoundException("Wallet not found");
                    }

                    if (source.Status != WalletStatus.Active)
                    {
                        throw new WalletAlreadyClosedException("This wallet is closed.");
                    }

                    _logger.LogWarning("Checkout failed, insufficient balance in wallet {WalletId}", userWalletId);
                    throw new InsufficientBalanceException("Insufficient balance.");
                }

                if (credited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);

                    var merchant = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == merchantWalletId)
                        .Select(w => new { w.Status, w.Role })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (merchant == null)
                    {
                        throw new WalletNotFoundException("Merchant wallet not found");
                    }

                    if (merchant.Role != WalletRole.Merchant)
                    {
                        throw new WalletConflictException("Destination wallet is not a merchant wallet.");
                    }

                    throw new WalletAlreadyClosedException("Merchant wallet is closed.");
                }

                var transaction = new Transaction
                {
                    Reference = CreateTransactionReference("TXN-CHK"),
                    Type = TransactionType.Payment,
                    Status = TransactionStatus.Successful,
                    Amount = request.Amount,
                    SourceWalletId = userWalletId,
                    DestinationWalletId = merchantWalletId,
                    OrderId = orderId
                };

                _db.Transactions.Add(transaction);

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = userWalletId,
                    EntryType = LedgerEntryType.Debit,
                    Amount = request.Amount
                });

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = merchantWalletId,
                    EntryType = LedgerEntryType.Credit,
                    Amount = request.Amount
                });

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    var userWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == userWalletId)
                        .Select(w => new { w.Balance, w.AccountNumber, w.UserAccount.FullName })
                        .FirstAsync(cancellationToken);

                    var merchantWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == merchantWalletId)
                        .Select(w => new { w.AccountNumber, w.UserAccount.FullName })
                        .FirstAsync(cancellationToken);

                    var response = new WalletCheckoutResponse
                    {
                        TransactionId = transaction.Id,
                        Reference = transaction.Reference,
                        OrderId = orderId,
                        UserWalletId = userWalletId,
                        UserAccountNumber = userWallet.AccountNumber,
                        UserAccountName = userWallet.FullName,
                        MerchantWalletId = merchantWalletId,
                        MerchantAccountNumber = merchantWallet.AccountNumber,
                        MerchantAccountName = merchantWallet.FullName,
                        Amount = request.Amount,
                        NewBalance = userWallet.Balance,
                        State = transaction.Status.ToString()
                    };

                    await _idempotencyService.MarkCompletedAsync(idempotencyKey, 200,
                        JsonSerializer.Serialize(response, JsonSerializerOptions.Web), cancellationToken);

                    await dbTransaction.CommitAsync(cancellationToken);
                    return response;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_transaction_reference"
                })
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    _logger.LogWarning("Transaction reference collision on attempt {attempt}", attempt);
                }
            }

            _logger.LogError("Could not generate a unique transaction reference after {max} attempts", MaxReferenceAttempts);
            throw new WalletProcessingFailedException("Could not process checkout. Please retry with a new Idempotency-Key.");
        }

        public async Task<RefundResponse> RefundAsync(Guid userId, RefundRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            var original = await _db.Transactions.AsNoTracking()
                .Where(t => t.Reference == request.OriginalTransactionReference)
                .Select(t => new
                {
                    t.Id,
                    t.Reference,
                    t.Type,
                    t.Amount,
                    t.SourceWalletId,
                    t.DestinationWalletId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (original == null)
            {
                throw new WalletNotFoundException("Original transaction not found");
            }

            if (original.Type != TransactionType.Payment || original.SourceWalletId == null)
            {
                throw new WalletBadRequestException("Transaction is not refundable.");
            }

            var destinationRole = await _db.Wallets.AsNoTracking()
                .Where(w => w.Id == original.DestinationWalletId)
                .Select(w => (WalletRole?)w.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (destinationRole != WalletRole.Merchant)
            {
                throw new WalletBadRequestException("Transaction is not refundable.");
            }

            var merchantWalletId = original.DestinationWalletId;
            var sourceWalletId = original.SourceWalletId.Value;

            var sourceWalletOwner = await _db.Wallets.AsNoTracking()
                .Where(w => w.Id == sourceWalletId)
                .Select(w => (Guid?)w.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceWalletOwner == null || sourceWalletOwner.Value != userId)
            {
                throw new WalletNotFoundException("Original transaction not found");
            }

            for (int attempt = 1; attempt <= MaxReferenceAttempts; attempt++)
            {
                await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                await _db.Database.ExecuteSqlRawAsync(
                    "SELECT id FROM transactions WHERE id = {0} FOR UPDATE", new object[] { original.Id }, cancellationToken);

                var alreadyRefunded = await _db.Transactions
                    .Where(t => t.ParentTransactionId == original.Id)
                    .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

                var remaining = original.Amount - alreadyRefunded;

                if (remaining <= 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    throw new WalletConflictException("This transaction has already been refunded.");
                }

                var refundAmount = remaining;

                var sourceWallet = await _db.Wallets.AsNoTracking()
                    .Where(w => w.Id == sourceWalletId)
                    .Select(w => new { w.Status, w.AccountId, w.Currency })
                    .FirstAsync(cancellationToken);

                Wallet? newRefundWallet = null;
                Guid creditedWalletId;

                if (sourceWallet.Status == WalletStatus.Active)
                {
                    creditedWalletId = sourceWalletId;
                }
                else
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT id FROM accounts WHERE id = {0} FOR UPDATE", new object[] { sourceWallet.AccountId }, cancellationToken);

                    var existingRefundWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.AccountId == sourceWallet.AccountId
                                 && w.Name == RefundWalletName
                                 && w.Status != WalletStatus.Closed)
                        .Select(w => (Guid?)w.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingRefundWallet != null)
                    {
                        creditedWalletId = existingRefundWallet.Value;
                    }
                    else
                    {
                        newRefundWallet = new Wallet
                        {
                            AccountId = sourceWallet.AccountId,
                            AccountNumber = GenerateAccountNumber(),
                            Name = RefundWalletName,
                            Currency = sourceWallet.Currency,
                            Balance = refundAmount
                        };

                        creditedWalletId = newRefundWallet.Id;
                    }
                }

                int debited;
                int credited;

                if (newRefundWallet != null)
                {
                    debited = await DebitWalletAsync(merchantWalletId, refundAmount, cancellationToken);
                    credited = 1;
                }
                else
                {
                    var debitFirst = merchantWalletId.CompareTo(creditedWalletId) < 0;

                    if (debitFirst)
                    {
                        debited = await DebitWalletAsync(merchantWalletId, refundAmount, cancellationToken);
                        credited = await CreditWalletAsync(creditedWalletId, refundAmount, cancellationToken);
                    }
                    else
                    {
                        credited = await CreditWalletAsync(creditedWalletId, refundAmount, cancellationToken);
                        debited = await DebitWalletAsync(merchantWalletId, refundAmount, cancellationToken);
                    }
                }

                if (debited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();

                    var merchantWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == merchantWalletId)
                        .Select(w => new { w.Status })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (merchantWallet == null)
                    {
                        throw new WalletNotFoundException("Merchant wallet not found");
                    }

                    if (merchantWallet.Status != WalletStatus.Active)
                    {
                        throw new WalletAlreadyClosedException("Merchant wallet is closed.");
                    }

                    _logger.LogWarning("Refund failed, insufficient balance in merchant wallet {WalletId}", merchantWalletId);
                    throw new InsufficientBalanceException("Merchant wallet has insufficient balance for this refund.");
                }

                if (credited == 0)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();

                    var destination = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == creditedWalletId)
                        .Select(w => new { w.Status })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (destination == null)
                    {
                        throw new WalletNotFoundException("Wallet not found");
                    }

                    throw new WalletAlreadyClosedException("This wallet is closed.");
                }

                if (newRefundWallet != null)
                {
                    _db.Wallets.Add(newRefundWallet);
                }

                var transaction = new Transaction
                {
                    Reference = CreateTransactionReference("TXN-RFD"),
                    Type = TransactionType.Refund,
                    Status = TransactionStatus.Successful,
                    Amount = refundAmount,
                    SourceWalletId = merchantWalletId,
                    DestinationWalletId = creditedWalletId,
                    ParentTransactionId = original.Id,
                    Narration = reason
                };

                _db.Transactions.Add(transaction);

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = merchantWalletId,
                    EntryType = LedgerEntryType.Debit,
                    Amount = refundAmount
                });

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    TransactionId = transaction.Id,
                    WalletId = creditedWalletId,
                    EntryType = LedgerEntryType.Credit,
                    Amount = refundAmount
                });

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);

                    var creditedWallet = await _db.Wallets.AsNoTracking()
                        .Where(w => w.Id == creditedWalletId)
                        .Select(w => new { w.Balance, w.AccountNumber, w.Name })
                        .FirstAsync(cancellationToken);

                    var response = new RefundResponse
                    {
                        RefundTransactionId = transaction.Id,
                        Reference = transaction.Reference,
                        OriginalTransactionReference = original.Reference,
                        MerchantWalletId = merchantWalletId,
                        CreditedWalletId = creditedWalletId,
                        CreditedAccountNumber = creditedWallet.AccountNumber,
                        CreditedWalletName = creditedWallet.Name,
                        CreatedWallet = newRefundWallet != null,
                        Amount = refundAmount,
                        Reason = reason,
                        State = transaction.Status.ToString()
                    };

                    await _idempotencyService.MarkCompletedAsync(idempotencyKey, 200,
                        JsonSerializer.Serialize(response, JsonSerializerOptions.Web), cancellationToken);

                    await dbTransaction.CommitAsync(cancellationToken);
                    return response;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_transaction_reference"
                })
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    _logger.LogWarning("Transaction reference collision on refund attempt {attempt}", attempt);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_wallet_account_number"
                })
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    _logger.LogWarning("Refund wallet account number collision on attempt {attempt}", attempt);
                }
            }

            _logger.LogError("Could not process refund after {max} attempts", MaxReferenceAttempts);
            throw new WalletProcessingFailedException("Could not process refund. Please retry with a new Idempotency-Key.");
        }
    }
}
