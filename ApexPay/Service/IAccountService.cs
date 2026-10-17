using ApexPay.Dto;

namespace ApexPay.Service
{
    public interface IAccountService
    {
        Task<CreateAccountResponse> SignUpAsync(CreateAccountRequest request);
        Task<CreateAccountResponse> GetMeAsync(Guid userId);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task RequestPasswordResetAsync(ForgotPasswordRequest request);
        Task ResetPasswordAsync(ResetPasswordRequest request);
    }
}
