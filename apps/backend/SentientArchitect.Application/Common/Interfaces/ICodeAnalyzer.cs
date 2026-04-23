namespace SentientArchitect.Application.Common.Interfaces;

public interface ICodeAnalyzer
{
    Task AnalyzeAsync(Guid repositoryInfoId, Guid? reportId = null, CancellationToken ct = default);
}
