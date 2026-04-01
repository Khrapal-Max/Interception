#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Використання: $0 <шлях_до_флешки>"
  exit 1
fi

USB_PATH="$1"
OUT_DIR="./artifacts/wpf/win-x64"

if [ ! -d "$USB_PATH" ]; then
  echo "Помилка: шлях '$USB_PATH' не існує або флешка не змонтована."
  exit 1
fi

echo "Публікація WPF-клієнта..."
dotnet publish ./src/Interception.WpfClient/Interception.WpfClient.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$OUT_DIR"

echo "Копіювання на флешку: $USB_PATH"
rsync -a --delete "$OUT_DIR"/ "$USB_PATH"/Interception.WpfClient/

echo "Готово. Артефакти на флешці: $USB_PATH/Interception.WpfClient"
