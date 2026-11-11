using TimeTrack.Domain.Common;
using TimeTrack.Domain.Projects;

namespace TimeTrack.Domain.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void Create_NormalizesCodeToUpperCase()
    {
        var project = Project.Create(" cld-2026-021 ", "Azure Cloud Migration", "Fabrikam Bank", 900);

        project.Code.ShouldBe("CLD-2026-021");
        project.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("CLD 2026")]
    [InlineData("CLD_2026")]
    [InlineData("THIS-CODE-IS-WAY-TOO-LONG")]
    public void Create_WithInvalidCode_Throws(string code)
    {
        var exception = Should.Throw<DomainException>(
            () => Project.Create(code, "Name", "Client", 100));

        exception.Code.ShouldBe("Project.InvalidCode");
    }

    [Fact]
    public void Create_WithZeroBudget_Throws()
    {
        var exception = Should.Throw<DomainException>(
            () => Project.Create("AUD-1", "Audit", "Client", 0));

        exception.Code.ShouldBe("Project.InvalidBudget");
    }
}
