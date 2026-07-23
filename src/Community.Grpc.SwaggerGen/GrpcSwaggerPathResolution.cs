// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// The result returned by a <see cref="GrpcSwaggerOptions.ResolveOpenApiPath"/> callback to
/// override the OpenAPI path emitted for a transcoded gRPC method.
/// </summary>
public sealed class GrpcSwaggerPathResolution
{
    /// <summary>
    /// Creates a resolution that overrides only the path string. The provider derives the
    /// OpenAPI parameter list automatically by matching each <c>{token}</c> in <paramref name="path"/>
    /// against the route's known variables: exact name match first, then with trailing digits
    /// stripped (so <c>{name1}</c> falls back to the <c>name</c> field). Tokens that match no
    /// variable produce an untyped string parameter.
    /// </summary>
    /// <param name="path">The OpenAPI path template to emit (for example, <c>/v1/things/{name}</c>).</param>
    public GrpcSwaggerPathResolution(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Path = path;
        Parameters = null;
    }

    /// <summary>
    /// Creates a resolution that overrides both the path string and the OpenAPI parameter list.
    /// Use this overload when you need to rename parameters while preserving their schema and
    /// documentation metadata. Each token in <paramref name="path"/> should have a corresponding
    /// entry in <paramref name="parameters"/>.
    /// </summary>
    /// <param name="path">The OpenAPI path template to emit.</param>
    /// <param name="parameters">The OpenAPI parameter list, in document order.</param>
    public GrpcSwaggerPathResolution(string path, IReadOnlyList<GrpcSwaggerPathParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(parameters);

        Path = path;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the overriding OpenAPI path template.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the overriding OpenAPI parameter list, or <see langword="null"/> when only the path
    /// is overridden and the provider should derive parameters automatically.
    /// </summary>
    public IReadOnlyList<GrpcSwaggerPathParameter>? Parameters { get; }
}
