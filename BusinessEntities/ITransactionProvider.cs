namespace StockSharp.BusinessEntities;

/// <summary>
/// Transactional operations provider interface.
/// </summary>
public interface ITransactionProvider
{
	/// <summary>
	/// Transaction id generator.
	/// </summary>
	IdGenerator TransactionIdGenerator { get; }

	/// <summary>
	/// Own trade received.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OwnTradeReceived event.")]
	event Action<MyTrade> NewMyTrade;

	/// <summary>
	/// Order received.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderReceived event.")]
	event Action<Order> NewOrder;

	/// <summary>
	/// Order changed (cancelled, matched).
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderReceived event.")]
	event Action<Order> OrderChanged;

	/// <summary>
	/// <see cref="EditOrder"/> success result event.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderReceived event.")]
	event Action<long, Order> OrderEdited;

	/// <summary>
	/// Order registration error event.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderRegisterFailReceived event.")]
	event Action<OrderFail> OrderRegisterFailed;

	/// <summary>
	/// Order cancellation error event.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderCancelFailReceived event.")]
	event Action<OrderFail> OrderCancelFailed;

	/// <summary>
	/// <see cref="EditOrder"/> error result event.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.OrderEditFailReceived event.")]
	event Action<long, OrderFail> OrderEditFailed;

	/// <summary>
	/// Mass order cancellation event.
	/// </summary>
	event Action<long> MassOrderCanceled;

	/// <summary>
	/// Mass order cancellation event.
	/// </summary>
	event Action<long, DateTime> MassOrderCanceled2;

	/// <summary>
	/// Mass order cancellation errors event.
	/// </summary>
	event Action<long, Exception> MassOrderCancelFailed;

	/// <summary>
	/// Mass order cancellation errors event.
	/// </summary>
	event Action<long, Exception, DateTime> MassOrderCancelFailed2;

	/// <summary>
	/// Lookup result <see cref="PortfolioLookupMessage"/> received.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.PortfolioReceived and ISubscriptionProvider.SubscriptionStopped events.")]
	event Action<PortfolioLookupMessage, IEnumerable<Portfolio>, Exception> LookupPortfoliosResult;

	/// <summary>
	/// Lookup result <see cref="PortfolioLookupMessage"/> received.
	/// </summary>
	[Obsolete("Use ISubscriptionProvider.PortfolioReceived and ISubscriptionProvider.SubscriptionStopped events.")]
	event Action<PortfolioLookupMessage, IEnumerable<Portfolio>, IEnumerable<Portfolio>, Exception> LookupPortfoliosResult2;

	/// <summary>
	/// Register new order.
	/// </summary>
	/// <param name="order">Registration details.</param>
	[Obsolete("Use RegisterOrderAsync instead.")]
	void RegisterOrder(Order order);

	/// <summary>
	/// Edit the order.
	/// </summary>
	/// <param name="order">Order.</param>
	/// <param name="changes">Order changes.</param>
	[Obsolete("Use EditOrderAsync instead.")]
	void EditOrder(Order order, Order changes);

	/// <summary>
	/// Reregister the order.
	/// </summary>
	/// <param name="oldOrder">Cancelling order.</param>
	/// <param name="newOrder">New order to register.</param>
	[Obsolete("Use ReRegisterOrderAsync instead.")]
	void ReRegisterOrder(Order oldOrder, Order newOrder);

	/// <summary>
	/// Cancel the order.
	/// </summary>
	/// <param name="order">The order which should be canceled.</param>
	[Obsolete("Use CancelOrderAsync instead.")]
	void CancelOrder(Order order);

	/// <summary>
	/// Cancel orders by filter.
	/// </summary>
	/// <param name="isStopOrder"><see langword="true" />, if cancel only a stop orders, <see langword="false" /> - if regular orders, <see langword="null" /> - both.</param>
	/// <param name="portfolio">Portfolio. If the value is equal to <see langword="null" />, then the portfolio does not match the orders cancel filter.</param>
	/// <param name="direction">Order side. If the value is <see langword="null" />, the direction does not use.</param>
	/// <param name="board">Trading board. If the value is equal to <see langword="null" />, then the board does not match the orders cancel filter.</param>
	/// <param name="security">Instrument. If the value is equal to <see langword="null" />, then the instrument does not match the orders cancel filter.</param>
	/// <param name="securityType">Security type. If the value is <see langword="null" />, the type does not use.</param>
	/// <param name="transactionId">Order cancellation transaction id.</param>
	[Obsolete("Use CancelOrdersAsync instead.")]
	void CancelOrders(bool? isStopOrder = null, Portfolio portfolio = null, Sides? direction = null, ExchangeBoard board = null, Security security = null, SecurityTypes? securityType = null, long? transactionId = null);

