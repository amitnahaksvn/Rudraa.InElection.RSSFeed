using Microsoft.AspNetCore.SignalR;

namespace WebPlatform.Hubs;

/// <summary>Push channel for the error-monitor UI - clients only listen (see SignalRErrorLogNotifier), they never call a method on this hub, so it declares none of its own.</summary>
public sealed class ErrorLogHub : Hub;
