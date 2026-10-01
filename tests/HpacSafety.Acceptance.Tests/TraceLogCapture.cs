using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Keeps everything a host logs at any level, scopes and structured values
///     included, so a scenario can prove what was never logged. Register it with
///     <see cref="Register" />, which also opens every category to Trace so no
///     configured filter hides a line from it.
/// </summary>
internal sealed class TraceLogCapture : ILoggerProvider, ISupportExternalScope
{
	private readonly ConcurrentQueue<string> _lines = new();
	private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

	/// <summary>Every captured line, scopes and structured values flattened to text.</summary>
	public IReadOnlyCollection<string> Lines => _lines;

	/// <summary>Adds this capture to a host's logging and lets every category through at Trace.</summary>
	public void Register(ILoggingBuilder logging)
	{
		ArgumentNullException.ThrowIfNull(logging);

		logging.ClearProviders();
		logging.SetMinimumLevel(LogLevel.Trace);
		logging.AddProvider(this);
		logging.AddFilter<TraceLogCapture>(null, LogLevel.Trace);
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new CapturingLogger(this, categoryName);
	}

	public void SetScopeProvider(IExternalScopeProvider scopeProvider)
	{
		_scopes = scopeProvider;
	}

	public void Dispose()
	{
	}

	private sealed class CapturingLogger(TraceLogCapture owner, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return owner._scopes.Push(state);
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter)
		{
			var text = new StringBuilder().Append(category).Append(' ').Append(formatter(state, exception));

			if (state is IEnumerable<KeyValuePair<string, object?>> values)
			{
				foreach (var (key, value) in values)
				{
					text.Append(' ').Append(key).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
				}
			}

			owner._scopes.ForEachScope(
				(scope, builder) => builder.Append(' ').Append(Convert.ToString(scope, CultureInfo.InvariantCulture)),
				text);

			if (exception is not null)
			{
				text.Append(' ').Append(exception);
			}

			owner._lines.Enqueue(text.ToString());
		}
	}
}
