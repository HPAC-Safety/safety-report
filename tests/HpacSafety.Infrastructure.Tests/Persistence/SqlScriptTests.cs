using HpacSafety.Infrastructure.Persistence.Sql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>Raw SQL is loaded from its own embedded file, never typed as a string literal (ADR-0055).</summary>
public class SqlScriptTests
{
	[Fact]
	public void GivenEmbeddedScript_WhenRead_ThenItsSqlIsReturned()
	{
		SqlScript.Read("20260923010810_CopyChoicesOntoQuestions.sql").ShouldContain("INSERT INTO question_choices");
	}

	[Fact]
	public void GivenNoSuchScript_WhenRead_ThenRefusedByName()
	{
		Should.Throw<InvalidOperationException>(() => SqlScript.Read("missing.sql")).Message.ShouldContain("missing.sql");
	}
}
