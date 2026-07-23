// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Google.Protobuf.Reflection;

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// Describes a single OpenAPI path parameter emitted by a custom
/// <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/> callback. The parameter's documentation
/// metadata (schema type, XML comments) is sourced from the supplied <see cref="Field"/>.
/// </summary>
public sealed record GrpcSwaggerPathParameter
{
    /// <summary>
    /// The name of the path parameter as it should appear in the OpenAPI path template
    /// (without surrounding braces). For example, <c>"thingId"</c> for a path token <c>{thingId}</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Protobuf field that backs this parameter. Used to populate the OpenAPI schema type
    /// and to resolve XML documentation comments. Multiple <see cref="GrpcSwaggerPathParameter"/>
    /// instances may reference the same field when one logical Protobuf field is rendered as
    /// multiple OpenAPI parameters (e.g. for a multi-wildcard path template).
    /// </summary>
    public required FieldDescriptor Field { get; init; }
}
