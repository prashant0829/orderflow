using Xunit;

// Collections run one after another because the services read settings from process-wide environment variables while starting up.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
