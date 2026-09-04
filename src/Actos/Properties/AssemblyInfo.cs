using System.Runtime.CompilerServices;

// Allow the test project to construct resources via their internal transport-carrying
// constructors (resource ctors stay idiomatic-internal; only the piecemeal test assembly
// reaches inside). See TestHarness.
[assembly: InternalsVisibleTo("Actos.Tests")]