﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using Tochka.JsonRpc.ApiExplorer;
using Tochka.JsonRpc.Common;
using Tochka.JsonRpc.Server.Serialization;

namespace Tochka.JsonRpc.Swagger;

/// <summary>
/// Extensions to configure using Swagger for JSON-RPC in application
/// </summary>
[PublicAPI]
public static class Extensions
{
    /// <summary>
    /// Register services required for JSON-RPC Swagger document generation and configure Swagger options
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to</param>
    /// <param name="xmlDocAssembly">Assembly with xml documentation for API methods and model types</param>
    /// <param name="info">Metadata about API</param>
    /// <param name="setupAction">Delegate used to additionally configure Swagger options</param>
    /// <exception cref="FileNotFoundException">If xml documentation disabled</exception>
    public static IServiceCollection AddSwaggerWithJsonRpc(this IServiceCollection services, Assembly xmlDocAssembly, OpenApiInfo info, Action<SwaggerGenOptions> setupAction)
    {
        services.TryAddTransient<ISchemaGenerator, JsonRpcSchemaGenerator>();
        services.TryAddSingleton<ITypeEmitter, TypeEmitter>();
        if (services.All(static x => x.ImplementationType != typeof(JsonRpcDescriptionProvider)))
        {
            // add by interface if not present
            services.AddTransient<IApiDescriptionProvider, JsonRpcDescriptionProvider>();
        }

        services.AddSwaggerGen(c =>
        {
            setupAction(c);

            // it's impossible to add same model with different serializers, so we have to create separate documents for each serializer
            c.SwaggerDoc(ApiExplorerConstants.DefaultDocumentName, info);
            var jsonSerializerOptionsProviders = services
                .Where(static x => typeof(IJsonSerializerOptionsProvider).IsAssignableFrom(x.ServiceType))
                .Select(static x => x.ImplementationType)
                .Where(static x => x != null)
                .Distinct();
            foreach (var providerType in jsonSerializerOptionsProviders)
            {
                var documentName = ApiExplorerUtils.GetDocumentName(providerType);
                var documentInfo = new OpenApiInfo
                {
                    Title = info.Title,
                    Version = info.Version,
                    Description = $"Serializer: {providerType!.Name}\n{info.Description}",
                    Contact = info.Contact,
                    License = info.License,
                    TermsOfService = info.TermsOfService,
                    Extensions = info.Extensions
                };
                c.SwaggerDoc(documentName, documentInfo);
            }

            c.DocInclusionPredicate(DocumentSelector);
            c.SchemaFilter<JsonRpcPropertiesFilter>();

            // to correctly create request and response models with controller.action binding style
            c.CustomSchemaIds(SchemaIdSelector);

            var xmlFile = $"{xmlDocAssembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (!File.Exists(xmlPath))
            {
                // check to enforce users set up their projects properly
                throw new FileNotFoundException("Swagger requires generated XML doc file! Add <GenerateDocumentationFile>true</GenerateDocumentationFile> to your csproj or disable Swagger integration", xmlPath);
            }

            c.IncludeXmlComments(xmlPath);
            c.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    /// <summary>
    /// Register services required for JSON-RPC Swagger document generation with metadata from Assembly and configure Swagger options
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to</param>
    /// <param name="xmlDocAssembly">Assembly with xml documentation for API methods and model types</param>
    /// <param name="setupAction">Delegate used to additionally configure Swagger options</param>
    /// <exception cref="FileNotFoundException">If xml documentation disabled</exception>
    public static IServiceCollection AddSwaggerWithJsonRpc(this IServiceCollection services, Assembly xmlDocAssembly, Action<SwaggerGenOptions> setupAction)
    {
        // returns assembly name, not what Rider shows in Csproj>Properties>Nuget>Title
        var assemblyName = xmlDocAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        var title = $"{assemblyName} {ApiExplorerConstants.DefaultDocumentTitle}".TrimStart();
        var description = xmlDocAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        var info = new OpenApiInfo
        {
            Title = title,
            Version = ApiExplorerConstants.DefaultDocumentVersion,
            Description = description
        };

        return services.AddSwaggerWithJsonRpc(xmlDocAssembly, info, setupAction);
    }

    /// <summary>
    /// Register services required for JSON-RPC Swagger document generation
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to</param>
    /// <param name="xmlDocAssembly">Assembly with xml documentation for API methods and model types</param>
    /// <param name="info">Metadata about API</param>
    /// <exception cref="FileNotFoundException">If xml documentation disabled</exception>
    [ExcludeFromCodeCoverage]
    public static IServiceCollection AddSwaggerWithJsonRpc(this IServiceCollection services, Assembly xmlDocAssembly, OpenApiInfo info) =>
        services.AddSwaggerWithJsonRpc(xmlDocAssembly,
            info,
            static _ => { });

    /// <summary>
    /// Register services required for JSON-RPC Swagger document generation with metadata from Assembly
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to</param>
    /// <param name="xmlDocAssembly">Assembly with xml documentation for API methods and model types</param>
    /// <exception cref="FileNotFoundException">If xml documentation disabled</exception>
    [ExcludeFromCodeCoverage]
    public static IServiceCollection AddSwaggerWithJsonRpc(this IServiceCollection services, Assembly xmlDocAssembly) =>
        services.AddSwaggerWithJsonRpc(xmlDocAssembly,
            static _ => { });

    /// <summary>
    /// Add Swagger JSON endpoints for JSON-RPC
    /// </summary>
    /// <param name="options">SwaggerUI options to add endpoints to</param>
    /// <param name="services">The <see cref="IServiceProvider" /> to retrieve serializer options providers</param>
    /// <param name="name">Name that appears in the document selector drop-down</param>
    /// <remarks>
    /// Adds endpoint for actions with default serializer options and additional endpoints for every <see cref="IJsonSerializerOptionsProvider" />
    /// </remarks>
    public static void JsonRpcSwaggerEndpoints(this SwaggerUIOptions options, IServiceProvider services, string name)
    {
        options.SwaggerEndpoint(GetSwaggerDocumentUrl(ApiExplorerConstants.DefaultDocumentName), name);
        var jsonSerializerOptionsProviders = services.GetRequiredService<IEnumerable<IJsonSerializerOptionsProvider>>();
        foreach (var provider in jsonSerializerOptionsProviders)
        {
            var documentName = ApiExplorerUtils.GetDocumentName(provider.GetType());
            options.SwaggerEndpoint(GetSwaggerDocumentUrl(documentName), $"{name} {GetSwaggerEndpointSuffix(provider)}");
        }
    }

    /// <summary>
    /// Add Swagger JSON endpoints for JSON-RPC with default name (<see cref="ApiExplorerConstants.DefaultDocumentTitle" />)
    /// </summary>
    /// <param name="options">SwaggerUI options to add endpoints to</param>
    /// <param name="services">The <see cref="IServiceProvider" /> to retrieve serializer options providers</param>
    /// <remarks>
    /// Adds endpoint for actions with default serializer options and additional endpoints for every <see cref="IJsonSerializerOptionsProvider" />
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static void JsonRpcSwaggerEndpoints(this SwaggerUIOptions options, IServiceProvider services) =>
        options.JsonRpcSwaggerEndpoints(services, ApiExplorerConstants.DefaultDocumentTitle);

    // internal for tests
    internal static bool DocumentSelector(string docName, ApiDescription description)
    {
        if (docName.StartsWith(ApiExplorerConstants.DefaultDocumentName, StringComparison.Ordinal))
        {
            return description.GroupName == docName;
        }

        return description.GroupName == null || description.GroupName == docName;
    }

    // internal for tests
    internal static string SchemaIdSelector(Type type) =>
        type.Assembly.FullName?.StartsWith(ApiExplorerConstants.GeneratedModelsAssemblyName, StringComparison.Ordinal) == true
            ? type.FullName!
            : type.Name;

    [ExcludeFromCodeCoverage]
    private static string GetSwaggerDocumentUrl(string docName) => $"/swagger/{docName}/swagger.json";

    [ExcludeFromCodeCoverage]
    private static string GetSwaggerEndpointSuffix(IJsonSerializerOptionsProvider jsonSerializerOptionsProvider)
    {
        var caseName = jsonSerializerOptionsProvider.GetType().Name.Replace(nameof(IJsonSerializerOptionsProvider)[1..], "");
        return jsonSerializerOptionsProvider.Options.ConvertName(caseName);
    }
}
