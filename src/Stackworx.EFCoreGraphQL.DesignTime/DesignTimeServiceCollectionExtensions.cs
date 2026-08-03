namespace Stackworx.EfCoreGraphQL.DesignTime;

using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL;

/// <summary>
/// Registers DataLoader generation with the EF Core design-time services, from
/// <c>IDesignTimeServices.ConfigureDesignTimeServices</c>.
/// </summary>
public static class DesignTimeServiceCollectionExtensions
{
    /// <summary>
    /// Generates DataLoaders alongside the model snapshot using the default options
    /// (<see cref="Abstractions.Mode.OptOut"/>, no filter, foreign-key fields hidden).
    /// </summary>
    public static IServiceCollection AddEfCoreGraphQL(this IServiceCollection services)
        => services.AddSingleton<IMigrationsCodeGenerator, EfCoreMigrationsCodeGenerator>();

    /// <summary>
    /// Generates DataLoaders alongside the model snapshot using <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    /// Leave <see cref="GenerateOptions.Namespace"/> null to keep the namespace derived from the model
    /// snapshot's namespace.
    /// </remarks>
    public static IServiceCollection AddEfCoreGraphQL(this IServiceCollection services, GenerateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return services
            .AddSingleton(options)
            .AddEfCoreGraphQL();
    }

    /// <summary>
    /// Generates DataLoaders alongside the model snapshot, configuring the options with
    /// <paramref name="configure"/>.
    /// </summary>
    public static IServiceCollection AddEfCoreGraphQL(this IServiceCollection services, Action<GenerateOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GenerateOptions();
        configure(options);

        return services.AddEfCoreGraphQL(options);
    }
}
