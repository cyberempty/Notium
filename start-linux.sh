#!/usr/bin/env bash
# Notium launcher for Linux. First start: builds Notium-portable (one executable), then runs it.
cd "$(dirname "$0")" || exit 1
ROOT="$PWD"
case "$(uname -m)" in aarch64|arm64) ARCH=arm64 ;; *) ARCH=x64 ;; esac
RID="linux-$ARCH"
PORT="$ROOT/Notium-portable"
EXE="$PORT/Notium-linux"
RT="$ROOT/runtime"
export DOTNET_ROOT="$RT/dotnet"
export DOTNET_CLI_HOME="$RT/home"
export NUGET_PACKAGES="$RT/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_XMLDOC_MODE=skip MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 DOTNET_MULTILEVEL_LOOKUP=0

# Downloads a file with a progress bar.
download() {
  if command -v curl >/dev/null 2>&1; then curl -fL --progress-bar "$1" -o "$2"
  elif command -v wget >/dev/null 2>&1; then wget -q --show-progress "$1" -O "$2"
  else echo "  curl or wget is required."; return 1; fi
}

# Installs the .NET SDK with a progress bar; falls back to Microsoft's installer if the address cannot be found.
install_dotnet() {
  download https://dot.net/v1/dotnet-install.sh "$RT/dotnet-install.sh" >/dev/null 2>&1 || return 1
  local url
  url="$(bash "$RT/dotnet-install.sh" --channel 10.0 --architecture "$ARCH" --install-dir "$DOTNET_ROOT" --no-path --dry-run 2>/dev/null \
        | tr -d '\r' | sed -n 's/.*Primary named payload URL: *//p' | head -n1)"
  if [ -n "$url" ]; then
    download "$url" "$RT/dotnet-sdk.tar.gz" || return 1
    echo "          Extracting..."
    mkdir -p "$DOTNET_ROOT" && tar -xzf "$RT/dotnet-sdk.tar.gz" -C "$DOTNET_ROOT" --exclude=templates --exclude="*/FSharp" --exclude="*/DotnetTools" || return 1
    rm -f "$RT/dotnet-sdk.tar.gz"
  else
    echo "          Installing..."
    bash "$RT/dotnet-install.sh" --channel 10.0 --architecture "$ARCH" --install-dir "$DOTNET_ROOT" --no-path >/dev/null 2>&1 || return 1
  fi
  rm -f "$RT/dotnet-install.sh"
  [ -x "$DOTNET_ROOT/dotnet" ]
}

fail() {
  echo
  echo "  --------------------------------------------------------"
  echo "  $1"
  echo "  Please copy the message above and report it."
  echo "  --------------------------------------------------------"
  read -r -p "  Press Enter to close..." _
  exit 1
}

if [ ! -f "$EXE" ]; then
  clear 2>/dev/null
  echo
  echo "  NOTIUM"
  echo "  --------------------------------------------------------"
  echo "  First start: preparing the program. This happens only once"
  echo "  and needs an Internet connection."
  echo
  echo "  [  0%] Downloading components"
  mkdir -p "$RT"
  install_dotnet || fail "Download or installation of the components failed."

  echo
  echo "  [ 50%] Building Notium (this takes a few minutes)"
  "$DOTNET_ROOT/dotnet" publish "$ROOT/src/Notium/Notium.csproj" -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false -nodeReuse:false \
    -nologo -v q -o "$RT/out" || fail "The build failed."

  echo "  [ 90%] Creating Notium-portable"
  mkdir -p "$PORT" && cp -R "$RT/out/." "$PORT/" || fail "Could not create Notium-portable."
  mv "$PORT/Notium.App" "$EXE"

  echo "  [100%] Done. Starting Notium..."
  echo
  # tidy up in the background, after the program is already on screen
  ( "$DOTNET_ROOT/dotnet" build-server shutdown >/dev/null 2>&1; rm -rf "$RT" "$ROOT/src/Notium/obj" "$ROOT/src/Notium/bin" ) >/dev/null 2>&1 &
fi

chmod +x "$EXE" 2>/dev/null
exec "$EXE" "$@"
