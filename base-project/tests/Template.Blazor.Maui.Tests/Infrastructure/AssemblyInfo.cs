// Run test classes sequentially. xUnit parallelises across classes by default, and this suite
// mutates state that is process-wide: ThemeStore and PreferencesStore are static classes, and
// ThemeStore.IsDark broadcasts on the process-wide WeakReferenceMessenger.Default.
//
// To re-enable parallelism, give the theme and preference tests their own [Collection].
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
