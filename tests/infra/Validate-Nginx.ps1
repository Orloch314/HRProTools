$ErrorActionPreference='Stop'
$taskRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskFolder=Join-Path $taskRoot ('artifacts/nginx-validation/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory "$taskFolder/tls","$taskFolder/acme","$taskFolder/live/hrprotools.com","$taskFolder/live/app.hrprotools.com" | Out-Null
$taskOpenSsl='C:\Program Files\Git\usr\bin\openssl.exe'
foreach($taskDomain in @('hrprotools.com','app.hrprotools.com')) {
 & $taskOpenSsl req -x509 -newkey rsa:2048 -noenc -days 1 -subj "/CN=$taskDomain" -keyout "$taskFolder/live/$taskDomain/privkey.pem" -out "$taskFolder/live/$taskDomain/fullchain.pem" 2>$null
 if($LASTEXITCODE -ne 0){throw 'QA certificate creation failed'}
}
Copy-Item "$taskRoot/deploy/production/nginx-site.conf","$taskRoot/deploy/production/nginx-app.conf" "$taskFolder/tls/"
Copy-Item "$taskRoot/deploy/production/nginx-acme.conf" "$taskFolder/acme/"
foreach($taskMode in @('acme','tls')) {
 docker run --rm --pull never --network none --entrypoint nginx --mount "type=bind,source=$taskFolder/$taskMode,target=/etc/nginx/conf.d,readonly" --mount "type=bind,source=$taskFolder/live,target=/etc/letsencrypt/live,readonly" nginx:1.31.2 -t
 if($LASTEXITCODE -ne 0){throw "Nginx validation failed: $taskMode"}
}
Write-Output 'Both HR configurations passed nginx -t locally, network disabled; only QA self-signed certificates were used.'
