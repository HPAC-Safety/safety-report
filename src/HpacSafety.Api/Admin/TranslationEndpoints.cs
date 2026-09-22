using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;

namespace HpacSafety.Api.Admin;

/// <summary>
///     Machine translation for an administrator authoring a question.
/// </summary>
/// <remarks>
///     <para>
///         Translation happens here, in the API, and never in the browser. The
///         credential stays on the server: nothing ships it to a page, and no page
///         calls the provider directly. This is the same reason
///         <see cref="ITranslator" /> exists rather than a fetch in the authoring
///         screen.
///     </para>
///     <para>
///         What comes back is a <b>draft</b>. The administrator edits it and saves it
///         deliberately, and the saved revision is theirs — see ADR-0062. Nothing here
///         writes to the question bank.
///     </para>
/// </remarks>
public static class TranslationEndpoints
{
    /// <summary>Maps the admin translation endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The group, so the caller can see what was mapped.</returns>
    public static RouteGroupBuilder MapAdminTranslation(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/admin/translate").RequireAuthorization(HpacPolicies.Administrator);

        group.MapGet("/", Availability);
        group.MapPost("/", TranslateAsync);

        return group;
    }

    /// <summary>
    ///     Whether translation is usable on this server. The authoring screen asks
    ///     once and disables the button when the answer is no, rather than offering
    ///     a control that fails on click.
    /// </summary>
    private static IResult Availability(ITranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);

        // The screen says so plainly when it is the stand-in: a developer
        // seeing their English copied into the French box should know why,
        // rather than concluding the translator is broken.
        return Results.Ok(
            new TranslationAvailability(translator.IsConfigured, translator is EchoTranslator));
    }

    private static async Task<IResult> TranslateAsync(
        TranslateRequest request,
        ITranslator translator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(translator);

        if (!Locale.TryParse(request.From, out var source) || !Locale.TryParse(request.To, out var target))
            return Problem(
                "unknown-locale",
                "That is not one of the two official languages.",
                StatusCodes.Status400BadRequest);

        if (source == target)
            return Problem(
                "same-locale",
                "A translation needs two different languages.",
                StatusCodes.Status400BadRequest);

        // Blank fields are dropped rather than sent: an administrator may leave
        // the help text empty, and a provider charged per request should not be
        // asked to translate nothing. Positions are preserved so the caller can
        // still line results up with what it sent.
        var texts = request.Texts ?? [];
        var translatable = texts
            .Select((text, index) => (text, index))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.text))
            .ToList();

        if (translatable.Count == 0) return Results.Ok(new TranslateResponse([.. texts.Select(_ => string.Empty)]));

        try
        {
            var translated = await translator
                .TranslateAsync([.. translatable.Select(entry => entry.text)], source, target, cancellationToken)
                .ConfigureAwait(false);

            var results = new string[texts.Count];
            Array.Fill(results, string.Empty);

            for (var i = 0; i < translatable.Count; i++) results[translatable[i].index] = translated[i];

            return Results.Ok(new TranslateResponse(results));
        }
        catch (TranslationUnavailableException cause)
        {
            // The exception's own message is authored to be safe to show. The
            // inner exception is not surfaced: it can carry a provider payload,
            // and the submitted text is question wording being drafted.
            return Problem("translation-unavailable", cause.Message, StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult Problem(string code, string detail, int statusCode)
    {
        return Results.Problem(
            title: "Translation failed.",
            detail: detail,
            statusCode: statusCode,
            type: $"https://hpac.ca/problems/{code}");
    }
}

/// <summary>Whether a translation provider is configured on this server.</summary>
/// <param name="Available">True when the Translate control should be offered.</param>
/// <param name="StandIn">
///     True when the provider is the development stand-in, which returns its input
///     unchanged. The screen says so, so nobody mistakes copied English for a
///     translation.
/// </param>
public sealed record TranslationAvailability(bool Available, bool StandIn);

/// <summary>
///     Text to translate between the two official languages.
/// </summary>
/// <param name="Texts">
///     The strings to translate, in order. Blank entries come back blank.
/// </param>
/// <param name="From">The locale the text is written in.</param>
/// <param name="To">The locale to translate into.</param>
public sealed record TranslateRequest(IReadOnlyList<string> Texts, string From, string To);

/// <summary>One translation per submitted string, in the same order.</summary>
/// <param name="Texts">The translations.</param>
public sealed record TranslateResponse(IReadOnlyList<string> Texts);
