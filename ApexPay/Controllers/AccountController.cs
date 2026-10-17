using ApexPay.Dto;
using ApexPay.Service;
using ApexPay.Service.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ApexPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }


        [HttpPost("signup")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> SignUp([FromBody] CreateAccountRequest request)
        {
            try
            {
                var response = await _accountService.SignUpAsync(request);
                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (AuthException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _accountService.LoginAsync(request);
                return StatusCode(StatusCodes.Status200OK, response);
            }
            catch (AuthException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

            try
            {
                var response = await _accountService.GetMeAsync(userId);
                return StatusCode(StatusCodes.Status200OK, response);
            }
            catch (AccountNotFoundException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _accountService.RequestPasswordResetAsync(request);
            return StatusCode(StatusCodes.Status200OK, "If an account with that email exists, a reset link has been sent.");
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _accountService.ResetPasswordAsync(request);
                return StatusCode(StatusCodes.Status204NoContent);
            }
            catch (AuthException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }
    }
}
