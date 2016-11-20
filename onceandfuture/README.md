# Instructions to build and run on MacOS

## Fix up System.Native

You'll need to fix up System.Native; not entirely sure why this is busted but
there you go. Here's what you do:

- cd onceandfuture
- dotnet restore
- Run:

    sudo ln -s \
    ~/.nuget/packages/runtime.osx.10.10-x64.runtime.native.System/1.0.1/runtimes/osx.10.10-x64/native/System.Native.dylib \
    /usr/local/Cellar/mono/4.6.1.5/lib/libSystem.Native.so

And you should be good to go. Version numbers may need a little tweaking; 
sorry.

## Be in 64bit mode

Make sure you either run with oaf.sh or you do:

    export MONO_ENV_OPTIONS=--arch=64

before you run, so that mono can load the native libraries, which are all 
64bit.