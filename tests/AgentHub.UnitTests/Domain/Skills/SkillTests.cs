using AIAgentHub.Domain.Skills;

namespace AgentHub.UnitTests.Domain.Skills;

public sealed class SkillTests
{
    [Fact]
    public void Skill_Properties()
    {
        var skill = new Skill
        {
            Name = "skill1",
            Description = "desc",
            Author = "author",
            ProviderId = "opencode",
            IsEnabled = true,
            FilePath = "/path",
            Content = "content"
        };
        Assert.Equal("skill1", skill.Name);
    }
}
