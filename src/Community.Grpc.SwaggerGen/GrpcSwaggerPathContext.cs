// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Google.Api;
using Google.Protobuf.Reflection;

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// The context object passed to a <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/> callback,
/// exposing the gRPC method, its HTTP rule, and the default OpenAPI path the provider would
/// emit if no override were supplied.
/// </summary>
public sealed class GrpcSwaggerPathContext
{
    /// <summary>
    /// The Protobuf descriptor for the gRPC method whose OpenAPI path is being resolved.
    /// </summary>
    public required MethodDescriptor Method { get; init; }

    /// <summary>
    /// The Google API <see cref="Google.Api.HttpRule"/> attached to the gRPC method, including the raw HTTP binding template.
    /// </summary>
    public required HttpRule HttpRule { get; init; }

    /// <summary>
    /// The OpenAPI path the provider would emit if <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/> were not set.
    /// If expansion is enabled with <see cref="GrpcSwaggerOptions.ExpandLiteralPathSegments"/> this value contains the expanded path,
    /// otherwise it contains the path as set in the HttpRule
    /// </summary>
    public required string DefaultPath { get; init; }

    /// <summary>
    /// The OpenAPI parameter list the provider would emit if no override were supplied.
    /// Override callbacks can rename a subset of parameters by cloning entries with
    /// <c>with</c> expressions and returning the new list via
    /// <see cref="GrpcSwaggerPathResolution(string, IReadOnlyList{GrpcSwaggerPathParameter})"/>.
    /// </summary>
    public required IReadOnlyList<GrpcSwaggerPathParameter> DefaultParameters { get; init; }
}
