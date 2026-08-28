// Run test classes sequentially. xUnit parallelises across classes by default, and this suite
// mutates state that is process-wide:
//
//   - template.api registers HarnessFaultInjector as a SINGLETON on purpose, so an injected
//     fault survives across requests. Two WebApplicationFactory-based classes running concurrently
//     would consume each other's injected failures.
//   - the API suite shares one in-memory SQLite database per class, held open by WeatherApiFactory
//     for the lifetime of the factory.
//
// To re-enable parallelism, first move the harness suites into a single non-parallel [Collection].
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
