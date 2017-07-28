#!/bin/bash
# This is the script that we use to set up the docker image for the app.
# Clean up as hard as you can at the end, because everything still on disk
# when we're finished adds to the size of the image.
#
# I know, set -e is not recommended, but it works for this particular script
# and makes it so much more readable.  Proper tools for the proper job.
set -e

echo ======================================
echo INSTALLING SECURITY PATCHES
echo ======================================
apt-get update -q
apt-get dist-upgrade -y -q

echo ======================================
echo INSTALLING REQUIRED PACKAGES
echo ======================================
apt-get install -y -q curl xz-utils

echo ======================================
echo BUILDING APP
echo ======================================
dotnet restore
dotnet build --configuration="Release"

echo ======================================
echo BUILDING PRODUCTION JS
echo ======================================
# The checked-in bundle is debug; generate the production JS.
NODE_VERSION_NUMBER=v8.2.1
NODE_VERSION=node-$NODE_VERSION_NUMBER-linux-x64
mkdir /tmp/nodejs
cd /tmp/nodejs
curl -SL https://nodejs.org/dist/$NODE_VERSION_NUMBER/$NODE_VERSION.tar.xz -o $NODE_VERSION.tar.xz
tar xf $NODE_VERSION.tar.xz
cd /app
NODE_BIN_PATH=/tmp/nodejs/$NODE_VERSION/bin
env PATH=$PATH:$NODE_BIN_PATH $NODE_BIN_PATH/npm install
env PATH=$PATH:$NODE_BIN_PATH $NODE_BIN_PATH/node ./node_modules/webpack/bin/webpack.js \
    --config webpack.production.config.js
rm -rf ./node_modules/
rm -rf /tmp/nodejs

# CLEANUP
cd /app

echo ======================================
echo REMOVING PACKAGES
echo ======================================
apt-get remove -q -y curl xz-utils
apt-get autoremove -y

echo ======================================
echo REMOVING CACHE THINGS
echo ======================================
rm -rf /var/lib/apt/lists/*
rm -rf /root/.nuget
rm -rf /root/.npm
rm -rf /root/.local
