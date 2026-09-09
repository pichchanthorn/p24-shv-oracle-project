using backend.Models.Request;

namespace backend.Services
{
    public interface IUserService
    {
        Task<UserServiceResult<object>> RegisterAsync(RegisterRequest request, string? ipAddress);
        Task<UserServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress);
        Task<UserServiceResult<TwoFactorSetupResponse>> StartTwoFactorSetupAsync(int userId, string? ipAddress);
        Task<UserServiceResult<object>> EnableTwoFactorAsync(int userId, TwoFactorEnableRequest request, string? ipAddress);
        Task<UserServiceResult<LoginResponse>> VerifyTwoFactorLoginAsync(TwoFactorVerifyLoginRequest request, string? ipAddress);
        Task<UserServiceResult<object>> DisableTwoFactorAsync(int userId, TwoFactorDisableRequest request, string? ipAddress);
        Task<UserServiceResult<UserProfileResponse>> GetProfileAsync(int userId);
    }

    public enum UserServiceStatus
    {
        Ok,
        Conflict,
        InvalidCredentials,
        AccountLocked,
        ValidationFailed,
        NotFound
    }

    public class UserServiceResult<T>
    {
        public UserServiceStatus Status { get; }
        public string? Message { get; }
        public T? Value { get; }

        private UserServiceResult(UserServiceStatus status, string? message, T? value)
        {
            Status = status;
            Message = message;
            Value = value;
        }

        public static UserServiceResult<T> Ok(T value) =>
            new(UserServiceStatus.Ok, null, value);

        public static UserServiceResult<T> Fail(UserServiceStatus status, string message) =>
            new(status, message, default);
    }
}
