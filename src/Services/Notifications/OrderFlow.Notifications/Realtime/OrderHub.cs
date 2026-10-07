using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrderFlow.Security;

namespace OrderFlow.Notifications.Realtime;

/// <summary>
/// Signed-in browsers receive pushes for their own orders. The customer group comes from the token, never from the client,
/// so nobody can listen to another customer's orders. Joining a single order by id is for admins only.
/// </summary>
[Authorize]
public class OrderHub : Hub
{
    public Task JoinMyOrders() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeConstants.CustomerGroup(Context.User!.GetUserId()));

    [Authorize(Policy = PolicyNames.AdminOnly)]
    public Task JoinOrder(Guid orderId) => Groups.AddToGroupAsync(Context.ConnectionId, RealtimeConstants.OrderGroup(orderId));

    [Authorize(Policy = PolicyNames.AdminOnly)]
    public Task JoinCustomer(Guid customerId) => Groups.AddToGroupAsync(Context.ConnectionId, RealtimeConstants.CustomerGroup(customerId));
}
