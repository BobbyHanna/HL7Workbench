using Xunit;

// The generated attachment measurements require stable per-process GC accounting.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
