using ApexPay.Models;
using Microsoft.EntityFrameworkCore;

namespace ApexPay.Database
{
    public class ApexPayDbContext : DbContext
    {
        public ApexPayDbContext(DbContextOptions<ApexPayDbContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<LedgerEntry> LedgerEntries { get; set; }
        public DbSet<Idempotency> Idempotencies { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Account>(entity =>
            {
                entity.Property(a => a.FullName).HasMaxLength(100).IsRequired();
                entity.Property(a => a.Email).HasMaxLength(255).IsRequired();

                entity.HasIndex(a => a.Email)
                    .HasDatabaseName("uq_account_email")
                    .IsUnique();
            });

            modelBuilder.Entity<Wallet>(entity =>
            {
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("chk_positive_balance", "balance >= 0");
                });

                entity.HasIndex(w => new { w.AccountId, w.Name })
                    .HasDatabaseName("uq_wallet_account_id_name")
                    .HasFilter("status <> 'CLOSED'")
                    .IsUnique();

                entity.HasIndex(w => new { w.AccountId, w.CreatedAt })
                    .HasDatabaseName("ix_wallet_account_id_created_at");

                entity.Property(w => w.Currency).HasMaxLength(3).HasDefaultValue("NGN");

                entity.Property(w => w.Balance).HasPrecision(18, 2);

                entity.Property(w => w.Name).HasMaxLength(50).IsRequired();

                entity.Property(w => w.Status)
                    .HasMaxLength(20)
                    .HasConversion(
                        v => v.ToString().ToUpperInvariant(),
                        v => Enum.Parse<WalletStatus>(v, true))
                    .HasDefaultValue(WalletStatus.Active);

                entity.Property(w => w.Role)
                    .HasMaxLength(20)
                    .HasConversion(
                        v => v.ToString().ToUpperInvariant(),
                        v => Enum.Parse<WalletRole>(v, true))
                    .HasDefaultValue(WalletRole.User);

                entity.HasOne(w => w.UserAccount)
                    .WithMany(a => a.Wallets)
                    .HasForeignKey(w => w.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(w => w.AccountNumber).HasMaxLength(10).IsRequired();

                entity.HasIndex(w => w.AccountNumber)
                    .HasDatabaseName("uq_wallet_account_number")
                    .IsUnique();
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("chk_positive_amount", "amount > 0");
                });

                entity.Property(t => t.Reference).HasMaxLength(64).IsRequired();

                entity.HasIndex(t => t.Reference)
                    .HasDatabaseName("uq_transaction_reference")
                    .IsUnique();

                entity.Property(t => t.Amount).HasPrecision(18, 2);

                entity.Property(t => t.Type).HasMaxLength(20).HasConversion<string>();
                entity.Property(t => t.Status).HasMaxLength(20).HasConversion<string>();

                entity.Property(t => t.Narration).HasMaxLength(200);

                entity.Property(t => t.OrderId).HasMaxLength(64);

                entity.HasOne(t => t.SourceWallet)
                    .WithMany()
                    .HasForeignKey(t => t.SourceWalletId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.DestinationWallet)
                    .WithMany()
                    .HasForeignKey(t => t.DestinationWalletId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Transaction>()
                    .WithMany()
                    .HasForeignKey(t => t.ParentTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.ParentTransactionId)
                    .HasDatabaseName("ix_transaction_parent");
            });

            modelBuilder.Entity<LedgerEntry>(entity =>
            {
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("chk_ledger_positive", "amount > 0");
                });

                entity.Property(l => l.Amount).HasPrecision(18, 2);
                entity.Property(l => l.EntryType).HasMaxLength(10).HasConversion<string>();

                entity.HasOne(l => l.Transaction)
                    .WithMany()
                    .HasForeignKey(l => l.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Wallet)
                    .WithMany()
                    .HasForeignKey(l => l.WalletId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => new { l.WalletId, l.CreatedAt })
                    .HasDatabaseName("ix_ledger_wallet_created");
            });

            modelBuilder.Entity<Idempotency>(entity =>
            {
                entity.ToTable("idempotency_records");
                entity.HasKey(i => i.Key);
                entity.Property(i => i.Key).HasMaxLength(128);
                entity.Property(i => i.RequestPath).IsRequired().HasMaxLength(255);
                entity.Property(i => i.ResponseBody).HasColumnType("text");
                entity.Property(i => i.Status).HasMaxLength(20).HasConversion<string>();
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("password_reset_tokens");
                entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

                entity.HasIndex(t => t.TokenHash)
                    .HasDatabaseName("uq_password_reset_token_hash")
                    .IsUnique();

                entity.HasOne(t => t.Account)
                    .WithMany()
                    .HasForeignKey(t => t.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}