using ApexPay.Dto;

namespace ApexPay.Service
{
    public interface IWalletService
    {
        Task<WalletResponse> CreateWalletAsync(Guid userId, CreateWalletRequest request, CancellationToken cancellationToken = default);
        Task<WalletListResponse> GetAllWalletsAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default);
        Task<WalletResponse> GetWalletByIdAsync(Guid userId, Guid walletId, CancellationToken cancellationToken = default);
        Task<WalletHistoryResponse> GetWalletHistoryAsync(Guid userId, Guid walletId, int page, int limit, CancellationToken cancellationToken = default);
        Task CloseWalletAsync(Guid userId, Guid walletId, CancellationToken cancellationToken = default);
        Task RenameWalletAsync(Guid userId, Guid walletId, RenameWalletRequest request, CancellationToken cancellationToken = default);
        Task<FundWalletResponse> FundWalletAsync(Guid userId, FundWalletRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<WalletTransferResponse> TransferAsync(Guid userId, Guid walletId, WalletTransferRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<WalletCheckoutResponse> CheckoutAsync(Guid userId, WalletCheckoutRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<RefundResponse> RefundAsync(Guid userId, RefundRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<WalletResponse> GetWalletByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
        Task<WalletResponse> GetMerchantWalletAsync(CancellationToken cancellationToken = default);
    }
}
