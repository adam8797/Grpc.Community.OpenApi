// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Community.Grpc.SwaggerGen;

/// <summary>
/// Options used to configure the OpenAPI document generated for gRPC JSON transcoding endpoints.
/// </summary>
public sealed class GrpcSwaggerOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether literal segments inside an HTTP rule binding template are rendered into the generated OpenAPI path.
    /// <para>
    /// When <see langword="false"/> a binding such as <c>/v1/{name=foo/*}</c> is rendered as <c>/v1/{name}</c>: the literal <c>foo</c>
    /// prefix is collapsed into the variable. AIP-131-style services that bind multiple resources through the same field name will collide on
    /// identical paths and cause Swagger generation to fail.
    /// </para>
    /// <para>
    /// When <see langword="true"/>, the same binding renders as <c>/v1/foo/{name}</c>, and a multi-wildcard binding such as
    /// <c>/v1/{name=foo/*/bar/*}</c> renders as <c>/v1/foo/{name}/bar/{name1}</c> with one OpenAPI parameter per wildcard slot.
    /// </para>
    /// </summary>
    public bool ExpandLiteralPathSegments { get; set; }

    /// <summary>
    /// Gets or sets a callback that is invoked once per transcoded gRPC method to override the OpenAPI path the provider would otherwise emit.
    /// <para>
    /// Return <see langword="null"/> to fall back to the default.
    /// Return a <see cref="GrpcSwaggerPathResolution"/> to override the path (and optionally the parameter list).
    /// </para>
    /// <para>
    /// The callback fires regardless of <see cref="ExpandLiteralPathSegments"/>; the flag only changes what <see cref="GrpcSwaggerPathContext.DefaultPath"/> contains.
    /// </para>
    /// <para>
    /// Can be used in conjunction with <see cref="GrpcSwaggerPathResolver"/> to easily re-write paths</para>
    /// </summary>
    public Func<GrpcSwaggerPathContext, GrpcSwaggerPathResolution?>? ResolveOpenApiPath { get; set; }
}
