using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Infrastructure.Persistence.Conventions;

/// <summary>
///     Every application table except the append-only <c>audit_log</c> carries a
///     <c>Deleted timestamptz null</c> column and is filtered to its live rows by
///     default. See <c>.spec/data-and-persistence.md</c>.
/// </summary>
public static class SoftDeleteFilters
{
	/// <summary>
	///     Entities whose removed rows their aggregate reads itself, so a filter
	///     would hide rows the domain depends on. A question's removed choice is
	///     still what a fork copies and what stops a reporter reviving it; the
	///     question filters to live choices in <c>Question.Choices</c> (ADR-0095).
	///     A choice's stamped parent link is the row ticking that parent again
	///     restores; the choice filters to live links in <c>ParentChoiceIds</c>
	///     (ADR-0151).
	/// </summary>
	private static readonly HashSet<Type> Unfiltered =
	[
		typeof(Core.Features.QuestionBank.QuestionChoice),
		typeof(Core.Features.QuestionBank.ChoiceParentLink),
	];

	/// <summary>Applies the default live-row filter to every entity with a <c>Deleted</c> property.</summary>
	/// <param name="modelBuilder">The model being built.</param>
	public static void Apply(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		foreach (var entity in modelBuilder.Model.GetEntityTypes())
		{
			if (entity.FindProperty("Deleted") is null
				|| Unfiltered.Contains(entity.ClrType))
			{
				continue;
			}

			var parameter = Expression.Parameter(entity.ClrType, "e");
			var property = Expression.Property(parameter, "Deleted");
			var isNull = Expression.Equal(property, Expression.Constant(null, property.Type));
			var lambda = Expression.Lambda(isNull, parameter);

			modelBuilder.Entity(entity.ClrType).HasQueryFilter(lambda);
		}
	}
}
