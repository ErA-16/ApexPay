using ApexPay.Database;
using ApexPay.Dto;
using ApexPay.Models;
using ApexPay.Service.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApexPay.Service
{
    public class AccountService : IAccountService
    {
        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.EnhancedHashPassword("apexpay-dummy-password", BCrypt.Net.HashType.SHA384);

        private readonly ApexPayDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountService> _logger;

        public AccountService(ApexPayDbContext db, IConfiguration configuration, ILogger<AccountService> logger)
        {
            _logger = logger;
            _db = db;
            _configuration = configuration;
        }

        public async Task<CreateAccountResponse> SignUpAsync(CreateAccountRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var firstName = request.FirstName.Trim();
            var lastName = request.LastName.Trim();

            _logger.LogInformation("New user is creating an account");
            var emailExists = await _db.Accounts.AnyAsync(e => e.Email == email);

            if (emailExists)
            {
                _logger.LogWarning("Account creation failed, user with email already exists");
                throw new EmailExistException();
            }

            var newAccount = new Account
            {
                FullName = firstName + " " + lastName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password, BCrypt.Net.HashType.SHA384)
            };

            _db.Accounts.Add(newAccount);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                if (pg.ConstraintName == "uq_account_email")
                {
                    _logger.LogWarning("Account creation failed (race), email already exists");
                    throw new EmailExistException();
                }

                throw;
            }

            var response = new CreateAccountResponse
            {
                Id = newAccount.Id,
                FullName = newAccount.FullName,
                Email = newAccount.Email
            };

            return response;
        }

        public async Task<CreateAccountResponse> GetMeAsync(Guid userId)
        {
            var account = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == userId);

            if (account == null)
            {
                throw new AccountNotFoundException();
            }

            return new CreateAccountResponse
            {
                Id = account.Id,
                FullName = account.FullName,
                Email = account.Email
            };
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var account = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email);

            var passwordHash = account?.PasswordHash ?? DummyPasswordHash;
            var verified = BCrypt.Net.BCrypt.EnhancedVerify(request.Password, passwordHash, BCrypt.Net.HashType.SHA384);

            if (!verified || account == null || account.PasswordHash == null)
            {
                _logger.LogWarning("Login failed, invalid credentials");
                throw new InvalidCredentialsException();
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(_configuration.GetValue("Jwt:AccessTokenMinutes", 60));
            var token = CreateAccessToken(account, expiresAt);

            return new LoginResponse
            {
                AccessToken = token,
                ExpiresAt = expiresAt
            };
        }

        private string CreateAccessToken(Account account, DateTime expiresAt)
        {
            var key = _configuration["Jwt:Key"]!;

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, account.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
