```sh
https://cdn.frankgp.com/call/CallMonitor.exe

# Get-FileHash -Algorithm SHA256 -LiteralPath "E:\proyects\call-project\call-monitor\CallMonitor\bin\Production\Release\net10.0-windows\win-x64\publish\CallMonitor.exe"
https://cdn.frankgp.com/call/CallMonitorLatest.json

```

```sh
# Añade -- --demo para probar el diseño sin backend.
dotnet run --project CallMonitor/CallMonitor.csproj -- --demo

dotnet run --project E:\proyects\call-project\call-monitor\CallMonitor

# Desarrollo
dotnet run --project CallMonitor/CallMonitor.csproj -p:AppEnvironment=Development

# QA
dotnet publish CallMonitor/CallMonitor.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:AppEnvironment=QA
# E:\proyects\call-project\call-monitor\CallMonitor\bin\QA\Release\net10.0-windows\win-x64\publish

# Producción
dotnet publish CallMonitor/CallMonitor.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:AppEnvironment=Production
# E:\proyects\call-project\call-monitor\CallMonitor\bin\Production\Release\net10.0-windows\win-x64\publish

# Para generar futuras versiones y su JSON:
node scripts/publish-production.mjs --version 1.0.4 --notes "Mejoras y correcciones."

```

```sh
cd /e/proyects/call-project/call-backend
npm --prefix ../call-dashboard run build
cp -r ../call-dashboard/dist/. public/

```
