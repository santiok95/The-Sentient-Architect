namespace SentientArchitect.Application.Common.Interfaces;

public record UserSummary(Guid Id, string DisplayName, string Role);

public interface IUserService
{
    Task<Dictionary<Guid, UserSummary>> GetUserSummariesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default);
}
