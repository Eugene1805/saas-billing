# saas-billing

Microservicio de **Billing, Cobros, Cortes de Especialista, Ajustes Compensatorios y KPIs Administrativos (`CU-20` a `CU-23`, `PC-05`, `CU-16`)** para la plataforma Vital SaaS, construido con **.NET 10**, **Arquitectura Hexagonal (Ports & Adapters)** y **PostgreSQL 17**.

## Arquitectura Hexagonal

```text
src/
  Saas.Billing.Domain/          # Entidades (ChargeSheet, PatientPayment, SpecialistCut, BillingAdjustment), Value Objects, reloj estricto GMT-6 y políticas de dominio
  Saas.Billing.Application/     # Puertos de entrada/salida y casos de uso (hojas de cargo, cobros, cortes, ajustes, métricas CU-16)
  Saas.Billing.Infrastructure/  # EF Core 10 + Npgsql multitenant (esquema `billing`), resolución CNPG por tenant, JWT Bearer y adaptadores locales
  Saas.Billing.Api/             # Minimal APIs REST v1, manejo 503 de BD por tenant, OpenAPI (`/openapi/v1.json`), Scalar (`/scalar/v1`) y probes K8s (`/health/live`, `/health/ready`)
tests/
  Saas.Billing.UnitTests/       # Pruebas unitarias xUnit de dominio, reloj GMT-6, políticas, casos de uso y resolución CNPG (14 pruebas)
  Saas.Billing.IntegrationTests/# Pruebas multi-PostgreSQL 17 con Testcontainers / BDs reales y aislamiento físico entre clínicas
  postman/                      # Colección Newman/Postman E2E (36 requests) + plantilla de entorno local

## Aislamiento por clínica (ADR-0002 Alternativa B)

Cada clínica opera contra su propia instancia PostgreSQL 17 (`vital_billing_<clinic>`) con el esquema privado `billing`, compartiendo el proceso `.NET 10` sin mezclar registros entre tenants ni guardar credenciales en el repositorio:

- **Kubernetes / CloudNativePG:** `TenantConnectionResolver` lee secretos montados en `TenantDatabases:SecretsDirectory` bajo cualquiera de estas convenciones: `<tenantId>/uri`, `<tenantId>/{host,port,dbname,username,password,sslmode}` o `<tenantId>.connection`.
- **Docker Compose / Pruebas:** se configura `TenantDatabases__Connections__<tenantId>`. Si la BD de un tenant no existe o es inalcanzable, la API falla cerrado con HTTP `503 Service Unavailable`.

## Inicio rápido con Docker Compose

```bash
cp .env.example .env
# Define contraseñas locales y JWT_SIGNING_KEY (misma clave que saas-identity) en .env
docker compose up -d --build
```

Servicios locales expuestos únicamente en loopback (`127.0.0.1`):
- **API Billing (`vital-billing-api`):** `http://127.0.0.1:8084` (puerto interno `8080`, usuario no root `1654`)
- **PostgreSQL Norte (`vital-billing-norte-db`):** `127.0.0.1:5435` (`vital_billing_norte`, esquema `billing`)
- **PostgreSQL Valle (`vital-billing-valle-db`):** `127.0.0.1:5443` (`vital_billing_valle`, esquema `billing`)
- **PostgreSQL Sur (`vital-billing-sur-db`):** `127.0.0.1:5444` (`vital_billing_sur`, esquema `billing`)
- **OpenAPI v1:** `http://127.0.0.1:8084/openapi/v1.json`
- **Scalar UI (Development / `OPENAPI_ENABLED=true`):** `http://127.0.0.1:8084/scalar/v1`

## Endpoints principales (Incremento 1)

- **Salud y Kubernetes:** `GET /health`, `GET /health/live`, `GET /health/ready`
- **Catálogo (`CU-20`):** `GET /v1/catalog/concepts`
- **Hojas de cargo (`CU-20`):** `GET /v1/charge-sheets`, `GET /v1/charge-sheets/{chargeSheetId}`, `PUT /v1/encounters/{encounterId}/charge-sheet`, `POST /v1/charge-sheets/{chargeSheetId}/review`, `POST /v1/charge-sheets/{chargeSheetId}/reopen`
- **Cobros de paciente (`CU-21`, `PC-05`):** `GET /v1/payments`, `POST /v1/payments`
- **Cortes de especialista (`CU-22`, `CU-23`):** `GET /v1/cuts`, `GET /v1/cuts/{cutId}`, `POST /v1/cuts`, `PUT /v1/cuts/{cutId}`, `POST /v1/cuts/{cutId}/confirm`, `POST /v1/cuts/{cutId}/observe`
- **Ajustes compensatorios vinculados:** `GET /v1/adjustments`, `POST /v1/adjustments`
- **KPIs administrativos (`CU-16`):** `GET /v1/metrics/clinic/payments`, `GET /v1/metrics/clinic/cuts`

## Pruebas locales y CI

- **Build y pruebas unitarias xUnit dentro de Docker:**
  ```bash
  docker build --target test -t vital-billing-test .
  ```
- **Pruebas de integración multi-PostgreSQL (Testcontainers o `TEST_TENANT_A_DB_CS` / `TEST_TENANT_B_DB_CS`):**
  ```bash
  dotnet test tests/Saas.Billing.IntegrationTests/Saas.Billing.IntegrationTests.csproj -c Release
  ```
- **Colección Postman / Newman contra el stack local (la colección exige pasar `--env-var "jwtSigningKey=..."` real):**
  ```bash
  docker run --rm --network saas-billing_default -v "${PWD}/tests/postman:/etc/newman:ro" postman/newman:alpine \
    run /etc/newman/Vital.Billing.postman_collection.json \
    --env-var "baseUrl=http://billing-api:8080" \
    --env-var "jwtSigningKey=TU_CLAVE_JWT_DE_ENV"
  ```
- **GitHub Actions (`.github/workflows/ci.yml`):** `build-and-test` restaura, compila, ejecuta xUnit e IntegrationTests multi-PostgreSQL (.NET 10); `docker-build` levanta 2 instancias `postgres:17-alpine` con secretos efímeros y el contenedor endurecido (`--cap-drop=ALL`, `--security-opt=no-new-privileges:true`), valida `/health/live`, `/health/ready`, `/health`, `/openapi/v1.json`, Newman (`36/36`) y verifica aislamiento SQL (`VALLE_SHEETS=0`). En `main` publica `ghcr.io/eugene1805/saas-billing:<sha>`.
