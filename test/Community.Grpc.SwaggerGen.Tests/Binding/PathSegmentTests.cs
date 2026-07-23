// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Community.Grpc.SwaggerGen.Tests.Infrastructure;

namespace Community.Grpc.SwaggerGen.Tests.Binding;

public class ConcreteThingService : ThingService.ThingServiceBase { }

public class ConcreteWidgetService : WidgetService.WidgetServiceBase { }

public class PathSegmentTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public PathSegmentTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ConflictingPaths_DifferentLiteralSegments_GenerateUniquePaths()
    {
        var swagger = OpenApiTestHelpers.GetOpenApiDocument(
            _testOutputHelper,
            configureOptions: o => o.ExpandLiteralPathSegments = true,
            typeof(ConcreteThingService), typeof(ConcreteWidgetService));

        // Assert - Should have 6 distinct paths
        Assert.Equal(6, swagger.Paths.Count);

        // Path 1: /v1/widgets/{name}
        Assert.True(swagger.Paths.ContainsKey("/v1/widgets/{name}"));
        var widgetPath = swagger.Paths["/v1/widgets/{name}"];
        Assert.True(widgetPath.Operations.TryGetValue(HttpMethod.Get, out var widgetOperation));
        Assert.Single(widgetOperation.Parameters);
        Assert.Equal(ParameterLocation.Path, widgetOperation.Parameters[0].In);
        Assert.Equal("name", widgetOperation.Parameters[0].Name);

        // Path 2: /v1/things/{name}
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}"));
        var thingPath = swagger.Paths["/v1/things/{name}"];
        Assert.True(thingPath.Operations.TryGetValue(HttpMethod.Get, out var thingOperation));
        Assert.Single(thingOperation.Parameters);
        Assert.Equal(ParameterLocation.Path, thingOperation.Parameters[0].In);
        Assert.Equal("name", thingOperation.Parameters[0].Name);

        // Path 3: /v1/things/{name}/children/{name1}
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}/children/{name1}"));
        var childThingPath = swagger.Paths["/v1/things/{name}/children/{name1}"];
        Assert.True(childThingPath.Operations.TryGetValue(HttpMethod.Get, out var childThingOperation));
        Assert.Equal(2, childThingOperation.Parameters.Count);
        Assert.Equal(ParameterLocation.Path, childThingOperation.Parameters[0].In);
        Assert.Equal("name", childThingOperation.Parameters[0].Name);
        Assert.Equal(ParameterLocation.Path, childThingOperation.Parameters[1].In);
        Assert.Equal("name1", childThingOperation.Parameters[1].Name);

        // Path 4: /v1/things/{name}:extraAction
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}:extraAction"));
        var extraThingPath = swagger.Paths["/v1/things/{name}:extraAction"];
        Assert.True(extraThingPath.Operations.TryGetValue(HttpMethod.Get, out var extraThingOperation));
        Assert.Single(extraThingOperation.Parameters);
        Assert.Equal(ParameterLocation.Path, extraThingOperation.Parameters[0].In);
        Assert.Equal("name", extraThingOperation.Parameters[0].Name);

        // Path 5: /v1/things/{name}/children/{name1}:extraAction
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}/children/{name1}:extraAction"));
        var extraChildThingPath = swagger.Paths["/v1/things/{name}/children/{name1}:extraAction"];
        Assert.True(extraChildThingPath.Operations.TryGetValue(HttpMethod.Get, out var extraChildThingOperation));
        Assert.Equal(2, extraChildThingOperation.Parameters.Count);
        Assert.Equal(ParameterLocation.Path, extraChildThingOperation.Parameters[0].In);
        Assert.Equal("name", extraChildThingOperation.Parameters[0].Name);
        Assert.Equal(ParameterLocation.Path, extraChildThingOperation.Parameters[1].In);
        Assert.Equal("name1", extraChildThingOperation.Parameters[1].Name);

        // Path 6: /v1/things/{name}/children
        Assert.True(swagger.Paths.ContainsKey("/v1/things/{name}/children"));
        var listThingsPath = swagger.Paths["/v1/things/{name}/children"];
        Assert.True(listThingsPath.Operations.TryGetValue(HttpMethod.Get, out var listThingsOperation));
        Assert.Single(listThingsOperation.Parameters);
        Assert.Equal(ParameterLocation.Path, listThingsOperation.Parameters[0].In);
        Assert.Equal("name", listThingsOperation.Parameters[0].Name);
    }
}
