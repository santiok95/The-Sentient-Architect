using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class AnalysisFinding : BaseEntity
{
    public AnalysisFinding(Guid analysisReportId, FindingSeverity severity, string category, string message,
        string? filePath = null, int? lineNumber = null)
    {
        AnalysisReportId = analysisReportId;
        Severity = severity;
        Category = category;
        Message = message;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    private AnalysisFinding() { }

    public Guid AnalysisReportId { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? FilePath { get; private set; }
    public int? LineNumber { get; private set; }

    public AnalysisReport? AnalysisReport { get; private set; }
}
