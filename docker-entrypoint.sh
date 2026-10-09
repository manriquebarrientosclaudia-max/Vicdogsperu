#!/bin/sh
set -e

# Con disco persistente (plan de pago): USE_DISK=1 y disco montado en /var/data.
# Sin disco (plan gratuito): se usan tal cual las fotos incluidas en wwwroot/uploads.
if [ "$USE_DISK" = "1" ]; then
  DATA_DIR="${DATA_DIR:-/var/data}"
  mkdir -p "$DATA_DIR/uploads/caninos" "$DATA_DIR/keys"
  # copia las fotos del proyecto al disco la primera vez, sin pisar las que ya existan
  cp -rn /app/wwwroot/uploads/. "$DATA_DIR/uploads/" 2>/dev/null || true
  rm -rf /app/wwwroot/uploads
  ln -sfn "$DATA_DIR/uploads" /app/wwwroot/uploads
fi

mkdir -p /app/wwwroot/uploads/caninos
exec dotnet VicdogsPeru.dll
