// =====================================================================================
//  Nortrans Reference API
//  Organiser infrastructure for the Skill 09 competition simulation.
//
//  This service is consumed by Module 1 (consignee master data) and Module 5 (yard,
//  containers and movements). It is NEVER given to the competitors as source code:
//  it would show them how an ASP.NET Core API is built, which is what Module 4 asks
//  them to do themselves.
//
//  Everything lives in this one file on purpose: the organiser must be able to read it
//  and fix it during a competition day without opening a solution.
// =====================================================================================

using System.Data;
using System.Text.Json;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

var connectionString = Environment.GetEnvironmentVariable("NORTRANS_DB")
    ?? "Host=reference-db;Port=5432;Database=nortrans_ref;Username=nortrans;Password=Nortrans2026!";
var apiKey = Environment.GetEnvironmentVariable("NORTRANS_API_KEY") ?? "ws09-nortrans-2026";

// ------------------------------------------------------------------ helpers
async Task<NpgsqlConnection> OpenAsync()
{
    var c = new NpgsqlConnection(connectionString);
    await c.OpenAsync();
    return c;
}

static object Cell(object v) => v is DBNull ? null! : v;

async Task<List<Dictionary<string, object>>> QueryAsync(string sql, params (string, object?)[] ps)
{
    await using var conn = await OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    await using var r = await cmd.ExecuteReaderAsync();
    var rows = new List<Dictionary<string, object>>();
    while (await r.ReadAsync())
    {
        var row = new Dictionary<string, object>();
        for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = Cell(r.GetValue(i));
        rows.Add(row);
    }
    return rows;
}

// The API key guard applies to /reference/* only. /health and /swagger stay open so that
// the organiser can check the service without a key.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/reference"))
    {
        var supplied = ctx.Request.Headers["X-Api-Key"].ToString();
        if (!string.Equals(supplied, apiKey, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Missing or wrong X-Api-Key header." });
            return;
        }
    }
    await next();
});

// ------------------------------------------------------------------ health
app.MapGet("/health", async () =>
{
    try
    {
        await using var c = await OpenAsync();
        return Results.Ok(new { status = "ok", database = "up" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "degraded", database = "down", detail = ex.Message }, statusCode: 503);
    }
});

