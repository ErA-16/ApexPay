namespace ApexPay.Service.Exceptions
{
    public abstract class AuthException : System.Exception
    {
        public int StatusCode { get; }

        protected AuthException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class EmailExistException : AuthException
    {
        public EmailExistException() : base(409, "User with this email already exist") { }
    }

    public class InvalidCredentialsException : AuthException
    {
        public InvalidCredentialsException() : base(401, "Invalid email or password") { }
    }
}
