using Xunit;

// WebApplicationFactory<Program> boots the real host via reflection on the entry point
// (HostFactoryResolver), which is not safe for concurrent use across test classes — running
// two factories at once intermittently fails with "entry point exited without ever building an
// IHost". Each test class already gets its own isolated in-memory SQLite database (see
// TrainCoachApiFactory), so sequential execution only costs wall-clock time, not correctness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