// ------------------------------------------------------------------ consignees (Module 1)
// Deliberately served raw, defects included: this is the spreadsheet export the module has
// to validate. Do not clean it here.
app.MapGet("/reference/consignees", async () =>
{
    var rows = await QueryAsync(
        @"SELECT legal_name AS ""legalName"", trading_name AS ""tradingName"", tax_id AS ""taxId"",
                 country_code AS ""countryCode"", city, address_line AS ""addressLine"",
                 email, phone, incoterm, credit_limit_cents AS ""creditLimitCents"", active
          FROM consignees ORDER BY id");
    return Results.Ok(rows);
});

// ------------------------------------------------------------------ branches
app.MapGet("/reference/branches", async () =>
    Results.Ok(await QueryAsync("SELECT id, code, name, city, phone FROM branches ORDER BY code")));

// ------------------------------------------------------------------ containers (Module 5)
app.MapGet("/reference/containers", async (string? branch) =>
{
    const string sql =
        @"SELECT container_no AS ""containerNo"", size_type AS ""sizeType"", teu,
                 seal_no AS ""sealNo"", bill_of_lading AS ""billOfLading"",
                 consignee_legal_name AS ""consigneeLegalName"",
                 current_branch_code AS ""currentBranchCode"", status,
                 last_movement_at AS ""lastMovementAt""
          FROM containers
          WHERE (@branch IS NULL OR UPPER(current_branch_code) = UPPER(@branch))
          ORDER BY container_no";
    return Results.Ok(await QueryAsync(sql, ("branch", (object?)branch)));
});

app.MapGet("/reference/containers/{containerNo}", async (string containerNo) =>
{
    var rows = await QueryAsync(
        @"SELECT container_no AS ""containerNo"", size_type AS ""sizeType"", teu,
                 seal_no AS ""sealNo"", bill_of_lading AS ""billOfLading"",
                 consignee_legal_name AS ""consigneeLegalName"",
                 current_branch_code AS ""currentBranchCode"", status,
                 last_movement_at AS ""lastMovementAt""
          FROM containers WHERE UPPER(container_no) = UPPER(@no)", ("no", containerNo));
    return rows.Count == 0 ? Results.NotFound(new { error = "Container not found." }) : Results.Ok(rows[0]);
});

// ------------------------------------------------------------------ movements (Module 5)
string[] movementTypes = ["Gate In", "Gate Out", "Stripped", "Stuffed", "Damaged", "Reweighed"];

app.MapGet("/reference/containers/{containerNo}/movements", async (string containerNo, string? branch) =>
{
    const string sql =
        @"SELECT id, container_no AS ""containerNo"", branch_code AS ""branchCode"",
                 movement_type AS ""movementType"", occurred_at AS ""occurredAt"", note
          FROM movements
          WHERE UPPER(container_no) = UPPER(@no)
            AND (@branch IS NULL OR UPPER(branch_code) = UPPER(@branch))
          ORDER BY occurred_at DESC, id DESC";
    return Results.Ok(await QueryAsync(sql, ("no", containerNo), ("branch", (object?)branch)));
});

app.MapGet("/reference/branches/{code}/movements", async (string code) =>
{
    const string sql =
        @"SELECT m.id, m.container_no AS ""containerNo"", m.branch_code AS ""branchCode"",
                 m.movement_type AS ""movementType"", m.occurred_at AS ""occurredAt"", m.note,
                 c.consignee_legal_name AS ""consigneeLegalName""
          FROM movements m LEFT JOIN containers c ON c.container_no = m.container_no
          WHERE UPPER(m.branch_code) = UPPER(@code)
          ORDER BY m.occurred_at DESC, m.id DESC";
    return Results.Ok(await QueryAsync(sql, ("code", code)));
});

app.MapPost("/reference/containers/{containerNo}/movements", async (string containerNo, JsonElement body) =>
{
    string? Str(string name) =>
        body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    var branchCode = Str("branchCode");
    var movementType = Str("movementType");
    var occurredRaw = Str("occurredAt");
    var note = Str("note") ?? "";

    if (string.IsNullOrWhiteSpace(branchCode) || string.IsNullOrWhiteSpace(movementType))
        return Results.BadRequest(new { error = "branchCode and movementType are required." });
    if (!movementTypes.Contains(movementType, StringComparer.Ordinal))
        return Results.BadRequest(new { error = $"movementType must be one of: {string.Join(", ", movementTypes)}." });

    var occurredAt = DateTime.UtcNow;
    if (!string.IsNullOrWhiteSpace(occurredRaw) &&
        !DateTime.TryParse(occurredRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out occurredAt))
        return Results.BadRequest(new { error = "occurredAt must be an ISO 8601 instant." });

    var exists = await QueryAsync("SELECT container_no FROM containers WHERE UPPER(container_no) = UPPER(@no)",
                                  ("no", containerNo));
    if (exists.Count == 0) return Results.NotFound(new { error = "Container not found." });

    var branch = await QueryAsync("SELECT code FROM branches WHERE UPPER(code) = UPPER(@c)", ("c", branchCode));
    if (branch.Count == 0) return Results.NotFound(new { error = "Branch not found." });

    await using var conn = await OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    await using var ins = new NpgsqlCommand(
        @"INSERT INTO movements (container_no, branch_code, movement_type, occurred_at, note)
          VALUES (UPPER(@no), UPPER(@br), @mt, @at, @note)
          RETURNING id, container_no AS ""containerNo"", branch_code AS ""branchCode"",
                    movement_type AS ""movementType"", occurred_at AS ""occurredAt"", note", conn, tx);
    ins.Parameters.AddWithValue("no", containerNo);
    ins.Parameters.AddWithValue("br", branchCode!);
    ins.Parameters.AddWithValue("mt", movementType!);
    ins.Parameters.AddWithValue("at", occurredAt);
    ins.Parameters.AddWithValue("note", note);

    Dictionary<string, object> created;
    await using (var r = await ins.ExecuteReaderAsync())
    {
        await r.ReadAsync();
        created = new Dictionary<string, object>();
        for (var i = 0; i < r.FieldCount; i++) created[r.GetName(i)] = Cell(r.GetValue(i));
    }

    await using (var upd = new NpgsqlCommand(
        @"UPDATE containers
             SET status = CASE WHEN @mt = 'Gate Out' THEN 'Gated Out' ELSE 'In Yard' END,
                 current_branch_code = UPPER(@br),
                 last_movement_at = @at
           WHERE UPPER(container_no) = UPPER(@no)", conn, tx))
    {
        upd.Parameters.AddWithValue("mt", movementType!);
        upd.Parameters.AddWithValue("br", branchCode!);
        upd.Parameters.AddWithValue("at", occurredAt);
        upd.Parameters.AddWithValue("no", containerNo);
        await upd.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    return Results.Created($"/reference/movements/{created["id"]}", created);
});

// Undo: deletes a movement and recomputes the container's status from what is left.
app.MapDelete("/reference/movements/{id:int}", async (int id) =>
{
    await using var conn = await OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    await using var del = new NpgsqlCommand(
        "DELETE FROM movements WHERE id = @id RETURNING container_no", conn, tx);
    del.Parameters.AddWithValue("id", id);
    var containerNo = (string?)await del.ExecuteScalarAsync();
    if (containerNo is null) return Results.NotFound(new { error = "Movement not found." });

    await using (var upd = new NpgsqlCommand(
        @"UPDATE containers c
             SET status = COALESCE((SELECT CASE WHEN m.movement_type = 'Gate Out'
                                                THEN 'Gated Out' ELSE 'In Yard' END
                                      FROM movements m WHERE m.container_no = c.container_no
                                     ORDER BY m.occurred_at DESC, m.id DESC LIMIT 1), 'In Transit'),
                 last_movement_at = (SELECT MAX(m.occurred_at) FROM movements m
                                      WHERE m.container_no = c.container_no)
           WHERE c.container_no = @no", conn, tx))
    {
        upd.Parameters.AddWithValue("no", containerNo);
        await upd.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    return Results.NoContent();
});

app.Run("http://0.0.0.0:8080");
