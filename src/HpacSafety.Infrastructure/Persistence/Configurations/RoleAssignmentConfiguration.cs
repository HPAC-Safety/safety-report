using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using HpacSafety.Infrastructure.Persistence.Conversions;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HpacSafety.Infrastructure.Persistence.Configurations;

/// <summary>
///     The <c>role_assignments</c> table (ADR-0199). There is no <c>deleted</c>
///     column and no live-row filter: revoking is the retirement, and a trigger
///     refuses a delete, a truncate, and any update but one revoke (CON-DP-017).
///     The trigger and the view come from the migration's <c>.sql</c> file.
/// </summary>
public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
	/// <summary>The partial unique index: at most one active assignment per email.</summary>
	public const string ActiveEmailIndex = "ux_role_assignments_active_email";

	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<RoleAssignment> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToTable("role_assignments");
		builder.HasKey(assignment => assignment.Id);

		builder.Property(assignment => assignment.Email).HasMaxLength(RoleAssignment.EmailMaxLength).IsRequired();
		builder.Property(assignment => assignment.Role)
			.HasConversion<EnumCodeConverter<MemberRole>>()
			.HasMaxLength(64)
			.IsRequired();

		// Opaque token subjects, never keys: there is no user table (ADR-0065).
		builder.Property(assignment => assignment.GrantedBySubject).HasMaxLength(RoleAssignment.SubjectMaxLength).IsRequired();
		builder.Property(assignment => assignment.RevokedBySubject).HasMaxLength(RoleAssignment.SubjectMaxLength);

		// One active assignment per email, in any role. A revoked row frees the email.
		builder.HasIndex(assignment => assignment.Email)
			.HasDatabaseName(ActiveEmailIndex)
			.IsUnique()
			.HasFilter("revoked_at IS NULL");

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_role_assignments_role",
			"role IN ('safety_officer', 'administrator')"));

		// The stored form is the normalised one; the application normalises, the database refuses the rest.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_role_assignments_email",
			"email = lower(btrim(email)) AND email !~ '\\s' AND char_length(email) BETWEEN 3 AND 320 AND position('@' in email) > 1"));

		// Revoked when and by whom, or neither.
		builder.ToTable(t => t.HasCheckConstraint(
			"ck_role_assignments_revoked_together",
			"(revoked_at IS NULL) = (revoked_by_subject IS NULL)"));
	}
}

/// <summary>
///     The read-only <c>active_role_assignments</c> view. A migration creates it
///     from its own <c>.sql</c> file (ADR-0055); EF only reads it.
/// </summary>
public sealed class ActiveRoleAssignmentConfiguration : IEntityTypeConfiguration<ActiveRoleAssignment>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ActiveRoleAssignment> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.ToView("active_role_assignments");
		builder.HasKey(assignment => assignment.Id);
		builder.Property(assignment => assignment.Email).HasMaxLength(RoleAssignment.EmailMaxLength);
		builder.Property(assignment => assignment.Role).HasConversion<EnumCodeConverter<MemberRole>>().HasMaxLength(64);
	}
}
