using System.Collections.Concurrent;
using ResultsApi.Models;

namespace ResultsApi;

public sealed class InMemoryTestResultStore : ITestResultStore
{
    private readonly ConcurrentDictionary<string, TestResultRequest> _results = new(StringComparer.Ordinal);

    public void Save(TestResultRequest result) => _results[result.TestName] = result;

    public IReadOnlyDictionary<string, TestResultRequest> GetAll() => _results
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
