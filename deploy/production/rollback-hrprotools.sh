#!/bin/bash
set -euo pipefail
[[ ${1:-} == --apply && ${2:-} == Eqmatik && $(hostname) == Eqmatik && $EUID == 0 ]] || exit 1
systemctl stop hrprotools.service
backup=/var/backups/hrprotools/rollback-$(date -u +%Y%m%dT%H%M%SZ)
install -d -m 700 "$backup"
for file in /etc/nginx/conf.d/hrprotools-site.conf /etc/nginx/conf.d/hrprotools-app.conf; do
 [[ ! -L $file ]] || exit 1
 [[ ! -f $file ]] || mv "$file" "$backup/"
done
if nginx -t; then systemctl reload nginx;
else
 for file in "$backup"/*.conf; do [[ ! -f $file ]] || mv "$file" /etc/nginx/conf.d/; done
 echo 'Validation failed; HR config files restored. No Nginx reload performed.' >&2
 exit 1
fi
echo 'HR stopped; database, roles, keys and data retained. No other service was stopped.'
