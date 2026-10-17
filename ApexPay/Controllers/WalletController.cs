using ApexPay.Database;
using ApexPay.Dto;
using ApexPay.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApexPay.Service;
using ApexPay.Service.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ApexPay.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private readonly ApexPayDbContext _db;
        private readonly ILogger<WalletController> _logger;
        private readonly IdempotencyService _idempotencyService;
        private readonly IWalletService _walletService;

        private static IActionResult ReplayStoredResponse(Idempotency record)
        {
            var isError = record.ResponseCode >= 400;

            return new ContentResult
            {
                StatusCode = record.ResponseCode,
                Content = record.ResponseBody,
                ContentType = isError ? "text/plain" : "application/json"
            };
        }

        public WalletController(ApexPayDbContext db, ILogger<WalletController> logger, IdempotencyService idempotencyService, IWalletService walletService)
        {
            _db = db;
            _logger = logger;
            _idempotencyService = idempotencyService;
            _walletService = walletService;
        }

        private Guid GetUserId() =>
            Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        private async Task<IActionResult> ExecuteIdempotentAsync<T>(Guid idempotencyKey, Func<Task<T>> work)
        {
            var path = Request.Path.Value!;

            var stored = await _idempotencyService.GetStoredResponseAsync(path, idempotencyKey, HttpContext.RequestAborted);

            if (stored != null)
            {
                return ReplayStoredResponse(stored);
            }

            if (!await _idempotencyService.CreateInProgressAsync(path, idempotencyKey, HttpContext.RequestAborted))
            {
                var raced = await _idempotencyService.WaitForStoredResponseAsync(path, idempotencyKey,
                    TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(150), HttpContext.RequestAborted);

                if (raced != null)
                {
                    return ReplayStoredResponse(raced);
                }

                return Conflict("This key is already used or still processing.");
            }

            try
            {
                var response = await work();
                return Ok(response);
            }
            catch (WalletException ex)
            {
                await _idempotencyService.MarkFailedAsync(idempotencyKey, ex.StatusCode, ex.Message, HttpContext.RequestAborted);
                return StatusCode(ex.StatusCode, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected failure processing {Path} with key {Key}", path, idempotencyKey);

                try
                {
                    _db.ChangeTracker.Clear();
                    await _idempotencyService.MarkFailedAsync(idempotencyKey, 500,
                        "An unexpected error occurred. Please retry with a new Idempotency-Key.", HttpContext.RequestAborted);
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx, "Failed to record failure for key {Key}", idempotencyKey);
                }

                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred. Please retry with a new Idempotency-Key.");
            }
        }

        [HttpPost("create-wallet")]
        public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequest request)
        {
            try
            {
                var response = await _walletService.CreateWalletAsync(GetUserId(), request, HttpContext.RequestAborted);
                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (AccountNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (WalletNameConflictException ex)
            {
                return Conflict(ex.Message);
            }
            catch (WalletCreationFailedException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("wallets")]
        public async Task<IActionResult> GetAllWallets([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            if (page < 1)
            {
                return BadRequest("page must be at least 1");
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest("limit must be between 1 and 100");
            }

            var response = await _walletService.GetAllWalletsAsync(GetUserId(), page, limit, HttpContext.RequestAborted);
            return Ok(response);
        }

        [HttpGet("{walletId:guid}")]
        public async Task<IActionResult> GetWalletById(Guid walletId)
        {
            try
            {
                var response = await _walletService.GetWalletByIdAsync(GetUserId(), walletId, HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (WalletNotOwnedException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("{walletId:guid}/history")]
        public async Task<IActionResult> GetWalletHistory(Guid walletId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            if (page < 1)
            {
                return BadRequest("page must be at least 1");
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest("limit must be between 1 and 100");
            }

            try
            {
                var response = await _walletService.GetWalletHistoryAsync(GetUserId(), walletId, page, limit, HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (WalletNotOwnedException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetAccountTransactions([FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] Guid? walletId = null)
        {
            if (page < 1)
            {
                return BadRequest("page must be at least 1");
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest("limit must be between 1 and 100");
            }

            try
            {
                var response = await _walletService.GetAccountHistoryAsync(GetUserId(), walletId, page, limit, HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (WalletNotOwnedException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("by-account-number/{accountNumber}")]
        public async Task<IActionResult> GetWalletByAccountNumber(string accountNumber)
        {
            try
            {
                var response = await _walletService.GetWalletByAccountNumberAsync(accountNumber, HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (WalletNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("merchant")]
        public async Task<IActionResult> GetMerchantWallet()
        {
            try
            {
                var response = await _walletService.GetMerchantWalletAsync(HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (WalletNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPatch("{walletId:guid}/close-wallet")]
        public async Task<IActionResult> CloseWallet(Guid walletId)
        {
            try
            {
                await _walletService.CloseWalletAsync(GetUserId(), walletId, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (WalletNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (WalletConflictException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPatch("{walletId:guid}/rename")]
        public async Task<IActionResult> RenameWallet(Guid walletId, [FromBody] RenameWalletRequest request)
        {
            try
            {
                await _walletService.RenameWalletAsync(GetUserId(), walletId, request, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (WalletNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (WalletNameConflictException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("fund")]
        public async Task<IActionResult> FundWallet(
        [FromBody] FundWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey)
        {
            if (idempotencyKey == Guid.Empty)
            {
                return BadRequest("Idempotency-Key header is required.");
            }

            if (decimal.Round(request.Amount, 2) != request.Amount)
            {
                return BadRequest("Amount can have at most 2 decimal places.");
            }

            return await ExecuteIdempotentAsync(idempotencyKey,
                () => _walletService.FundWalletAsync(GetUserId(), request, idempotencyKey, HttpContext.RequestAborted));
        }

        [HttpPost("{walletId:guid}/transfer")]
        public async Task<IActionResult> WalletTransfer(
            Guid walletId,
            [FromBody] WalletTransferRequest request,
            [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey)
        {
            if (idempotencyKey == Guid.Empty)
            {
                return BadRequest("Idempotency-Key header is required.");
            }

            if (decimal.Round(request.Amount, 2) != request.Amount)
            {
                return BadRequest("Amount can have at most 2 decimal places.");
            }

            var destinationWalletId = request.DestinationWalletId!.Value;

            if (destinationWalletId == walletId)
            {
                return BadRequest("Source and destination wallet cannot be the same.");
            }

            var userId = GetUserId();

            return await ExecuteIdempotentAsync(idempotencyKey,
                () => _walletService.TransferAsync(userId, walletId, request, idempotencyKey, HttpContext.RequestAborted));
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> MerchantCheckout(
            [FromBody] WalletCheckoutRequest request,
            [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey)
        {
            if (idempotencyKey == Guid.Empty)
            {
                return BadRequest("Idempotency-Key header is required.");
            }

            if (decimal.Round(request.Amount, 2) != request.Amount)
            {
                return BadRequest("Amount can have at most 2 decimal places.");
            }

            var userWalletId = request.UserWalletId!.Value;
            var merchantWalletId = request.MerchantWalletId!.Value;

            if (userWalletId == merchantWalletId)
            {
                return BadRequest("User and merchant wallet cannot be the same.");
            }

            var userId = GetUserId();

            return await ExecuteIdempotentAsync(idempotencyKey,
                () => _walletService.CheckoutAsync(userId, request, idempotencyKey, HttpContext.RequestAborted));
        }

        [HttpPost("refund")]
        public async Task<IActionResult> RefundPayment(
            [FromBody] RefundRequest request,
            [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey)
        {
            if (idempotencyKey == Guid.Empty)
            {
                return BadRequest("Idempotency-Key header is required.");
            }

            return await ExecuteIdempotentAsync(idempotencyKey,
                () => _walletService.RefundAsync(GetUserId(), request, idempotencyKey, HttpContext.RequestAborted));
        }
    }
}
