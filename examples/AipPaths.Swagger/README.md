# AipPaths

Demonstrates **Community.Grpc.SwaggerGen** with [AIP-131](https://google.aip.dev/131)-style
resource names, where multiple RPCs bind their route through the same `name` field:

```proto
rpc GetShelf (GetShelfRequest) returns (Shelf) {
  option (google.api.http) = { get: "/v1/{name=shelves/*}" };
}
rpc GetBook (GetBookRequest) returns (Book) {
  option (google.api.http) = { get: "/v1/{name=shelves/*/books/*}" };
}
```

With default rendering both of these collapse to `GET /v1/{name}` — the literal
`shelves`/`books` segments disappear into the variable — so the generated OpenAPI
document has two operations on one path and generation fails with a conflict error.

## The fix

`Program.cs` turns on literal-segment expansion, and (optionally) renames the
auto-generated wildcard parameters per RPC:

```csharp
builder.Services.AddGrpcSwagger(options =>
{
    // /v1/{name=shelves/*/books/*} renders as /v1/shelves/{name}/books/{name1}
    options.ExpandLiteralPathSegments = true;

    // Optional: rename {name}/{name1} to friendlier tokens
    options.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(paths =>
    {
        paths.Add<Library.LibraryBase>(nameof(Library.LibraryBase.GetBook))
            .RenameParameter("name", "shelfId")
            .RenameParameter("name1", "bookId");
    });
});
```

The resulting document contains three distinct paths:

| RPC | OpenAPI path |
| --- | --- |
| `GetShelf` | `/v1/shelves/{name}` |
| `GetBook` | `/v1/shelves/{shelfId}/books/{bookId}` |
| `ListBooks` | `/v1/shelves/{shelfId}/books` |

## Run it

```bash
# From the repo root:
dotnet build
dotnet run --project examples/AipPaths.Swagger
```

Then navigate to [http://localhost:65415/swagger](http://localhost:65415/swagger) to see the Swagger UI.

## Try it

```bash
curl http://localhost:65415/v1/shelves/1/books/2
# {"name":"shelves/1/books/2","title":"The Hitchhiker's Guide to the Galaxy"}

curl http://localhost:65415/v1/shelves/1/books
# {"books":[{"name":"shelves/1/books/1","title":"Dune"}, ...]}
```
