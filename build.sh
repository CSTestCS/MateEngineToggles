#!/usr/bin/env bash
# Builds the plugin and packages it into dist/.
# Needs the .NET SDK (6.0 or newer). No game files or BepInEx download required.
set -euo pipefail
cd "$(dirname "$0")"

dotnet build src/VRoidClothingToggles/VRoidClothingToggles.csproj -c Release -nologo

out=src/VRoidClothingToggles/bin/Release/net472/VRoidClothingToggles.dll
stage=$(mktemp -d)
mkdir -p "$stage/BepInEx/plugins/VRoidClothingToggles" dist
cp "$out" "$stage/BepInEx/plugins/VRoidClothingToggles/"
cp "$out" dist/
rm -f dist/VRoidClothingToggles.zip
(cd "$stage" && zip -qr -X "$OLDPWD/dist/VRoidClothingToggles.zip" BepInEx)
rm -rf "$stage"
echo "Built dist/VRoidClothingToggles.dll and dist/VRoidClothingToggles.zip"
