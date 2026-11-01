# Taskly Backend (.NET)

A task manager backend: sign up and sign in, manage your profile and avatar, and create, filter, sort and paginate to-do tasks. It is a REST API built with ASP.NET Core and PostgreSQL, organised as a modular monolith with Domain-Driven Design and Clean Architecture. It is a .NET port of the Spring Boot and FastAPI versions and keeps the same HTTP contract.

The browser client is a separate Taskly frontend that talks to this API.

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · Npgsql · PostgreSQL 18 · versioned SQL migrations · JWT (Microsoft.IdentityModel) · bcrypt · xUnit · NetArchTest · Docker · GitHub Actions

## Contents

- [Quick start](#quick-start)
- [Features](#features)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Database](#database)
- [API](#api)
- [Testing](#testing)
- [CI](#ci)
- [Limits](#limits)
- [Migrating from the Spring Boot and FastAPI versions](#migrating-from-the-spring-boot-and-fastapi-versions)
- [References](#references)

## Quick start

Requires the .NET 10 SDK and Docker.

```sh
cd taskly-docker
make docker/dev-up
cd ../taskly-backend
make dev
```

`make dev` creates `.env` from `.env.example` with a generated `AUTH_SECRET` on the first run, then starts the API. The API runs at http://localhost:8080 and pgAdmin at http://localhost:8081 (login `admin@taskly.dev` / `admin`, server host `taskly-postgres`, user `dev_user`, password `dev_pass`). Migrations run automatically on startup. Values in `.env` are read from the working directory, so run the commands from `taskly-backend`. Regular environment variables take precedence over `.env`.

| Goal               | Command                                                  |
| ------------------ | -------------------------------------------------------- |
| Run in development | `make dev`                                               |
| Build              | `make build`                                             |
| Run a release build| `make run`                                               |
| Seed demo data     | `make seed`                                              |
| Run all tests      | `make test`                                              |
| Run with Docker    | `make docker/up` (from `taskly-docker`)                  |
| Seed in Docker     | `make docker/seed` (from `taskly-docker`)                |
| Stop Docker        | `make docker/down` (from `taskly-docker`)                |

`make docker/up` creates `taskly-docker/.env` with a random database password and `AUTH_SECRET` on the first run, then starts PostgreSQL and the API and waits until both are healthy. `PORT` overrides the host port in the Docker setup. The container always listens on port 8080. Demo login after seeding: `johndoe123@mail.com` / `password123`.

## Features

- **Authentication**: sign up, sign in and sign out with a JWT stored in an HTTP-only cookie.
- **Users**: list users, update the profile, upload an avatar and delete the account.
- **Tasks**: create, view, update and delete tasks.
- **Listing**: filter by status, sort and paginate tasks.
- **Platform**: versioned SQL migrations, request logging, graceful shutdown, health checks on `/` and `/health`.

## Configuration

| Variable            | Default                 | Purpose                                              |
| ------------------- | ----------------------- | ---------------------------------------------------- |
| `APP_NAME`          | `taskly-api`            | Application name                                     |
| `APP_HOST`          | `0.0.0.0`               | Bind address                                         |
| `APP_PORT`          | `8080`                  | Port the API listens on                              |
| `APP_LEVEL`         | `development`           | Environment level, `production` hardens the cookie   |
| `DATABASE_URL`      | required                | `postgres://user:password@host:5432/db`, an Npgsql connection string is accepted too |
| `DATABASE_POOL_MAX` | `10`                    | Maximum connections in the pool                      |
| `DATABASE_TIMEOUT`  | `30000`                 | Connection timeout in milliseconds                   |
| `AUTH_SECRET`       | required                | JWT secret, at least 32 characters                   |
| `COOKIE_DOMAIN`     | empty                   | Domain of the auth cookie in production              |
| `BCRYPT_ROUNDS`     | `10`                    | bcrypt cost factor, 4 to 31                          |
| `CLIENT_URL`        | `http://localhost:3000` | Allowed CORS origin                                  |
| `LOG_LEVEL`         | `debug`                 | Log level of the Taskly code, `info` in production   |
| `PUBLIC_DIR`        | `public`                | Directory served as static files, holds the avatars  |
| `SEED_ALLOW_PRODUCTION` | `false`             | Allows seeding when `APP_LEVEL` is `production`      |

Docker only, in `taskly-docker/.env`:

| Variable      | Default  | Purpose                                 |
| ------------- | -------- | --------------------------------------- |
| `PORT`        | `8080`   | Host port used by Docker                |
| `DB_NAME`     | `taskly` | PostgreSQL database name                |
| `DB_USER`     | `taskly` | PostgreSQL user                         |
| `DB_PASSWORD` | required | PostgreSQL password, generated by make |

The frontend must set `VITE_API_BASE_URL=http://localhost:8080/api/v1` and `VITE_SERVER_BASE_URL=http://localhost:8080`.

## Architecture

### Project layout

```
taskly-backend/
  src/
    Taskly.Host/                    composition root: configuration, pipeline, seed command
    Taskly.SharedKernel/            errors, ids, event bus, HTTP and persistence helpers
    Modules/
      Users/Taskly.Modules.Users/   authentication and user management
      Tasks/Taskly.Modules.Tasks/   task management
  tests/
    Taskly.UnitTests/               domain, use cases, JWT, bcrypt, file storage, configuration
    Taskly.IntegrationTests/        full HTTP flows and migrations on a real PostgreSQL
    Taskly.ArchitectureTests/       layer and module boundary rules
  public/images/users/              static avatars
taskly-docker/
  taskly-backend/Dockerfile
  docker-compose.local.yml
  docker-compose.dev.yml
  Makefile
.github/workflows/ci.yml
```

### Module layout

```
Taskly.Modules.<Module>/
  Domain/          entities, value objects, repository and service contracts
  Application/     use cases
  Infrastructure/  Npgsql, JWT, bcrypt and file system adapters, SQL migrations
  Presentation/    Minimal API endpoints
  <Module>Module.cs  dependency injection and endpoint registration
```

### Dependency rules

- Dependencies point inward: presentation and infrastructure depend on application, application depends on domain.
- Modules never reference each other. They communicate through the in-process event bus, so deleting a user publishes `UserDeleted` and the tasks module removes that user's tasks.
- Each module owns its tables, its SQL migrations and its endpoints. The host only wires modules together.
- Authentication is cross-cutting: endpoints are marked with `RequireAccessToken()`, a middleware in the shared kernel asks the registered `IRequestAuthenticator` (implemented by the users module with the cookie) and exposes the user id to endpoints as `AuthenticatedUser`.
- `Taskly.ArchitectureTests` fails the build when a layer or a module crosses these boundaries.

## Database

Schema changes live in each module as `Infrastructure/Persistence/Migrations/<number>_<name>.sql`, embedded in the assembly. `SqlMigrator` collects the scripts of all modules, sorts them by name and applies the pending ones on startup under a PostgreSQL advisory lock, one transaction per script. Applied names are recorded in `schema_migrations`.

| Table   | Notes                                                                                   |
| ------- | --------------------------------------------------------------------------------------- |
| `users` | `id` UUID, case-insensitive unique `email` and `username`, `password_hash`, `avatar_file`, timestamps |
| `tasks` | `id` UUID, `owner_id` UUID (foreign key to `users`), `due` timestamptz, `status` and `priority` limited by `CHECK`, indexed on `(owner_id, status)` |

- Identifiers are UUIDs. API responses expose them as `_id` and `owner`.
- Listings are ordered by `created_at` and `id`, so ties on the creation time still give a stable order.
- `tasks.owner_id` references `users(id)` with `ON DELETE CASCADE`. The `UserDeleted` event still removes a user's tasks.
- `status` accepts `todo`, `in_progress` and `done` (default `todo`). `priority` accepts `low`, `medium` and `high`.
- `due` is stored as a timestamp with time zone and returned as an ISO 8601 UTC string, for example `2026-12-08T10:00:00.000Z`. Inputs may be an ISO 8601 date-time with or without an offset, a plain date, or an RFC 1123 date. Values without an offset are read as UTC.

## API

All routes are under `/api/v1`. Routes other than sign up and sign in require the `taskly_token` cookie.

| Method | Path                | Purpose                                         |
| ------ | ------------------- | ----------------------------------------------- |
| POST   | `/auth/signup`      | Register and sign in                            |
| POST   | `/auth/signin`      | Sign in                                         |
| GET    | `/auth/signout`     | Sign out                                        |
| GET    | `/users`            | List users, filter with `username` and `email`  |
| GET    | `/users/{id}`       | Get a user                                      |
| PATCH  | `/users/update/{id}`| Update own profile                              |
| POST   | `/users/upload/{id}`| Upload own avatar (`avatar` field, 2 MB)        |
| DELETE | `/users/delete/{id}`| Delete own account                              |
| POST   | `/tasks/create`     | Create a task                                   |
| GET    | `/tasks/user/{id}`  | List own tasks with `status`, `orderBy`, `page` |
| GET    | `/tasks/{id}`       | Get a task                                      |
| PATCH  | `/tasks/{id}`       | Update a task                                   |
| DELETE | `/tasks/{id}`       | Delete a task                                   |

Errors are returned as `{ "status": <code>, "message": "<text>" }`. Unknown routes return `{ "message": "Not found" }` with status 404. Tasks are listed four per page.

## Testing

| Layer        | Command                | What it proves                                                  |
| ------------ | ---------------------- | --------------------------------------------------------------- |
| Unit         | `make test/unit`       | Domain, use cases, JWT, bcrypt, file storage and configuration, no I/O |
| Architecture | `make test/architecture` | Layers and modules respect the dependency rules               |
| Integration  | `make test/integration`| Full HTTP flows and migrations on a real PostgreSQL             |

Integration tests need a PostgreSQL server where the user can create databases. They read `TEST_DATABASE_URL` and default to the development database from `make docker/dev-up`. The suite creates its own temporary databases, runs the migrations and drops them when it finishes.

```sh
TEST_DATABASE_URL=postgres://postgres:postgres@localhost:5432/postgres dotnet test tests/Taskly.IntegrationTests
```

## CI

`.github/workflows/ci.yml` runs these jobs:

1. **Commitlint**: commit message linting on pull requests, configured in `.commitlintrc.json`.
2. **Build**: restores and compiles the solution with the .NET 10 SDK.
3. **Tests**: unit, architecture and integration suites against a PostgreSQL 18 service.
4. **Docker**: builds the image, starts the Compose stack, then checks `/health`, signs up a user and runs the seed.

## Limits

- The API needs a running PostgreSQL.
- Uploaded avatars are stored on local disk and served from `/images/users`. The Compose stack keeps them in the `taskly-avatars` volume.
- Events are delivered in-process, so the app runs as a single instance.
- Logs go to the console only. Collect them from the container output.
- Access tokens are stateless, so a deleted user's token stays valid until it expires. Creating a task with it is rejected with `404 User not found!`.

## Migrating from the Spring Boot and FastAPI versions

- `DATABASE_URL` is a `postgres://` URL with the credentials inside, as in the FastAPI version. `DATABASE_USER` and `DATABASE_PASSWORD` no longer exist.
- `LOG_FILE` no longer exists. Logs are written to the console.
- The health endpoint `/actuator/health` is now `/health`. `/` still answers.
- Existing sessions stay valid: tokens use the same HS256 secret, payload and cookie name, and bcrypt hashes are compatible.
- A database created by the FastAPI version is picked up as is: the migration names and the `schema_migrations` table are identical.
- A database created by the Spring Boot version has its history in `flyway_schema_history`. Record the two migrations once before the first start:

```sql
CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now());
INSERT INTO schema_migrations (name) VALUES ('0001_create_users.sql'), ('0002_create_tasks.sql') ON CONFLICT DO NOTHING;
```

## References

- [ASP.NET Core](https://learn.microsoft.com/aspnet/core)
- [Npgsql](https://www.npgsql.org/doc/)
- [PostgreSQL](https://www.postgresql.org/docs/18/)
- [Microsoft.IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)
- [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net)
- [xUnit](https://xunit.net/)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [Docker multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
