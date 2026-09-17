using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Account;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Application.Integrations;
using TrainCoach.Application.Nutrition;
using TrainCoach.Application.Planning;
using TrainCoach.Application.Platform;
using TrainCoach.Application.Relationships;
using TrainCoach.Application.Reporting;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAccountExportService, AccountExportService>();
        services.AddScoped<IRelationshipAccessGuard, RelationshipAccessGuard>();
        services.AddScoped<ICoachAthleteRelationshipService, CoachAthleteRelationshipService>();
        services.AddScoped<ITrainingPlanService, TrainingPlanService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ICheckInService, CheckInService>();

        services.AddScoped<IReportMetricsCalculator, ReportMetricsCalculator>();
        services.AddScoped<IReportRuleEngine, ReportRuleEngine>();
        services.AddScoped<IReportNarrativeComposer, ReportNarrativeComposer>();
        services.AddScoped<IReportGenerationService, ReportGenerationService>();
        services.AddScoped<IReportDeliveryDispatcher, ReportDeliveryDispatcher>();
        services.AddScoped<IReportDeliveryChannel, InAppReportDeliveryChannel>();
        services.AddScoped<IReportDeliveryChannel, EmailReportDeliveryChannel>();
        services.AddScoped<IReportDeliveryChannel, PushReportDeliveryChannel>();
        services.AddScoped<IReportDeliveryChannel, TelegramReportDeliveryChannel>();

        services.AddPlanningExtras();
        services.AddWellnessExtras();
        services.AddNutrition();
        services.AddPlatform();
        services.AddIntegrationsApplication();

        return services;
    }
}
