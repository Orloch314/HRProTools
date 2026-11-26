param([string]$ReleaseId = ('local-' + (Get-Date -Format 'yyyyMMddTHHmmss')), [switch]$RequireClean)
$ErrorActionPreference = 'Stop'
if ($ReleaseId -notmatch '^[A-Za-z0-9._-]+$') { throw 'Invalid release identifier' }
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskRoot "artifacts/production/$ReleaseId"
if (Test-Path -LiteralPath $taskOutput) { throw 'Choose a new release id; existing artifacts are not overwritten' }
New-Item -ItemType Directory -Path $taskOutput | Out-Null
Push-Location $taskRoot
try {
 $taskCommit = git rev-parse HEAD
 $taskSourceChanges = @(git status --porcelain --untracked-files=all -- src site deploy tests docs .gitignore .gitattributes)
 $taskTrackedChanges = @(git status --porcelain --untracked-files=no)
 $taskDirty = $taskSourceChanges.Count -gt 0 -or $taskTrackedChanges.Count -gt 0
 if ($RequireClean -and $taskDirty) { throw 'Commit all source changes before creating a deployable release' }
 dotnet publish src/HRProTools.Web/HRProTools.Web.csproj -c Release -r linux-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o "$taskOutput/web"
 if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed' }
 Push-Location site
 try {
  $env:PUBLIC_APP_LOGIN_URL = 'https://app.hrprotools.com/Account/Login'
  npm.cmd run build
  if ($LASTEXITCODE -ne 0) { throw 'Astro build failed' }
  npm.cmd run verify
  if ($LASTEXITCODE -ne 0) { throw 'Astro verification failed' }
 } finally { Pop-Location }
 Copy-Item -LiteralPath site/dist -Destination "$taskOutput/site" -Recurse
 New-Item -ItemType Directory "$taskOutput/deploy","$taskOutput/migrations" | Out-Null
 Get-ChildItem deploy/production -File | Where-Object Extension -in '.py','.sh','.conf','.service' | Copy-Item -Destination "$taskOutput/deploy"
 dotnet ef migrations script --context ApplicationDbContext --project src/HRProTools.Web --configuration Release --idempotent -o "$taskOutput/migrations/global.sql"
 if ($LASTEXITCODE -ne 0) { throw 'Global migration script generation failed' }
 dotnet ef migrations script --context TenantDbContext --project src/HRProTools.Web --configuration Release --idempotent -o "$taskOutput/migrations/tenant-template.sql"
 if ($LASTEXITCODE -ne 0) { throw 'Tenant migration script generation failed' }
 if ($RequireClean -and ((git rev-parse HEAD) -ne $taskCommit -or @(git status --porcelain --untracked-files=all -- src site deploy tests docs .gitignore .gitattributes).Count -gt 0 -or @(git status --porcelain --untracked-files=no).Count -gt 0)) { throw 'Source changed during build; release cannot be deployed' }
 $taskVersion = [ordered]@{ release=$ReleaseId; source_commit=$taskCommit; uncommitted_changes=$taskDirty; runtime='linux-x64 self-contained'; prepared_utc=(Get-Date).ToUniversalTime().ToString('o'); remote_deployed=$false }
 $taskVersion | ConvertTo-Json | Set-Content -LiteralPath "$taskOutput/version.json" -Encoding utf8NoBOM
 python deploy/package_release.py "$taskOutput"
 if ($LASTEXITCODE -ne 0) { throw 'Artifact inspection/packaging failed' }
 Write-Output "Prepared locally: $taskOutput"
} finally { Pop-Location }
