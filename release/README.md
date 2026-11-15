# Reusable WPF release module

Requires Node 22+ and the project's .NET SDK. No npm dependencies.

## Initialize

Copy release/ (excluding output/ and .env) into the project root. Copy .env.example to .env inside release/ and enter credentials and the project URL. Run once:

```sh
node release/scripts/init.mjs
```

This creates/updates npm commands, detects the WPF csproj and AssemblyName, and writes PROJECT in release/.env. Existing credentials, comments, custom URL and unrelated npm commands are preserved. Subsequent runs:

```sh
npm run wpf:init
npm run wpf:init -- --project App/App.csproj
```

A previously selected project is reused. Multiple WPF projects without a saved selection require --project. No library is installed. If Services/Updates/UpdateManifest.cs already exists with FeedUrl/ExecutableUrl constants, init updates those URLs.

## Project settings

release/.env is the source of product name and URL:

```dotenv
PROJECT=MyApp
STORAGE_PUBLIC_URL=https://cdn.example.com/my-app/
```

The executable is PROJECT.exe. The public download directory and R2 key prefix append downloads/ to the URL path. The config JSON stores only the csproj path (relative to release/), runtime RID and default notes. Changing the product URL should be followed by wpf:init to synchronize an existing updater contract.

## Commands

- npm run build: creates the single-file distribution and latest.json in release/output using the csproj version; no increment, remote lookup or upload.
- npm run deploy: reads current latest.json, increments patch and rebuilds. On 404 uses the csproj version unchanged for the first publication. Other errors abort.
- npm run deploy -- --notes "Changes": overrides the default notes.
- npm run deploy -- --dry-run: computes the release version without build/upload.
- npm run show: displays published version, notes, download URL and hash; no credentials needed.
- npm run test:deploy: simulated-network and initializer tests, without uploads.

Deploy validates hash, rechecks the remote version and uploads EXE before JSON. Do not run concurrent deployments: the recheck is not a distributed lock. Published apps require the matching .NET Desktop Runtime.

## Credentials

Use release/.env (excluded from Git), with STORAGE_ENDPOINT_API, STORAGE_BUCKET_PUBLIC, STORAGE_ACCESS_KEY_ID and STORAGE_SECRET_ACCESS_KEY. The endpoint is the HTTPS S3 endpoint of R2 without bucket/path. Secrets are never printed or included in the EXE. Output is ignored by the module's .gitignore.

The module has no legacy feeds, version migrations or compatibility fallback.
