# Pruebas Postman / Newman para saas-billing

1. Levantar `saas-billing`:

```powershell
cd saas-billing
docker compose up -d --build
```

2. Ejecutar la colección automatizada contra el stack Docker vivo:

```powershell
docker run --rm --network saas-billing_default `
  -v "${PWD}/tests/postman:/etc/newman" `
  postman/newman:alpine run /etc/newman/Vital.Billing.postman_collection.json `
  --env-var "baseUrl=http://billing-api:8080"
```

Nota: como las hojas `COBRADA` y los cortes `CONFIRMADO` son inmutables por regla de negocio (`CU-20 A5`, `CU-22 A4`), el flujo completo sobre `Enc0104_1` se ejecuta limpio en CI (sobre PostgreSQL efímero) o tras `docker compose down -v && docker compose up -d --build`.