	/// <summary>
	/// Register new order without blocking the calling thread.
	/// </summary>
	/// <param name="order">Registration details.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	ValueTask RegisterOrderAsync(Order order, CancellationToken cancellationToken)
	{
#pragma warning disable CS0618 // fallback for implementations without a dedicated async path
		RegisterOrder(order);
#pragma warning restore CS0618
		return default;
	}

	/// <summary>
	/// Edit the order without blocking the calling thread.
	/// </summary>
	/// <param name="order">Order.</param>
	/// <param name="changes">Order changes.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	ValueTask EditOrderAsync(Order order, Order changes, CancellationToken cancellationToken)
	{
#pragma warning disable CS0618 // fallback for implementations without a dedicated async path
		EditOrder(order, changes);
#pragma warning restore CS0618
		return default;
	}

	/// <summary>
	/// Reregister the order without blocking the calling thread.
	/// </summary>
	/// <param name="oldOrder">Cancelling order.</param>
	/// <param name="newOrder">New order to register.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	ValueTask ReRegisterOrderAsync(Order oldOrder, Order newOrder, CancellationToken cancellationToken)
	{
#pragma warning disable CS0618 // fallback for implementations without a dedicated async path
		ReRegisterOrder(oldOrder, newOrder);
#pragma warning restore CS0618
		return default;
	}

	/// <summary>
	/// Cancel the order without blocking the calling thread.
	/// </summary>
	/// <param name="order">The order which should be canceled.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	ValueTask CancelOrderAsync(Order order, CancellationToken cancellationToken)
	{
#pragma warning disable CS0618 // fallback for implementations without a dedicated async path
		CancelOrder(order);
#pragma warning restore CS0618
		return default;
	}

	/// <summary>
	/// Cancel orders by filter without blocking the calling thread.
	/// </summary>
	/// <param name="isStopOrder"><see langword="true" />, if cancel only a stop orders, <see langword="false" /> - if regular orders, <see langword="null" /> - both.</param>
	/// <param name="portfolio">Portfolio. If the value is equal to <see langword="null" />, then the portfolio does not match the orders cancel filter.</param>
	/// <param name="direction">Order side. If the value is <see langword="null" />, the direction does not use.</param>
	/// <param name="board">Trading board. If the value is equal to <see langword="null" />, then the board does not match the orders cancel filter.</param>
	/// <param name="security">Instrument. If the value is equal to <see langword="null" />, then the instrument does not match the orders cancel filter.</param>
	/// <param name="securityType">Security type. If the value is <see langword="null" />, the type does not use.</param>
	/// <param name="transactionId">Order cancellation transaction id.</param>
	/// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
	/// <returns><see cref="ValueTask"/>.</returns>
	ValueTask CancelOrdersAsync(bool? isStopOrder = null, Portfolio portfolio = null, Sides? direction = null, ExchangeBoard board = null, Security security = null, SecurityTypes? securityType = null, long? transactionId = null, CancellationToken cancellationToken = default)
	{
#pragma warning disable CS0618 // fallback for implementations without a dedicated async path
		CancelOrders(isStopOrder, portfolio, direction, board, security, securityType, transactionId);
#pragma warning restore CS0618
		return default;
	}

	/// <summary>
	/// Determines the specified order can be edited by <see cref="EditOrder"/>.
	/// </summary>
	/// <param name="order">Order.</param>
	/// <returns><see langword="true"/> if the order is editable, <see langword="false"/> order cannot be changed, <see langword="null"/> means no information.</returns>
	bool? IsOrderEditable(Order order);

	/// <summary>
	/// Determines the specified order can be replaced by <see cref="ReRegisterOrder"/>.
	/// </summary>
	/// <param name="order">Order.</param>
	/// <returns><see langword="true"/> if the order is replaceable, <see langword="false"/> order cannot be replaced, <see langword="null"/> means no information.</returns>
	bool? IsOrderReplaceable(Order order);
}