#!/bin/sh
set -eu
# Attach only to the two HR renewal lineages; never renew other domains here.
case "${RENEWED_LINEAGE:-}" in
 /etc/letsencrypt/live/hrprotools.com|/etc/letsencrypt/live/app.hrprotools.com)
  nginx -t
  systemctl reload nginx
  ;;
 *) exit 0 ;;
esac
