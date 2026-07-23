// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Google.Protobuf.Reflection;

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// Builder object passed to <see cref="GrpcSwaggerPathResolver.Build(System.Action{GrpcSwaggerPathOverrideBuilder})"/>
/// </summary>
public sealed class GrpcSwaggerPathOverrideBuilder
{
    private readonly Dictionary<string, GrpcSwaggerPathOverride> _byFullName = new(StringComparer.Ordinal);

    internal GrpcSwaggerPathOverrideBuilder() { }

    /// <inheritdoc cref="Add(string, string)"/>
    public GrpcSwaggerPathOverride Add(string methodFullName)
        => AddCore(methodFullName, path: null);

    /// <summary>
    /// Add an override keyed by the proto fully-qualified method name (matched against <see cref="MethodDescriptor.FullName"/>).
    /// </summary>
    /// <param name="methodFullName">The fully-qualified Protobuf method name, e.g. <c>"package.ServiceName.MethodName"</c> or <c>"ServiceName.MethodName"</c> when the proto has no package.</param>
    /// <param name="path">The explicit OpenAPI path template (e.g. <c>"/v1/things/{thingId}"</c>).</param>
    /// <returns>The created <see cref="GrpcSwaggerPathOverride"/> for fluent configuration.</returns>
    public GrpcSwaggerPathOverride Add(string methodFullName, string path)
        => AddCore(methodFullName, path);

    /// <inheritdoc cref="Add{TService}(string, string)"/>
    public GrpcSwaggerPathOverride Add<TService>(string methodName) where TService : class
        => AddCore(ResolveFullName(typeof(TService), methodName), path: null);

    /// <summary>
    /// Add an override keyed by a gRPC service base type and a method name. The service descriptor is resolved from the type's generated static <c>Descriptor</c> property.
    /// </summary>
    /// <typeparam name="TService">The generated gRPC service base type (e.g. <c>MyService.MyServiceBase</c>).</typeparam>
    /// <param name="methodName">The method name within <typeparamref name="TService"/>.</param>
    /// <param name="path">The explicit OpenAPI path template.</param>
    /// <returns>The created <see cref="GrpcSwaggerPathOverride"/> for fluent configuration.</returns>
    public GrpcSwaggerPathOverride Add<TService>(string methodName, string path) where TService : class
        => AddCore(ResolveFullName(typeof(TService), methodName), path);

    internal Func<GrpcSwaggerPathContext, GrpcSwaggerPathResolution?> BuildResolver()
    {
        return ctx => _byFullName.TryGetValue(ctx.Method.FullName, out var rule) ? rule.Apply(ctx) : null;
    }

    private GrpcSwaggerPathOverride AddCore(string methodFullName, string? path)
    {
        ArgumentException.ThrowIfNullOrEmpty(methodFullName);

        if (_byFullName.ContainsKey(methodFullName))
        {
            throw new InvalidOperationException(
                $"An override has already been registered for method '{methodFullName}'.");
        }

        var rule = new GrpcSwaggerPathOverride(path);
        _byFullName[methodFullName] = rule;
        return rule;
    }

    // Resolve a proto MethodDescriptor.FullName from a gRPC service base type and a C# method
    // name. gRPC-generated service base classes expose a static `Descriptor` property of type
    // ServiceDescriptor that lets us locate the matching proto method.
    private static string ResolveFullName(Type serviceType, string methodName)
    {
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        var descriptorProperty = serviceType.GetProperty(
            "Descriptor",
            BindingFlags.Public | BindingFlags.Static);

        // gRPC service base classes are nested inside an outer class (e.g. ThingService.ThingServiceBase)
        // whose outer type carries the static Descriptor. Walk up if the immediate type lacks it.
        var lookupType = serviceType;
        while (descriptorProperty == null && lookupType.DeclaringType != null)
        {
            lookupType = lookupType.DeclaringType;
            descriptorProperty = lookupType.GetProperty(
                "Descriptor",
                BindingFlags.Public | BindingFlags.Static);
        }

        if (descriptorProperty == null || descriptorProperty.PropertyType != typeof(ServiceDescriptor))
        {
            throw new InvalidOperationException(
                $"Type '{serviceType.FullName}' does not expose a static 'Descriptor' property of type ServiceDescriptor. " +
                $"Add<TService>(...) requires a gRPC-generated service base type.");
        }

        var serviceDescriptor = (ServiceDescriptor?)descriptorProperty.GetValue(null);
        if (serviceDescriptor == null)
        {
            throw new InvalidOperationException(
                $"Type '{serviceType.FullName}'.Descriptor returned null.");
        }

        var methodDescriptor = serviceDescriptor.FindMethodByName(methodName);
        if (methodDescriptor == null)
        {
            throw new InvalidOperationException(
                $"Service '{serviceDescriptor.FullName}' does not declare a method named '{methodName}'.");
        }

        return methodDescriptor.FullName;
    }
}
