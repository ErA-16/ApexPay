using ApexPay.Service.Exceptions;

public class ExceptionContractTests
{
    [Fact]
    public void WalletExceptions_CarryTheContractStatusCodes()
    {
        Assert.Equal(404, new WalletNotFoundException().StatusCode);
        Assert.Equal(404, new WalletNotOwnedException().StatusCode);
        Assert.Equal(404, new AccountNotFoundException().StatusCode);
        Assert.Equal(409, new WalletAlreadyClosedException().StatusCode);
        Assert.Equal(409, new InsufficientBalanceException().StatusCode);
        Assert.Equal(409, new WalletNameConflictException().StatusCode);
        Assert.Equal(409, new WalletConflictException("x").StatusCode);
        Assert.Equal(400, new WalletBadRequestException("x").StatusCode);
        Assert.Equal(500, new WalletCreationFailedException().StatusCode);
        Assert.Equal(500, new WalletProcessingFailedException("x").StatusCode);
    }

    [Fact]
    public void AuthExceptions_CarryTheContractStatusCodes()
    {
        Assert.Equal(409, new EmailExistException().StatusCode);
        Assert.Equal(401, new InvalidCredentialsException().StatusCode);
        Assert.Equal(400, new InvalidResetTokenException().StatusCode);
    }

    [Fact]
    public void WalletExceptions_CarryTheirMessage()
    {
        Assert.Equal("Wallet not found", new WalletNotFoundException("Wallet not found").Message);
        Assert.Equal("Transaction is not refundable.", new WalletBadRequestException("Transaction is not refundable.").Message);
        Assert.Equal("Could not process funding. Please retry with a new Idempotency-Key.",
            new WalletProcessingFailedException("Could not process funding. Please retry with a new Idempotency-Key.").Message);
    }
}