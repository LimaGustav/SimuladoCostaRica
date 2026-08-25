using Microsoft.AspNetCore.Mvc;
using ResultsApi.Models;

namespace ResultsApi.Controllers;

[ApiController]
[Route("api/test-results")]
public sealed class TestResultsController : ControllerBase
{
    private readonly ITestResultStore _store;

    public TestResultsController(ITestResultStore store) => _store = store;
    /// <summary>Recebe o resultado de um teste automatizado. Ainda não o persiste nem o processa.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Receive([FromBody] TestResultRequest result)
    {
        // Ponto de extensão futuro: persistir, encaminhar a uma fila ou atualizar um dashboard.
        if (string.IsNullOrWhiteSpace(result.TestName)) return BadRequest("TestName is required.");
        _store.Save(result);
        return Accepted();
    }

    [ProducesResponseType(typeof(IReadOnlyDictionary<string, TestResultRequest>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyDictionary<string, TestResultRequest>> GetTestResults() => Ok(_store.GetAll());
}
