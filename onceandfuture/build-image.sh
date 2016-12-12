#!/bin/bash
# This is the script that we use to set up the docker image for the app.
# Clean up as hard as you can at the end, because everything still on disk
# when we're finished adds to the size of the image.
#
# I know, set -e is not recommended, but it works for this particular script and makes it so much more readable.
# Proper tools for the proper job.
set -e

apt-get update -q
apt-get dist-upgrade -y -q

# Fetch, build, and install Mono 4.8 (from source, since the packages that exist are no good.)
apt-get install -y -q git autoconf libtool automake build-essential mono-devel gettext cmake python

mkdir /tmp/mono
cd /tmp/mono

export MONO_TLS_PROVIDER=btls

MONO_PREFIX=/opt/mono
MONO_VERSION=4.8.0

curl https://download.mono-project.com/sources/mono/mono-4.8.0.374.tar.bz2 -o mono-$MONO_VERSION.tar.bz2
tar xf mono-$MONO_VERSION.tar.bz2
cd mono-$MONO_VERSION
./autogen.sh --prefix=$MONO_PREFIX
make
make install

curl -L -o /tmp/mono/certdata.txt https://hg.mozilla.org/releases/mozilla-release/raw-file/default/security/nss/lib/ckfw/builtins/certdata.txt
/opt/mono/bin/mozroots --import --sync --file /tmp/mono/certdata.txt
/opt/mono/bin/btls-cert-sync

curl -o /tmp/mono/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe

apt-get install libgdiplus --no-install-recommends -y

# Go fetch dotnet 1.1.0, and extract the parts we need.
mkdir /tmp/dotnet
cd /tmp/dotnet

DOTNET_VERSION=1.1.0
DOTNET_DOWNLOAD_URL=https://dotnetcli.blob.core.windows.net/dotnet/release/1.1.0/Binaries/$DOTNET_VERSION/dotnet-debian-x64.$DOTNET_VERSION.tar.gz
curl -SL $DOTNET_DOWNLOAD_URL -o dotnet.tar.gz
tar xf dotnet.tar.gz

cp ./shared/Microsoft.NETCore.App/1.1.0/System.Native.so /usr/lib/libSystem.Native.so
cp ./shared/Microsoft.NETCore.App/1.1.0/libuv.so /usr/lib/libuv.so

# Build the app
cd /app
/opt/mono/bin/mono /tmp/mono/nuget.exe restore -SolutionDirectory ..
/opt/mono/bin/xbuild

# Static? Need to work out how to bundle all mono dependencies too.
# /opt/mono/bin/mkbundle --deps --simple ./bin/Debug/onceandfuture.exe ./bin/Debug/*.dll

# CLEANUP
cd /app
rm -rf /packages
rm -rf /tmp/mono
rm -rf /tmp/dotnet
apt-get remove -y -q git autoconf libtool automake build-essential mono-devel gettext cmake python
apt-get autoremove -y -q
rm -rf /var/lib/apt/lists/*
