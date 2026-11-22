using Microsoft.AspNetCore.SignalR;

namespace SkladApi.Hubs;

/// <summary>
/// Пуш результатов асинхронного разбора накладной в Angular.
/// Клиент вызывает <c>GetConnectionId</c> и передаёт его в <c>POST parse-async</c>.
/// </summary>
public sealed class ParsingHub : Hub
{
    public string GetConnectionId() => Context.ConnectionId;
}
