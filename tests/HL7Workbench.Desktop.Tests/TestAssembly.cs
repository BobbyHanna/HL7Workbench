using Xunit;

// Avalonia's dispatcher and editor keymap are process-wide services. Keep model and
// headless UI tests from initializing them concurrently on different threads.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
