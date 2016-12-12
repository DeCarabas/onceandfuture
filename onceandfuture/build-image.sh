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

# Go fetch dotnet 1.1.0, and extract the parts we need.
apt-get install -y -q curl
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
/opt/mono/bin/mono /opt/mono/nuget.exe restore -SolutionDirectory ..
/opt/mono/bin/xbuild

# Static? Need to work out how to bundle all mono dependencies too.
# /opt/mono/bin/mkbundle --deps --simple ./bin/Debug/onceandfuture.exe ./bin/Debug/*.dll

# CLEANUP
cd /app
rm -rf /packages
rm -rf /tmp/dotnet
rm -rf /root/.nuget

# I'd love to just drop the entire runtime after running mkbundle but mkbundle doesn't work for me yet.
rm -rf /opt/mono/lib/mono/3.5-api/
rm -rf /opt/mono/lib/mono/4.0/
rm -rf /opt/mono/lib/mono/4.0-api/
rm -rf /opt/mono/lib/mono/lldb/
rm -rf /opt/mono/lib/mono/monodoc/
rm -rf /opt/mono/lib/mono/xbuild/
rm -rf /opt/mono/lib/mono/xbuild-frameworks/
rm -rf /opt/mono/lib/*.a

apt-get remove -q -y curl
apt-get autoremove -y
rm -rf /var/lib/apt/lists/*
