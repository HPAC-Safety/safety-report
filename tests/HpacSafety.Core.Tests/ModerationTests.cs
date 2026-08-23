using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

public sealed class ModerationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_active_administrator_When_question_access_is_checked_Then_editing_is_allowed()
    {
        var admin = new AdminUser("oidc-subject", AdminRole.Administrator, Now);

        admin.MayEditQuestions.ShouldBeTrue();
        admin.Subject.ShouldBe("oidc-subject");
        admin.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Given_a_reviewer_When_question_access_is_checked_Then_editing_is_refused()
    {
        var admin = new AdminUser("oidc-subject", AdminRole.SafetyOfficer, Now);

        admin.MayEditQuestions.ShouldBeFalse();
    }

    [Fact]
    public void Given_an_administrator_is_revoked_When_question_access_is_checked_Then_editing_is_refused()
    {
        var admin = new AdminUser("oidc-subject", AdminRole.Administrator, Now);

        admin.Revoke();

        admin.IsActive.ShouldBeFalse();
        admin.MayEditQuestions.ShouldBeFalse();
    }

    [Fact]
    public void Given_an_admin_role_changes_When_access_is_checked_Then_the_new_role_applies()
    {
        var admin = new AdminUser("oidc-subject", AdminRole.SafetyOfficer, Now);

        admin.ChangeRole(AdminRole.Administrator);

        admin.Role.ShouldBe(AdminRole.Administrator);
        admin.MayEditQuestions.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_moderation_action_When_it_is_recorded_Then_only_identifiers_and_safe_detail_are_stored()
    {
        var adminId = TinyId.New();
        var targetId = TinyId.New();

        var entry = new AuditLogEntry(
            adminId,
            AuditAction.ApprovedReport,
            "report",
            targetId,
            Now,
            "Approved after safety review");

        entry.AdminUserId.ShouldBe(adminId);
        entry.Action.ShouldBe(AuditAction.ApprovedReport);
        entry.TargetType.ShouldBe("report");
        entry.TargetId.ShouldBe(targetId);
        entry.OccurredAt.ShouldBe(Now);
        entry.Detail.ShouldBe("Approved after safety review");
    }
}
