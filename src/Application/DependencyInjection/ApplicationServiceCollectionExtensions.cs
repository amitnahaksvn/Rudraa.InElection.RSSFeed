using FluentValidation;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Abstractions;
using Application.Common.Behaviours;
using Application.Options;
using Application.Services;

namespace Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the crawler orchestrator, the CQRS mediator pipeline (Mediator + FluentValidation
    /// + Logging/UnhandledException/Validation/Performance behaviours, in that order), and binds
    /// the "NewsCrawler" options section.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<NewsCrawlerOptions>()
            .Bind(configuration.GetSection(NewsCrawlerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<INewsCrawlerService, NewsCrawlerOrchestrator>();
        services.AddSingleton<IRawResponseCleanupService, RawResponseCleanupService>();

        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly, ServiceLifetime.Singleton);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));

        return services;
    }
}
