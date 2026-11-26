#!/bin/bash
set -euo pipefail
[[ ${1:-} == --apply && ${2:-} == Eqmatik && $(hostname) == Eqmatik && $EUID == 0 ]] || exit 1
mode=${3:?choose acme or tls}
source=/opt/hrprotools/current/deploy
site=/etc/nginx/conf.d/hrprotools-site.conf
app=/etc/nginx/conf.d/hrprotools-app.conf
[[ ! -L $site && ! -L $app ]] || exit 1
nginx -t
backup=/var/backups/hrprotools/nginx-$(date -u +%Y%m%dT%H%M%SZ)
install -d -m 700 "$backup"
for file in "$site" "$app"; do [[ ! -e $file ]] || cp -p "$file" "$backup/$(basename "$file")"; done
reload_attempted=false
rollback() {
 trap - ERR
 for file in "$site" "$app"; do
  if [[ -f $backup/$(basename "$file") ]]; then cp -p "$backup/$(basename "$file")" "$file";
  elif [[ -f $file ]]; then mv "$file" "$backup/failed-$(basename "$file")"; fi
 done
 if $reload_attempted; then nginx -t && systemctl reload nginx; fi
 exit 1
}
trap rollback ERR
if [[ $mode == acme ]]; then
 [[ ! -e $site && ! -e $app ]] || exit 1
 install -m 644 "$source/nginx-acme.conf" "$site"
elif [[ $mode == tls ]]; then
 [[ -f /etc/letsencrypt/live/hrprotools.com/fullchain.pem && -f /etc/letsencrypt/live/app.hrprotools.com/fullchain.pem ]]
 install -m 644 "$source/nginx-site.conf" "$site"
 install -m 644 "$source/nginx-app.conf" "$app"
else exit 1; fi
nginx -t
reload_attempted=true
systemctl reload nginx
trap - ERR
echo 'Only HR virtual hosts activated. Existing files were not overwritten.'
