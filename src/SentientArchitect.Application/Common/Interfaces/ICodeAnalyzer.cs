namespace SentientArchitect.Application.Common.Interfaces;

public interface ICodeAnalyzer
{
    Task AnalyzeAsync(Guid repositoryInfoId, CancellationToken ct = default);
}
