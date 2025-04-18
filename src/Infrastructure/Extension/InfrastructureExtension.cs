using Domain.Interfaces;
using Domain.UnitOfWork;
using Infrastructure.Context;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extension;

public static class InfrastructureExtension
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Registering the UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        // Registering the Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IEducationRepository, EducationRepository>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<ISocialLinkRepository, SocialLinkRepository>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Register health checks
        services.AddHealthChecks()
            .AddDbContextCheck<PortfolioDbContext>("Database");

        return services;
    }
}