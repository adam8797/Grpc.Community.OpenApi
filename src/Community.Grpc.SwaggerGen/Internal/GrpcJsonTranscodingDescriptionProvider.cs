// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Google.Api;
using Google.Protobuf.Reflection;
using Grpc.AspNetCore.Server;
using Grpc.Shared;
using Microsoft.AspNetCore.Grpc.JsonTranscoding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Community.Grpc.SwaggerGen.Internal;

internal sealed partial class GrpcJsonTranscodingDescriptionProvider : IApiDescriptionProvider
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly DescriptorRegistry _descriptorRegistry;
    private readonly GrpcSwaggerOptions _options;

    public GrpcJsonTranscodingDescriptionProvider(EndpointDataSource endpointDataSource, DescriptorRegistry descriptorRegistry, IOptions<GrpcSwaggerOptions> options)
    {
        _endpointDataSource = endpointDataSource;
        _descriptorRegistry = descriptorRegistry;
        _options = options.Value;
    }

    // Executes after ASP.NET Core
    public int Order => -900;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
        var endpoints = _endpointDataSource.Endpoints;
        var addedDescriptions = new List<ApiDescription>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                var grpcMetadata = endpoint.Metadata.GetMetadata<GrpcJsonTranscodingMetadata>();

                if (grpcMetadata != null)
                {
                    var httpRule = grpcMetadata.HttpRule;
                    var methodDescriptor = grpcMetadata.MethodDescriptor;

                    if (ServiceDescriptorHelpers.TryResolvePattern(grpcMetadata.HttpRule, out var pattern, out var verb))
                    {
                        var apiDescription = CreateApiDescription(routeEndpoint, httpRule, methodDescriptor, pattern, verb);
                        context.Results.Add(apiDescription);
                        addedDescriptions.Add(apiDescription);

                        _descriptorRegistry.RegisterFileDescriptor(grpcMetadata.MethodDescriptor.File);
                    }
                }
            }
        }

        DetectPathConflicts(addedDescriptions);
    }

    private ApiDescription CreateApiDescription(RouteEndpoint routeEndpoint, HttpRule httpRule, MethodDescriptor methodDescriptor, string pattern, string verb)
    {
        var apiDescription = new ApiDescription();
        apiDescription.HttpMethod = verb;
        apiDescription.ActionDescriptor = new ActionDescriptor
        {
            RouteValues = new Dictionary<string, string?>
            {
                // Swagger uses this to group endpoints together.
                // Group methods together using the service name.
                ["controller"] = methodDescriptor.Service.Name
            },
            EndpointMetadata = routeEndpoint.Metadata.ToList()
        };
        apiDescription.SupportedRequestFormats.Add(new ApiRequestFormat { MediaType = "application/json" });

        var responseBodyDescriptor = ServiceDescriptorHelpers.ResolveResponseBodyDescriptor(httpRule.ResponseBody, methodDescriptor);
        var responseType = responseBodyDescriptor != null ? MessageDescriptorHelpers.ResolveFieldType(responseBodyDescriptor) : methodDescriptor.OutputType.ClrType;
        apiDescription.SupportedResponseTypes.Add(new ApiResponseType
        {
            ApiResponseFormats = { new ApiResponseFormat { MediaType = "application/json" } },
            ModelMetadata = new GrpcModelMetadata(ModelMetadataIdentity.ForType(responseType)),
            StatusCode = 200
        });
        apiDescription.SupportedResponseTypes.Add(new ApiResponseType
        {
            ApiResponseFormats = { new ApiResponseFormat { MediaType = "application/json" } },
            ModelMetadata = new GrpcModelMetadata(ModelMetadataIdentity.ForType(typeof(Google.Rpc.Status))),
            IsDefaultResponse = true
        });
        var explorerSettings = routeEndpoint.Metadata.GetMetadata<ApiExplorerSettingsAttribute>();
        if (explorerSettings != null)
        {
            apiDescription.GroupName = explorerSettings.GroupName;
        }

        var methodMetadata = routeEndpoint.Metadata.GetMetadata<GrpcMethodMetadata>()!;
        var httpRoutePattern = HttpRoutePattern.Parse(pattern);
        var routeParameters = ServiceDescriptorHelpers.ResolveRouteParameterDescriptors(httpRoutePattern.Variables, methodDescriptor.InputType);

        var defaultPath = ResolvePath(httpRoutePattern, routeParameters, _options.ExpandLiteralPathSegments);
        var defaultPathParameters = BuildDefaultPathParameters(httpRoutePattern, routeParameters, _options.ExpandLiteralPathSegments);

        // Apply ResolveOpenApiPath override if one is configured.
        var resolvedPath = defaultPath;
        IReadOnlyList<ApiParameterDescription> resolvedPathParameters = defaultPathParameters;

        if (_options.ResolveOpenApiPath is { } resolver)
        {
            var contextParameters = defaultPathParameters
                .Select(p => new GrpcSwaggerPathParameter { Name = p.Name, Field = GetFieldFromMetadata(p, routeParameters) })
                .Where(p => p.Field != null!)
                .ToList();

            var pathContext = new GrpcSwaggerPathContext
            {
                Method = methodDescriptor,
                HttpRule = httpRule,
                DefaultPath = defaultPath,
                DefaultParameters = contextParameters,
            };

            var resolution = resolver(pathContext);
            if (resolution != null)
            {
                resolvedPath = resolution.Path;
                resolvedPathParameters = resolution.Parameters != null
                    ? resolution.Parameters.Select(BuildApiParameterDescription).ToList()
                    : AutoExtractParameters(resolution.Path, routeParameters);
            }
        }

        apiDescription.RelativePath = resolvedPath;
        foreach (var parameter in resolvedPathParameters)
        {
            apiDescription.ParameterDescriptions.Add(parameter);
        }

        var bodyDescriptor = ServiceDescriptorHelpers.ResolveBodyDescriptor(httpRule.Body, methodMetadata.ServiceType, methodDescriptor);
        if (bodyDescriptor != null)
        {
            // If from a property, create model as property to get its XML comments.
            var identity = bodyDescriptor.PropertyInfo != null
                ? ModelMetadataIdentity.ForProperty(bodyDescriptor.PropertyInfo, bodyDescriptor.PropertyInfo.PropertyType, bodyDescriptor.PropertyInfo.DeclaringType!)
                : ModelMetadataIdentity.ForType(bodyDescriptor.Descriptor.ClrType);

            // Or if from a parameter, create model as parameter to get its XML comments.
            var parameterDescriptor = bodyDescriptor.ParameterInfo != null
                ? new ControllerParameterDescriptor { ParameterInfo = bodyDescriptor.ParameterInfo }
                : null;

            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Input",
                ModelMetadata = new GrpcModelMetadata(identity),
                Source = BindingSource.Body,
                ParameterDescriptor = parameterDescriptor!
            });
        }

        var queryParameters = ServiceDescriptorHelpers.ResolveQueryParameterDescriptors(routeParameters, methodDescriptor, bodyDescriptor?.Descriptor, bodyDescriptor?.FieldDescriptor);
        foreach (var queryDescription in queryParameters)
        {
            var field = queryDescription.Value;
            var propertyInfo = field.ContainingType.ClrType.GetProperty(field.PropertyName);

            // If from a property, create model as property to get its XML comments.
            var identity = propertyInfo != null
                ? ModelMetadataIdentity.ForProperty(propertyInfo, MessageDescriptorHelpers.ResolveFieldType(field), field.ContainingType.ClrType)
                : ModelMetadataIdentity.ForType(MessageDescriptorHelpers.ResolveFieldType(field));

            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = queryDescription.Key,
                ModelMetadata = new GrpcModelMetadata(identity),
                Source = BindingSource.Query,
                DefaultValue = string.Empty
            });
        }

        return apiDescription;
    }

    private static string ResolvePath(HttpRoutePattern httpRoutePattern, Dictionary<string, RouteParameter> routeParameters, bool expandLiteralPathSegments)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < httpRoutePattern.Segments.Count; i++)
        {
            if (sb.Length > 0)
            {
                sb.Append('/');
            }
            var routeParameter = routeParameters.SingleOrDefault(kvp => kvp.Value.RouteVariable.StartSegment == i).Value;
            if (routeParameter != null)
            {
                if (!expandLiteralPathSegments)
                {
                    // Legacy behavior: collapse the variable's entire span into a single {jsonPath} token.
                    sb.Append('{');
                    sb.Append(routeParameter.JsonPath);
                    sb.Append('}');
                }
                else
                {
                    // Expansion: emit each segment inside the variable's span, with one path parameter per wildcard.
                    var wildcardOccurrence = 0;
                    for (var j = routeParameter.RouteVariable.StartSegment; j < routeParameter.RouteVariable.EndSegment; j++)
                    {
                        if (j > routeParameter.RouteVariable.StartSegment)
                        {
                            sb.Append('/');
                        }
                        var segment = httpRoutePattern.Segments[j];
                        if (IsWildcardSegment(segment))
                        {
                            sb.Append('{');
                            sb.Append(routeParameter.JsonPath);
                            if (wildcardOccurrence > 0)
                            {
                                sb.Append(wildcardOccurrence);
                            }
                            sb.Append('}');
                            wildcardOccurrence++;
                        }
                        else
                        {
                            sb.Append(segment);
                        }
                    }
                }
                i = routeParameter.RouteVariable.EndSegment - 1;
            }
            else
            {
                sb.Append(httpRoutePattern.Segments[i]);
            }
        }
        if (httpRoutePattern.Verb != null)
        {
            sb.Append(':');
            sb.Append(httpRoutePattern.Verb);
        }
        return sb.ToString();
    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        // no-op
    }

    private static List<ApiParameterDescription> BuildDefaultPathParameters(
        HttpRoutePattern httpRoutePattern,
        Dictionary<string, RouteParameter> routeParameters,
        bool expandLiteralPathSegments)
    {
        var result = new List<ApiParameterDescription>();
        foreach (var routeParameter in routeParameters.Values)
        {
            var field = routeParameter.DescriptorsPath.Last();

            if (!expandLiteralPathSegments)
            {
                // Legacy: one parameter per variable, named by JsonPath.
                result.Add(BuildApiParameterDescription(routeParameter.JsonPath, field));
                continue;
            }

            // Expansion: one parameter per wildcard segment inside the variable's span,
            // suffixing with occurrence index for subsequent wildcards.
            var wildcardOccurrence = 0;
            for (var j = routeParameter.RouteVariable.StartSegment; j < routeParameter.RouteVariable.EndSegment; j++)
            {
                if (!IsWildcardSegment(httpRoutePattern.Segments[j]))
                {
                    continue;
                }
                var name = wildcardOccurrence == 0
                    ? routeParameter.JsonPath
                    : routeParameter.JsonPath + wildcardOccurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                result.Add(BuildApiParameterDescription(name, field));
                wildcardOccurrence++;
            }
        }
        return result;
    }

    private static ApiParameterDescription BuildApiParameterDescription(string name, FieldDescriptor field)
    {
        var parameterName = ServiceDescriptorHelpers.FormatUnderscoreName(field.Name, pascalCase: true, preservePeriod: false);
        var propertyInfo = field.ContainingType.ClrType.GetProperty(parameterName);

        // If from a property, create model as property to get its XML comments.
        var identity = propertyInfo != null
            ? ModelMetadataIdentity.ForProperty(propertyInfo, MessageDescriptorHelpers.ResolveFieldType(field), field.ContainingType.ClrType)
            : ModelMetadataIdentity.ForType(MessageDescriptorHelpers.ResolveFieldType(field));

        return new ApiParameterDescription
        {
            Name = name,
            ModelMetadata = new GrpcModelMetadata(identity),
            Source = BindingSource.Path,
            DefaultValue = string.Empty,
        };
    }

    private static ApiParameterDescription BuildApiParameterDescription(GrpcSwaggerPathParameter parameter)
        => BuildApiParameterDescription(parameter.Name, parameter.Field);

    private static List<ApiParameterDescription> AutoExtractParameters(
        string path,
        Dictionary<string, RouteParameter> routeParameters)
    {
        var result = new List<ApiParameterDescription>();
        foreach (Match match in PathTokenRegex().Matches(path))
        {
            var token = match.Groups[1].Value;

            // Step (i): exact name match against any known route variable.
            var routeParameter = routeParameters.Values.FirstOrDefault(p => p.JsonPath == token);

            if (routeParameter == null)
            {
                // Step (ii): strip trailing digits and retry.
                var stripped = StripTrailingDigits(token);
                if (stripped.Length != token.Length)
                {
                    routeParameter = routeParameters.Values.FirstOrDefault(p => p.JsonPath == stripped);
                }
            }

            if (routeParameter != null)
            {
                result.Add(BuildApiParameterDescription(token, routeParameter.DescriptorsPath.Last()));
            }
            else
            {
                // Step (iii): untyped string fallback.
                result.Add(new ApiParameterDescription
                {
                    Name = token,
                    ModelMetadata = new GrpcModelMetadata(ModelMetadataIdentity.ForType(typeof(string))),
                    Source = BindingSource.Path,
                    DefaultValue = string.Empty,
                });
            }
        }
        return result;
    }

    private static string StripTrailingDigits(string s)
    {
        var end = s.Length;
        while (end > 0 && char.IsDigit(s[end - 1]))
        {
            end--;
        }
        return s.Substring(0, end);
    }

    private static bool IsWildcardSegment(string segment) => segment == "*" || segment == "**";

    // Recover the FieldDescriptor behind a default ApiParameterDescription so we can pass it into
    // a GrpcSwaggerPathParameter for the resolver's DefaultParameters. The default param's name
    // may carry a numeric suffix (e.g. "name1"); strip it to find the corresponding route variable.
    private static FieldDescriptor GetFieldFromMetadata(ApiParameterDescription parameter, Dictionary<string, RouteParameter> routeParameters)
    {
        var match = routeParameters.Values.FirstOrDefault(p => p.JsonPath == parameter.Name);
        if (match == null)
        {
            var stripped = StripTrailingDigits(parameter.Name);
            match = routeParameters.Values.FirstOrDefault(p => p.JsonPath == stripped);
        }
        return match!.DescriptorsPath.Last();
    }

    private static void DetectPathConflicts(List<ApiDescription> addedDescriptions)
    {
        var groups = addedDescriptions
            .GroupBy(d => (d.HttpMethod, d.RelativePath))
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine("gRPC JSON transcoding produced conflicting OpenAPI paths:");
        foreach (var group in groups)
        {
            message.Append("  - ").Append(group.Key.HttpMethod).Append(' ').Append('/').AppendLine(group.Key.RelativePath);
            foreach (var apiDesc in group)
            {
                var metadata = apiDesc.ActionDescriptor.EndpointMetadata
                    .OfType<GrpcJsonTranscodingMetadata>()
                    .FirstOrDefault();
                if (metadata == null)
                {
                    continue;
                }
                var template = GetHttpRuleTemplate(metadata.HttpRule);
                message.Append("      • ").Append(metadata.MethodDescriptor.FullName).Append(" (template: ").Append(template).AppendLine(")");
            }
        }
        message.Append("Set GrpcSwaggerOptions.ExpandLiteralPathSegments = true and/or use GrpcSwaggerOptions.ResolveOpenApiPath to disambiguate.");
        throw new InvalidOperationException(message.ToString());
    }

    private static string GetHttpRuleTemplate(HttpRule rule) => rule.PatternCase switch
    {
        HttpRule.PatternOneofCase.Get => rule.Get,
        HttpRule.PatternOneofCase.Put => rule.Put,
        HttpRule.PatternOneofCase.Post => rule.Post,
        HttpRule.PatternOneofCase.Delete => rule.Delete,
        HttpRule.PatternOneofCase.Patch => rule.Patch,
        HttpRule.PatternOneofCase.Custom => rule.Custom.Path,
        _ => "<unknown>",
    };

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PathTokenRegex();
}
