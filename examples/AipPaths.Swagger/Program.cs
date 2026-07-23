using AipPaths;
using Community.Grpc.SwaggerGen;
using Grpc.Core;

var builder = WebApplication.CreateBuilder(args);

// The Library service uses AIP-131 resource names: GetShelf binds
// "/v1/{name=shelves/*}" and GetBook binds "/v1/{name=shelves/*/books/*}".
// With default rendering, both collapse to "GET /v1/{name}" and document
// generation fails with a path-conflict error.
builder.Services.AddGrpcSwagger(options =>
{
    // Render the literal segments hidden inside each binding, so the paths
    // become /v1/shelves/{name} and /v1/shelves/{name}/books/{name1}.
    options.ExpandLiteralPathSegments = true;

    // Optional: rename the auto-generated wildcard parameters ({name}, {name1})
    // to friendlier names, per RPC. The renamed parameters keep the schema and
    // XML-comment metadata of the proto field they came from.
    options.ResolveOpenApiPath = GrpcSwaggerPathResolver.Build(paths =>
    {
        paths.Add<Library.LibraryBase>(nameof(Library.LibraryBase.GetBook))
            .RenameParameter("name", "shelfId")
            .RenameParameter("name1", "bookId");

        paths.Add<Library.LibraryBase>(nameof(Library.LibraryBase.ListBooks))
            .RenameParameter("parent", "shelfId");
    });
});
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGrpcService<LibraryService>();

app.MapGet("/", () => "Open /swagger to explore the API, or try GET /v1/shelves/1/books/2");

app.Run();


public class LibraryService : Library.LibraryBase
{
    public override Task<Shelf> GetShelf(GetShelfRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Shelf
        {
            Name = request.Name,
            Theme = "Science Fiction"
        });
    }

    public override Task<Book> GetBook(GetBookRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Book
        {
            Name = request.Name,
            Title = "The Hitchhiker's Guide to the Galaxy"
        });
    }

    public override Task<ListBooksResponse> ListBooks(ListBooksRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ListBooksResponse
        {
            Books =
            {
                new Book { Name = $"{request.Parent}/books/1", Title = "Dune" },
                new Book { Name = $"{request.Parent}/books/2", Title = "Foundation" }
            }
        });
    }
}
