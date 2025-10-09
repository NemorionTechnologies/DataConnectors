using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Data.Repositories;

namespace DataWorkflows.Engine.Controllers;

[ApiController]
[Route("api/v1/executions")]
public class ExecutionsController : ControllerBase
{
    private readonly IConfiguration _config;

    public ExecutionsController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExecution(Guid id)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var repo = new WorkflowExecutionRepository(connectionString);
        var execution = await repo.GetById(id);

        return execution != null ? Ok(execution) : NotFound();
    }
}
