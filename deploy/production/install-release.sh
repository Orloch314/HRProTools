#!/bin/bash
set -euo pipefail
[[ ${1:-} == --apply && ${2:-} == Eqmatik && $(hostname) == Eqmatik && $EUID == 0 ]] || { echo 'Requires --apply Eqmatik on the approved target.' >&2; exit 1; }
release=${3:?absolute verified release directory required}
[[ $release =~ ^/opt/hrprotools/releases/[A-Za-z0-9._-]+$ && -d $release && ! -L $release ]] || exit 1
cd "$release"
sha256sum --check SHA256SUMS
[[ $(head -c 4 web/HRProTools.Web) == $'\x7fELF' ]]
[[ -f site/index.html && -f deploy/hrprotools.service ]]
[[ ! -e /etc/systemd/system/hrprotools.service ]] || { echo 'Initial install refuses to overwrite an existing service.' >&2; exit 1; }
[[ ! -e /opt/hrprotools/current && ! -L /opt/hrprotools/current && ! -e /var/www/hrprotools/site && ! -L /var/www/hrprotools/site ]] || exit 1
chmod 755 web/HRProTools.Web
install -d -m 755 /opt/hrprotools /var/www/hrprotools /var/www/hrprotools/acme
ln -s "$release" /opt/hrprotools/current
ln -s "$release/site" /var/www/hrprotools/site
install -m 644 deploy/hrprotools.service /etc/systemd/system/hrprotools.service
systemctl daemon-reload
echo 'HR release installed; service and Nginx remain untouched until explicit activation.'
