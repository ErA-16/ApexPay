namespace ApexPay.Service.Exceptions
{
    public abstract class WalletException : System.Exception
    {
        public int StatusCode { get; }

        protected WalletException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class WalletAlreadyClosedException : WalletException
    {
        public WalletAlreadyClosedException() : base(409, "This wallet is closed") { }
        public WalletAlreadyClosedException(string message) : base(409, message) { }
    }

    public class WalletNotFoundException : WalletException
    {
        public WalletNotFoundException() : base(404, "Wallet does not exist") { }
        public WalletNotFoundException(string message) : base(404, message) { }
    }

    public class InsufficientBalanceException : WalletException
    {
        public InsufficientBalanceException() : base(409, "Insufficient balance") { }
        public InsufficientBalanceException(string message) : base(409, message) { }
    }

    public class WalletNameConflictException : WalletException
    {
        public WalletNameConflictException() : base(409, "You already have a wallet with this name") { }
    }

    public class AccountNotFoundException : WalletException
    {
        public AccountNotFoundException() : base(404, "Account not found") { }
    }

    public class WalletCreationFailedException : WalletException
    {
        public WalletCreationFailedException() : base(500, "Could not create wallet, please try again") { }
    }

    public class WalletNotOwnedException : WalletException
    {
        public WalletNotOwnedException() : base(404, "Wallet not found") { }
    }

    public class WalletConflictException : WalletException
    {
        public WalletConflictException(string message) : base(409, message) { }
    }

    public class WalletBadRequestException : WalletException
    {
        public WalletBadRequestException(string message) : base(400, message) { }
    }

    public class WalletProcessingFailedException : WalletException
    {
        public WalletProcessingFailedException(string message) : base(500, message) { }
    }
}