#!/usr/bin/env bash
# scripts/update-commons.sh [path-to-fnf-commons]
#
# Builds the shared library FlipNflopCommons for netstandard2.1 and copies it into the Unity
# project, with the canonical contract examples used by the EditMode tests:
#
#   Assets/Plugins/FlipNflopCommons/  FlipNflopCommons.dll, FlipNflopCommons.xml and VERSION
#   Assets/Tests/Gla/Contract/        contract/examples/ of the commons (valid/ and invalid/)
#
# VERSION records the package version and the commit of the commons checkout that was built.
# The commons has no dependencies, so no other DLL is copied. The .meta files are created by
# the Unity editor on import and are kept by this script; files removed from the commons are
# removed here, with their .meta.
#
# The default path is ../fnf-commons, the sibling checkout in the monorepo.
set -euo pipefail

readonly FRAMEWORK="netstandard2.1"
readonly ASSEMBLY="FlipNflopCommons"

GAME_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMMONS_ARG="${1:-$GAME_ROOT/../fnf-commons}"

fail() {
    echo "update-commons: $*" >&2
    exit 1
}

[ -d "$COMMONS_ARG" ] || fail "fnf-commons not found at '$COMMONS_ARG'."
COMMONS_ROOT="$(cd "$COMMONS_ARG" && pwd)"
PROJECT="$COMMONS_ROOT/src/FlipNflop.Commons/FlipNflop.Commons.csproj"
EXAMPLES="$COMMONS_ROOT/contract/examples"

[ -f "$PROJECT" ] || fail "project not found: $PROJECT"
[ -d "$EXAMPLES" ] || fail "contract examples not found: $EXAMPLES"
command -v dotnet >/dev/null 2>&1 || fail "the .NET SDK (dotnet) is required."

PLUGIN_DIR="$GAME_ROOT/Assets/Plugins/FlipNflopCommons"
CONTRACT_DIR="$GAME_ROOT/Assets/Tests/Gla/Contract"
OUTPUT_DIR="$COMMONS_ROOT/src/FlipNflop.Commons/bin/Release/$FRAMEWORK"

echo "Building $ASSEMBLY ($FRAMEWORK) from $COMMONS_ROOT"
dotnet build "$PROJECT" -c Release -f "$FRAMEWORK" -p:ContinuousIntegrationBuild=true --nologo -v quiet

for file in "$ASSEMBLY.dll" "$ASSEMBLY.xml"; do
    [ -f "$OUTPUT_DIR/$file" ] || fail "build output not found: $OUTPUT_DIR/$file"
done

VERSION="$(dotnet msbuild "$PROJECT" -getProperty:Version -p:TargetFramework="$FRAMEWORK" | tr -d '[:space:]')"
[ -n "$VERSION" ] || fail "could not read the version of $PROJECT."

COMMIT="unknown"
if git -C "$COMMONS_ROOT" rev-parse --git-dir >/dev/null 2>&1; then
    COMMIT="$(git -C "$COMMONS_ROOT" rev-parse HEAD)"
    if [ -n "$(git -C "$COMMONS_ROOT" status --porcelain --untracked-files=normal -- src contract)" ]; then
        COMMIT="$COMMIT-dirty"
        echo "update-commons: warning: $COMMONS_ROOT has uncommitted changes in src/ or contract/." >&2
    fi
fi

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

mkdir -p "$STAGING/plugin"
cp "$OUTPUT_DIR/$ASSEMBLY.dll" "$OUTPUT_DIR/$ASSEMBLY.xml" "$STAGING/plugin/"
{
    echo "version: $VERSION"
    echo "commit: $COMMIT"
    echo "framework: $FRAMEWORK"
} > "$STAGING/plugin/VERSION"

# Mirrors the regular files of $1 into $2: copies every file and removes from $2 the files
# (and their .meta) that no longer exist in $1. The .meta files of $2 are kept.
mirror() {
    local source="$1" target="$2" relative
    mkdir -p "$target"

    while IFS= read -r -d '' relative; do
        relative="${relative#./}"
        case "$relative" in
            *.meta) continue ;;
        esac
        if [ ! -f "$source/$relative" ]; then
            rm -f "$target/$relative" "$target/$relative.meta"
            echo "  removed $relative"
        fi
    done < <(cd "$target" && find . -type f -print0)

    while IFS= read -r -d '' relative; do
        relative="${relative#./}"
        mkdir -p "$target/$(dirname "$relative")"
        cp "$source/$relative" "$target/$relative"
    done < <(cd "$source" && find . -type f -print0)
}

echo "Copying the library to ${PLUGIN_DIR#"$GAME_ROOT"/}"
mirror "$STAGING/plugin" "$PLUGIN_DIR"

echo "Copying the contract examples to ${CONTRACT_DIR#"$GAME_ROOT"/}"
mirror "$EXAMPLES" "$CONTRACT_DIR"

echo "FlipNflopCommons $VERSION ($COMMIT) installed."
