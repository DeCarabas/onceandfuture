FROM microsoft/dotnet:latest
RUN mkdir app
WORKDIR app
COPY ./onceandfuture .

# Maybe remove:
# - System.IO.Compression
# - System.Net.Http.WebRequest

RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
 && echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list \
 && echo "deb http://download.mono-project.com/repo/debian wheezy-libjpeg62-compat main" | tee -a /etc/apt/sources.list.d/mono-xamarin.list \
 && apt-get update \
 && apt-get install -q -y \
        libmono-microsoft-csharp4.0-cil \
        libmono-system4.0-cil \
        libmono-system-core4.0-cil \
        libmono-system-data4.0-cil \
        libmono-system-data-datasetextensions4.0-cil \
        libmono-system-drawing4.0-cil \
        libmono-system-io-compression4.0-cil \
        libmono-system-net4.0-cil \
        libmono-system-net-http4.0-cil \
        libmono-system-net-http-webrequest4.0-cil \
        libmono-system-numerics4.0-cil \
        libmono-system-runtime4.0-cil \
        libmono-system-runtime-caching4.0-cil \
        libmono-system-runtime-serialization4.0-cil \
        libmono-system-security4.0-cil \
        libmono-system-xml4.0-cil \
        libmono-system-xml-linq4.0-cil \
        libgdiplus \
        \
        mono-devel \
        mono-xbuild \
        mono-mcs \
        nuget \
 && ln -s /usr/share/dotnet/shared/Microsoft.NETCore.App/1.1.0/System.Native.so /usr/lib/libSystem.Native.so \ 
 && ln -s /usr/share/dotnet/shared/Microsoft.NETCore.App/1.1.0/libuv.so /usr/lib/libuv.so \
 && nuget update -self \
 && nuget restore -SolutionDirectory .. \
 && xbuild \
 && rm -rf ../packages 
    
ENTRYPOINT mono bin/Debug/onceandfuture.exe serve --environment=Production --url=http://0.0.0.0:$PORT -vvvv