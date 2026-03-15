using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.EmailVerificationServices
{
    public class UnavailableEmailVerificationRateLimitService : IEmailVerificationRateLimitService
    {
        public Task EnforceSendRateLimitAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            throw new InternalServerException("Email verification rate limit is unavailable.");
        }
    }
}
