docker compose up -d (Redis local)

set REDIS_CONNECTION for cloud

dotnet run (server)

npm run dev (client, later)

endpoints: /swagger, /sse, /messages



FOR MIGRATION
PowerShell
$env:ASPNETCORE_ENVIRONMENT="Migration"
dotnet ef migrations add InitialCreate --project ./dataaccess --startup-project ./api
dotnet ef database update --project ./dataaccess --startup-project ./api

Git Bash (your terminal)
export ASPNETCORE_ENVIRONMENT=Migration
dotnet ef migrations add InitialCreate --project ./dataaccess --startup-project ./api
dotnet ef database update --project ./dataaccess --startup-project ./api


After you’re done, you can unset it:

unset ASPNETCORE_ENVIRONMENT