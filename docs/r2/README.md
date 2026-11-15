# Publicación manual en R2

- `CallMonitorLatest.json` se genera desde `call-monitor/scripts/publish-production.mjs`, con el hash del ejecutable final.
- Sube primero `CallMonitor.exe` y, cuando termine, este JSON.
- Destinos: `https://cdn.frankgp.com/call/CallMonitor.exe` y `https://cdn.frankgp.com/call/CallMonitorLatest.json`.
- Evita caché prolongada en ambas rutas: `Cache-Control: no-store, max-age=0`, sin reglas que lo sobrescriban.
- La primera versión con actualizador se instala manualmente. A partir de ella, se ofrecerán únicamente versiones superiores.
- Si recompilas o firmas el ejecutable, regenera el hash antes de subirlo. No reutilices el de una compilación anterior.
- No guardes credenciales de R2 en esta carpeta ni dentro de la app.
