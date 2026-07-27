namespace StockSharp.Messages;

/// <summary>
/// Extensions for invoking multicast async events.
/// </summary>
public static class AsyncEventExtensions
{
	/// <summary>
	/// Invoke every handler of the multicast async event and await each returned task.
	/// </summary>
	/// <remarks>
	/// Invoking a multicast delegate directly returns only the last handler's
	/// <see cref="ValueTask"/> — every other handler's task becomes unobserved
	/// fire-and-forget: its exception is swallowed and back-pressure is lost.
	/// Handlers are awaited sequentially in subscription order, so a subscriber
	/// observes messages in order even when its handlers complete asynchronously.
	/// </remarks>
	/// <param name="handler">Event backing delegate. Can be <see langword="null"/>.</param>
	/// <param name="message"><see cref="Message"/>.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/> completing when all handlers have completed.</returns>
	public static ValueTask InvokeAllAsync(this Func<Message, CancellationToken, ValueTask> handler, Message message, CancellationToken cancellationToken)
	{
		if (handler is null)
			return default;

		if (handler.HasSingleTarget)
			return handler(message, cancellationToken);

		return invokeMulti(handler, message, cancellationToken);

		static async ValueTask invokeMulti(Func<Message, CancellationToken, ValueTask> handler, Message message, CancellationToken cancellationToken)
		{
			foreach (var single in Delegate.EnumerateInvocationList(handler))
				await single(message, cancellationToken);
		}
	}
}
