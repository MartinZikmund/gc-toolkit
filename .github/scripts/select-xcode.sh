#!/usr/bin/env bash
# Selects the Xcode the installed .NET iOS workload was built against. The workload
# floats with `workload install`/`restore`, so a pinned or image-default Xcode drifts
# out of sync with it (the build then fails with "requires Xcode X.Y").
set -euo pipefail

dotnet_root="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet)")")}"
tfm="${1:-net10.0}"

plist=$(ls -d "$dotnet_root"/packs/Microsoft.iOS.Sdk."${tfm}"_*/*/Versions.plist 2>/dev/null | sort -V | tail -1 || true)
if [[ -z "$plist" ]]; then
  echo "::error::No Microsoft.iOS.Sdk.${tfm}_* pack under $dotnet_root/packs - install the ios workload first."
  exit 1
fi

required=$(plutil -extract RecommendedXcodeVersion raw -o - "$plist")
echo "iOS workload ($plist) wants Xcode $required"

for candidate in "/Applications/Xcode_${required}.app" "/Applications/Xcode_${required}.0.app"; do
  if [[ -d "$candidate" ]]; then
    sudo xcode-select -s "$candidate/Contents/Developer"
    xcodebuild -version
    exit 0
  fi
done

echo "::error::Xcode $required is not installed on this runner. Installed: $(ls -d /Applications/Xcode_*.app | xargs -n1 basename | tr '\n' ' ')"
exit 1
