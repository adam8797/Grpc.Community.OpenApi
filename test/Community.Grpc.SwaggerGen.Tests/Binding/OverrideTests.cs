// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Community.Grpc.SwaggerGen.Tests.Infrastructure;

namespace Community.Grpc.SwaggerGen.Tests.Binding;

public class OverrideTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public OverrideTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Override_StringFqn_PathLess_AppliesRenameToDefault()
    {
        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o =>
            {
                o.ExpandLiteralPathSegments = true;
                o.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(p =>
                {
                    p.Add("ThingService.GetThing").RenameParameter("name", "thingId");
                });
            },
            typeof(ConcreteThingService));

        Assert.True(swagger.Paths.ContainsKey("/v1/things/{thingId}"));
        var op = swagger.Paths["/v1/things/{thingId}"].Operations[HttpMethod.Get];
        Assert.Single(op.Parameters);
        Assert.Equal("thingId", op.Parameters[0].Name);
        Assert.Equal(ParameterLocation.Path, op.Parameters[0].In);
    }

    [Fact]
    public void Override_StringFqn_WithExplicitPath_AndRenames()
    {
        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o =>
            {
                o.ExpandLiteralPathSegments = true;
                o.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(p =>
                {
                    p.Add("ThingService.GetChildThing", "/v1/things/{thingId}/children/{childId}")
                        .RenameParameter("name", "thingId")
                        .RenameParameter("name1", "childId");
                });
            },
            typeof(ConcreteThingService));

        Assert.True(swagger.Paths.ContainsKey("/v1/things/{thingId}/children/{childId}"));
        var op = swagger.Paths["/v1/things/{thingId}/children/{childId}"].Operations[HttpMethod.Get];
        Assert.Equal(2, op.Parameters.Count);
        Assert.Equal("thingId", op.Parameters[0].Name);
        Assert.Equal("childId", op.Parameters[1].Name);
    }

    [Fact]
    public void Override_GenericNameof_ResolvesViaDescriptor()
    {
        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o =>
            {
                o.ExpandLiteralPathSegments = true;
                o.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(p =>
                {
                    p.Add<ThingService.ThingServiceBase>(nameof(ThingService.ThingServiceBase.GetThing))
                        .RenameParameter("name", "thingId");
                });
            },
            typeof(ConcreteThingService));

        Assert.True(swagger.Paths.ContainsKey("/v1/things/{thingId}"));
    }

    [Fact]
    public void Override_DuplicateFqn_ThrowsAtBuild()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GrpcSwaggerPathResolver.Build(p =>
            {
                p.Add("ThingService.GetThing");
                p.Add("ThingService.GetThing");
            }));

        Assert.Contains("already been registered", ex.Message);
        Assert.Contains("ThingService.GetThing", ex.Message);
    }

    [Fact]
    public void Override_UnmatchedFqn_IsSilentlyIgnored()
    {
        // A rule for a method that doesn't exist should produce no error and leave the
        // swagger document unchanged (matches the "no extra validation" design decision).
        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o =>
            {
                o.ExpandLiteralPathSegments = true;
                o.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(p =>
                {
                    p.Add("ThingService.DoesNotExist", "/v1/never-applied").RenameParameter("name", "ignored");
                });
            },
            typeof(ConcreteThingService));

        // The override doesn't fire — default expansion produces /v1/things/{name} for GetThing
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}"));
        Assert.False(swagger.Paths.ContainsKey("/v1/never-applied"));
    }

    [Fact]
    public void Override_ComposesWithFallbackResolver()
    {
        // The helper handles one method; a fallback resolver handles a different one.
        var helper = GrpcSwaggerPathResolver.Build(p =>
        {
            p.Add("ThingService.GetThing").RenameParameter("name", "thingId");
        });

        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o =>
            {
                o.ExpandLiteralPathSegments = true;
                o.ResolveOpenApiPath = ctx => helper(ctx)
                    ?? (ctx.Method.Name == "ListThings"
                        ? new GrpcSwaggerPathResolution("/v1/list-things/{name}")
                        : null);
            },
            typeof(ConcreteThingService));

        // helper rule handled GetThing
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{thingId}"));
        // fallback resolver handled ListThings
        Assert.True(swagger.Paths.ContainsKey("/v1/list-things/{name}"));
        // unhandled RPCs use the default-expanded path
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}/children/{name1}"));
    }
}
