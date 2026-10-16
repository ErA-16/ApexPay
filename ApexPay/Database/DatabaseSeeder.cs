using ApexPay.Service;
using ApexPay.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApexPay.Database
{
    public static class DatabaseSeeder
    {
        private const string MerchantEmail = "merchant@apexpay.test";
        private const int MaxAccountNumberAttempts = 5;
        private static readonly Guid MerchantAccountId = Guid.Parse("5d77e75e-0f38-4868-924d-54b3b912a11c");
        private static readonly Guid MerchantWalletId = Guid.Parse("58637fb1-3b56-4f11-a4e4-a35668fa0fb0");

        public static async Task SeedAsync(ApexPayDbContext db)
        {
            if (await db.Wallets.AnyAsync(w => w.Id == MerchantWalletId))
            {
                return;
            }

            if (await db.Accounts.FindAsync(MerchantAccountId) == null)
            {
                db.Accounts.Add(new Account
                {
                    Id = MerchantAccountId,
                    FullName = "ApexPay Merchant",
                    Email = MerchantEmail
                });

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_account_email"
                })
                {
                    db.ChangeTracker.Clear();
                }
            }

            for (int attempt = 1; attempt <= MaxAccountNumberAttempts; attempt++)
            {
                db.Wallets.Add(new Wallet
                {
                    Id = MerchantWalletId,
                    AccountId = MerchantAccountId,
                    AccountNumber = WalletService.GenerateAccountNumber(),
                    Name = "Merchant Settlement",
                    Role = WalletRole.Merchant
                });

                try
                {
                    await db.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "uq_wallet_account_number"
                })
                {
                    db.ChangeTracker.Clear();
                }
            }
        }
    }
}
