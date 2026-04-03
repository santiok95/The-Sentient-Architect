using FluentAssertions;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.UnitTests.Domain;

public class RepositoryInfoTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Constructor_ShouldSetRepositoryUrlAndTrust()
    {
        var repo = new RepositoryInfo(UserId, TenantId, "https://github.com/user/repo", RepositoryTrust.Internal);

        repo.RepositoryUrl.Should().Be("https://github.com/user/repo");
        repo.Trust.Should().Be(RepositoryTrust.Internal);
    }

    [Fact]
    public void SetLocalPath_ShouldUpdateLocalPath()
    {
        var repo = new RepositoryInfo(UserId, TenantId, "https://github.com/user/repo", RepositoryTrust.External);

        repo.SetLocalPath("/tmp/clones/repo");

        repo.LocalPath.Should().Be("/tmp/clones/repo");
    }

    [Fact]
    public void ClearLocalPath_ShouldSetLocalPathToNull()
    {
        var repo = new RepositoryInfo(UserId, TenantId, "https://github.com/user/repo", RepositoryTrust.External);
        repo.SetLocalPath("/tmp/clones/repo");

        repo.ClearLocalPath();

        repo.LocalPath.Should().BeNull();
    }

    [Fact]
    public void UpdateGitMetadata_ShouldSetAllMetadataFields()
    {
        var repo       = new RepositoryInfo(UserId, TenantId, "https://github.com/user/repo", RepositoryTrust.Internal);
        var lastCommit = DateTime.UtcNow.AddDays(-1);

        repo.UpdateGitMetadata("main", 500, 12, lastCommit);

        repo.DefaultBranch.Should().Be("main");
        repo.StarCount.Should().Be(500);
        repo.ContributorCount.Should().Be(12);
        repo.LastCommitAt.Should().Be(lastCommit);
    }

    [Fact]
    public void MarkAnalyzed_ShouldSetLastAnalyzedAtToNow()
    {
        var repo   = new RepositoryInfo(UserId, TenantId, "https://github.com/user/repo", RepositoryTrust.Internal);
        var before = DateTime.UtcNow;

        repo.MarkAnalyzed();

        repo.LastAnalyzedAt.Should().NotBeNull();
        repo.LastAnalyzedAt!.Value.Should().BeOnOrAfter(before);
    }
}
