using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     The development stand-in: returns every string unchanged.
/// </summary>
/// <remarks>
///     <para>
///         A developer has no DeepL credential, and the alternative — disabling the
///         Translate control locally — means the one path most likely to break is the
///         one nobody exercises until production. With this registered, the browser
///         posts to the same endpoint, the endpoint calls the same
///         <see cref="ITranslator" />, and the same code fills the same field. Only the
///         adapter differs.
///     </para>
///     <para>
///         It is <b>never</b> registered outside Development, and it is not a fallback:
///         a production deployment with no credential reports translation unavailable
///         rather than quietly copying English into the French column, which would put
///         untranslated English in front of French-speaking pilots. See ADR-0062.
///     </para>
/// </remarks>
public sealed class EchoTranslator : ITranslator
{
	/// <summary>
	///     Always true. The stand-in has nothing to configure, and the point of it
	///     is that the control works locally.
	/// </summary>
	public bool IsConfigured => true;

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> Translate(
		IReadOnlyList<string> texts, Locale source, Locale target, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(texts);

		return source == target
			? throw new TranslationUnavailableException("A translation needs two different languages.")
			: Task.FromResult<IReadOnlyList<string>>([.. texts]);
	}
}
