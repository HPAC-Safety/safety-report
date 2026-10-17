using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     The one role resolver (ADR-0199, ADR-0200): sources, precedence, matching,
///     and failing closed. Pure rules run against an unreachable database, which
///     is also the unreadable-store case.
/// </summary>
public sealed class RoleResolverTests
{
	private static readonly RoleResolverMode Development = new(IncludeDevelopmentSources: true, InterimIssuerEnabled: false);
	private static readonly RoleResolverMode Interim = new(IncludeDevelopmentSources: false, InterimIssuerEnabled: true);
	private static readonly RoleResolverMode Provider = new(IncludeDevelopmentSources: false, InterimIssuerEnabled: false);

	[Theory]
	[InlineData("dev:admin", MemberRole.Administrator)]
	[InlineData("dev:officer", MemberRole.SafetyOfficer)]
	[InlineData("dev:user", MemberRole.User)]
	[InlineData("dev:Admin", MemberRole.User)]
	[InlineData("dev:stranger", MemberRole.User)]
	public async Task GivenDevelopment_WhenAFixedAccountIsResolved_ThenItHoldsItsOwnRole(string subject,
																						MemberRole expected)
	{
		// Given
		var resolver = Resolver(Development);

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(expected);
	}

	[Theory]
	[InlineData("dev:Admin@Example.COM", MemberRole.Administrator)]
	[InlineData("dev:  officer@example.com ", MemberRole.SafetyOfficer)]
	[InlineData("dev:nobody@example.com", MemberRole.User)]
	public async Task GivenDevelopment_WhenAnEmailOnTheListsIsResolved_ThenCaseAndSpacesDoNotMatter(string subject,
																								  MemberRole expected)
	{
		// Given
		var resolver = Resolver(
			Development,
			administrators: ["ADMIN@example.com"],
			officers: ["Officer@Example.com"]);

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("auth0|123")]
	public async Task GivenDevelopment_WhenASubjectIsNotADevelopmentOne_ThenItIsOnlyAUser(string? subject)
	{
		// Given
		var resolver = Resolver(Development, administrators: ["admin"]);

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.User);
	}

	[Fact]
	public async Task GivenAnEmailOnBothLists_WhenResolved_ThenAdministratorWins()
	{
		// Given
		var resolver = Resolver(Development, administrators: ["both@example.com"], officers: ["both@example.com"]);

		// When
		var role = await resolver.Resolve("dev:both@example.com", TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.Administrator);
	}

	[Theory]
	[InlineData("member:listed@example.com")]
	[InlineData("dev:listed@example.com")]
	[InlineData("dev:admin")]
	public async Task GivenOutsideDevelopment_WhenTheListsAndFixedAccountsAreAsked_ThenTheyAreIgnored(string subject)
	{
		// Given
		var resolver = Resolver(Interim, administrators: ["listed@example.com"]);

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then — an interim host reads only its own member: subjects, and the lists are Development's
		role.ShouldBe(MemberRole.User);
	}

	[Theory]
	[InlineData("member:super@example.com")]
	[InlineData("dev:super@example.com")]
	public async Task GivenARealProvider_WhenASubjectLooksLikeAnotherIssuers_ThenItResolvesToUser(string subject)
	{
		// Given — an opaque provider subject must never pose as an interim or development one
		var resolver = Resolver(Provider, superAdmin: "super@example.com");

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.User);
	}

	[Fact]
	public async Task GivenTheSuperAdministrator_WhenTheStoreCannotBeRead_ThenTheyAreStillAnAdministrator()
	{
		// Given
		var resolver = Resolver(Interim, superAdmin: " Super@Example.com ");

		// When
		var role = await resolver.Resolve("member:SUPER@example.com", TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.Administrator);
	}

	[Fact]
	public async Task GivenAnyoneElse_WhenTheStoreCannotBeRead_ThenTheyAreAUser()
	{
		// Given
		var resolver = Resolver(Interim, superAdmin: "super@example.com");

		// When
		var role = await resolver.Resolve("member:someone@example.com", TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.User);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("dev:")]
	[InlineData("auth0|abc")]
	public async Task GivenNoUsableSubject_WhenResolved_ThenAUser(string? subject)
	{
		// Given
		var resolver = Resolver(Development, superAdmin: "super@example.com");

		// When
		var role = await resolver.Resolve(subject, TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(MemberRole.User);
	}

	private static RoleResolver Resolver(RoleResolverMode mode,
										 IReadOnlyList<string>? administrators = null,
										 IReadOnlyList<string>? officers = null,
										 string? superAdmin = null)
	{
		var database = new HpacSafetyDbContext(new DbContextOptionsBuilder<HpacSafetyDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1;Command Timeout=1")
			.Options);

		return new RoleResolver(
			database,
			Options.Create(new HpacAuthenticationOptions { SuperAdmin = superAdmin }),
			Options.Create(new MembersSiteLoginOptions
			{
				AdministratorEmails = administrators ?? [],
				SafetyOfficerEmails = officers ?? [],
			}),
			mode,
			NullLogger<RoleResolver>.Instance);
	}
}

/// <summary>The resolver against the real database: assignments and Development's lists together.</summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class RoleResolverDatabaseTests(ApiPostgresFixture fixture)
{
	[Theory]
	[InlineData(MemberRole.Administrator, "safety_officer", MemberRole.Administrator)]
	[InlineData(MemberRole.SafetyOfficer, "administrator", MemberRole.Administrator)]
	[InlineData(MemberRole.SafetyOfficer, "safety_officer", MemberRole.SafetyOfficer)]
	public async Task GivenAListAndAnAssignment_WhenResolved_ThenTheHighestRoleWins(MemberRole assigned,
																				  string listedAs,
																				  MemberRole expected)
	{
		// Given — Development: the list names the email as listedAs; the database assigns it `assigned`.
		var email = $"{Guid.NewGuid():n}@example.com";
		await using var host = fixture.Factory.WithWebHostBuilder(builder =>
			builder.UseSetting(
				listedAs == "administrator" ? "MembersSiteLogin:AdministratorEmails:0" : "MembersSiteLogin:SafetyOfficerEmails:0",
				email.ToUpperInvariant()));
		await using var scope = host.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		database.RoleAssignments.Add(RoleAssignment.Grant(email, assigned, "seed", DateTimeOffset.UtcNow));
		await database.SaveChangesAsync(TestContext.Current.CancellationToken);

		// When
		var role = await scope.ServiceProvider.GetRequiredService<IRoleResolver>().Resolve($"dev:{email}", TestContext.Current.CancellationToken);

		// Then
		role.ShouldBe(expected);
	}
}
