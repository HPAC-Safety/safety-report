using System.Buffers.Text;
using System.Security.Cryptography;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

public class BrowserReceiptTests
{
	[Fact]
	public void GivenANewReceipt_WhenInspected_ThenItIs256BitsOfBase64Url()
	{
		// Given / When
		var (receipt, _) = BrowserReceipt.New();

		// Then
		receipt.Length.ShouldBe(BrowserReceipt.Length);
		Base64Url.DecodeFromChars(receipt).Length.ShouldBe(32);
	}

	[Fact]
	public void GivenTwoReceipts_WhenCompared_ThenTheyAreUnrelated()
	{
		// Given / When
		var first = BrowserReceipt.New();
		var second = BrowserReceipt.New();

		// Then
		first.Receipt.ShouldNotBe(second.Receipt);
		first.Hash.ShouldNotBe(second.Hash);
	}

	[Fact]
	public void GivenANewReceipt_WhenHashed_ThenTheHashIsItsSha256()
	{
		// Given
		var (receipt, hash) = BrowserReceipt.New();

		// When
		var hashed = BrowserReceipt.TryHash(receipt, out var again);

		// Then
		hashed.ShouldBeTrue();
		again.ShouldBe(hash);
		hash.ShouldBe(Base64Url.EncodeToString(SHA256.HashData(Base64Url.DecodeFromChars(receipt))));
		hash.ShouldNotContain(receipt, Case.Sensitive);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("short")]
	[InlineData("this is not base64url and is exactly 43 chars!!")]
	[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
	public void GivenAnythingButAReceipt_WhenHashed_ThenItIsRefused(string? candidate)
	{
		// Given / When
		var hashed = BrowserReceipt.TryHash(candidate, out var hash);

		// Then
		hashed.ShouldBeFalse();
		hash.ShouldBeEmpty();
	}
}
