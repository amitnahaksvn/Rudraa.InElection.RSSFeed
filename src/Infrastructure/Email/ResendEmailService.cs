using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;
using Application.Abstractions;
using Application.Options;
using Domain.Entities;

namespace Infrastructure.Email;

/// <summary>
/// <see cref="IEmailService"/> implementation backed by the official Resend .NET SDK
/// (<see cref="IResend"/> - never a raw <c>HttpClient</c> call). Registered/retried/configured in
/// <c>InfrastructureServiceCollectionExtensions.AddInfrastructure</c> alongside every other
/// external HTTP dependency in this codebase. Every public method here is a guaranteed no-throw
/// boundary: a monitoring-alert email failing to send must never itself crash the crawler it was
/// reporting on, so every failure (network, auth, Resend API error) is caught and logged, never
/// propagated.
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailTemplateBuilder _templateBuilder;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IServiceScopeFactory scopeFactory, EmailTemplateBuilder templateBuilder, IOptions<EmailOptions> options, ILogger<ResendEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _templateBuilder = templateBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> SendErrorLogBatchAsync(IReadOnlyList<ErrorLog> errors, CancellationToken cancellationToken = default)
    {
        if (errors.Count == 0)
        {
            return Task.FromResult(false);
        }

        var (subject, html) = _templateBuilder.BuildErrorLogBatch(errors);
        return SendAsync(subject, html, "ErrorLogBatch", cancellationToken);
    }

    public Task<bool> SendWarningAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var (fullSubject, html) = _templateBuilder.BuildSimple("Warning", subject, message);
        return SendAsync(fullSubject, html, "Warning", cancellationToken);
    }

    public Task<bool> SendInformationAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var (fullSubject, html) = _templateBuilder.BuildSimple("Information", subject, message);
        return SendAsync(fullSubject, html, "Information", cancellationToken);
    }

    public Task<bool> SendSuccessAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var (fullSubject, html) = _templateBuilder.BuildSimple("Success", subject, message);
        return SendAsync(fullSubject, html, "Success", cancellationToken);
    }

    private async Task<bool> SendAsync(string subject, string html, string category, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email notifications disabled (Email:Enabled=false) - skipped {Category} email '{Subject}'", category, subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.From) || _options.To.Count == 0)
        {
            _logger.LogWarning(
                "Email not configured (Email:ApiKey/From/To) - skipped {Category} email '{Subject}'", category, subject);
            return false;
        }

        var message = new EmailMessage
        {
            From = _options.From,
            To = EmailAddressList.From(_options.To),
            Subject = subject,
            HtmlBody = html
        };

        try
        {
            // IResend (via the Resend SDK's own AddResend registration) transitively needs
            // IOptionsSnapshot<ResendClientOptions>, which is Scoped - resolving it once into this
            // Singleton's constructor would be a captive dependency (.NET's DI throws "Cannot
            // resolve scoped service ... from root provider" the first time it's actually used).
            // A short-lived scope per send keeps this service itself Singleton, matching the
            // orchestrators that hold a direct IEmailService reference in their own constructors.
            using var scope = _scopeFactory.CreateScope();
            var resend = scope.ServiceProvider.GetRequiredService<IResend>();
            var response = await resend.EmailSendAsync(message, cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation(
                    "Sent {Category} email '{Subject}' to {To} (Resend id {EmailId})",
                    category, subject, string.Join(",", _options.To), response.Content);
                return true;
            }

            _logger.LogError(
                response.Exception,
                "Failed to send {Category} email '{Subject}' to {To} - Resend API returned an unsuccessful response",
                category, subject, string.Join(",", _options.To));
            return false;
        }
        catch (Exception ex)
        {
            // Never rethrow: a broken email pipeline must never take the crawler down with it.
            _logger.LogError(ex, "Failed to send {Category} email '{Subject}' to {To}", category, subject, string.Join(",", _options.To));
            return false;
        }
    }
}
