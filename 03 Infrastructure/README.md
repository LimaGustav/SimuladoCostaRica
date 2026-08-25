# Infrastructure — Nortrans Skill 09 competition simulation

**Organiser material. Never give this folder to a competitor:** it contains the full source of an
ASP.NET Core REST API, and module 4 asks them to write one.

## What is here

```
03 Infrastructure/
├── docker-compose.yml          everything, in one file
├── reference-api/              Nortrans Reference API — ASP.NET Core 8 + Npgsql
│   ├── Program.cs              the whole service, one file on purpose
│   ├── ReferenceApi.csproj
│   └── Dockerfile
└── db/
    ├── reference/              schema and seed of the Reference API database
    │   ├── 01_schema.sql
    │   └── 02_seed.sql
    └── trackandtrace/          schema and seed of the module 4 database
        ├── 01_schema.sql
        └── 02_seed.sql
```

## Bring it up

```bash
docker compose up -d
docker compose ps
```

| Service | Port | Used by |
|---|---|---|
| `gitea` | 3001 | delivery of every module |
| `reference-api` | 5080 | modules 1 and 5 |
| `reference-db` | 5432 | the Reference API |
| `tt-db` | 5433 | module 4 |

The two PostgreSQL containers run everything in their `docker-entrypoint-initdb.d` folder the
first time their volume is created — that is how the schema and the seed get in. If you change a
`.sql` file, the container will not pick it up until the volume is recreated
(`docker compose down -v`) or you run the file by hand with `psql`.

## Check it

```bash
curl http://localhost:5080/health
curl -H "X-Api-Key: ws09-nortrans-2026" http://localhost:5080/reference/branches
curl -H "X-Api-Key: ws09-nortrans-2026" http://localhost:5080/reference/containers?branch=CAL
psql "postgresql://nortrans:Nortrans2026!@localhost:5433/nortrans_tt" -c "\dt"
```

Interactive documentation: `http://localhost:5080/swagger`.

## The Reference API

An intentionally small service: one file, raw SQL through Npgsql, no ORM, no migrations. The
organiser has to be able to read it and fix it during a competition day.

| Method and path | Purpose | Consumed by |
|---|---|---|
| `GET /health` | liveness, and whether the database answers | organiser |
| `GET /reference/consignees` | the raw consignee master data, **defects included** | module 1 |
| `GET /reference/branches` | the four Nortrans sites | modules 1 and 5 |
| `GET /reference/containers` | every container; `?branch=CAL` filters | module 5 |
| `GET /reference/containers/{no}` | one container, or 404 | module 5 |
| `GET /reference/containers/{no}/movements` | movements of one container; `?branch=` filters | module 5 |
| `GET /reference/branches/{code}/movements` | every movement at a branch, most recent first | module 5 |
| `POST /reference/containers/{no}/movements` | records a movement and updates the container | module 5 |
| `DELETE /reference/movements/{id}` | deletes a movement and recomputes the status — this is Undo | module 5 |

Everything under `/reference` requires the header `X-Api-Key: ws09-nortrans-2026`. `/health` and
`/swagger` are open, so that you can check the service without a key.

Both are configurable through the environment, in `docker-compose.yml`:
`NORTRANS_API_KEY` and `NORTRANS_DB`.

### The consignee data is dirty on purpose

`GET /reference/consignees` serves sixty records straight out of the table, and nine of them are
defective: a malformed tax ID, an unknown country code, a broken e-mail address, an unknown
Incoterm, a negative credit limit, a name that is empty, a name that is far too long, a tax ID
that is too short, a credit limit written as `1.250,00`, and one tax ID duplicated inside the
batch. Module 1 is the module that has to catch them.

Which rows and which defects: `02 Organizer/Module 1 - Only for Marking/expected-rejections.csv`.

## Deploying it on a server instead of a laptop

The compose file works unchanged on any Docker host. Three things to change if you put it on a
real server:

1. **Publish only what you need.** The competitors need `3001` (Gitea), `5080` (Reference API)
   and `5433` (module 4 database). `5432` can stay internal — nothing outside the compose network
   uses it.
2. **Change the passwords.** `Nortrans2026!` and the API key `ws09-nortrans-2026` are printed in
   the competitor instructions; that is fine on a closed training network and not fine on the
   open internet.
3. **Set `GITEA__server__ROOT_URL`** to the server's real address, otherwise the clone URLs Gitea
   shows will say `localhost` and the competitors will copy them.

If the server is reachable by name, replace `localhost` with that name in
`00 General/Competitor Instructions.md` before printing it, and in the module 5 document.

## One Reference API per competitor

By default every competitor writes movements into the same database, and they see each other's
movements. To isolate them, copy the compose file per competitor and shift the published ports
(`5081:8080`, `5082:8080`, …) with a separate volume for each `reference-db`. Then give each one
their own base URL.
