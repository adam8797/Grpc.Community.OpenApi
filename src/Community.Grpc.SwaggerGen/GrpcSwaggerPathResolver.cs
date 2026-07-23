// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// Builds a path-resolver function from declarative per-RPC override rules.
/// </summary>
/// <remarks>
/// <para>
/// Assign the returned function to <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/>. The
/// resolver looks up each transcoded gRPC method by its
/// <see cref="Google.Protobuf.Reflection.MethodDescriptor.FullName"/>; if a matching rule was
/// registered, the rule's path and renames are applied. Methods without a matching rule
/// receive <see langword="null"/>, so the provider falls back to its default path.
/// </para>
/// </remarks>
public static class GrpcSwaggerPathResolver
{
    /// <summary>
    /// Build a path-resolver function from a set of declarative override rules.
    /// </summary>
    /// <param name="configure">Callback that registers rules on a <see cref="GrpcSwaggerPathOverrideBuilder"/>.</param>
    /// <returns>A function suitable for assignment to <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/>.</returns>
    public static Func<GrpcSwaggerPathContext, GrpcSwaggerPathResolution?> Build(Action<GrpcSwaggerPathOverrideBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new GrpcSwaggerPathOverrideBuilder();
        configure(builder);
        return builder.BuildResolver();
    }
}
