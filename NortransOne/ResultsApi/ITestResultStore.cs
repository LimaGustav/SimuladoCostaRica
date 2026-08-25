using ResultsApi.Models;

namespace ResultsApi;

public interface ITestResultStore
{
    void Save(TestResultRequest result);
    IReadOnlyDictionary<string, TestResultRequest> GetAll();
}
