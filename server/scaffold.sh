#!/bin/bash
set -a
source .env
set +a

dotnet ef dbcontext scaffold "$CONN_STR" \
  Npgsql.EntityFrameworkCore.PostgreSQL \
  --project ./dataaccess/dataaccess.csproj \
  --startup-project ./api/api.csproj \
  --output-dir ./Entities \
  --context-dir . \
  --context MyDbContext \
  --no-onconfiguring \
  --namespace DataAccess.Entities \
  --context-namespace DataAccess \
  --force
