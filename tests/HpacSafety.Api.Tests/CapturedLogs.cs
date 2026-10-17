using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Api.Tests;

/// <summary>Records every message and exception text any logger writes, to prove none holds an email (CON-IF-011).</summary>
internal sealed class CapturedLogs : ILoggerProvider
{
	private readonly ConcurrentQueue<string> _messages = new();

	public IReadOnlyCollection<string> Messages => [.. _messages];

	public ILogger CreateLogger(string categoryName)
	{
		return new Capture(_messages);
	}

	public void Dispose()
	{
	}

	private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
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
			messages.Enqueue(formatter(state, exception) + exception);
		}
	}
}
