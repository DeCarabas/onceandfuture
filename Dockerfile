FROM microsoft/dotnet:latest
RUN mkdir app
WORKDIR app
COPY ./onceandfuture .

RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
 && echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list \
 && echo "deb http://download.mono-project.com/repo/debian wheezy-libjpeg62-compat main" | tee -a /etc/apt/sources.list.d/mono-xamarin.list \
 && apt-get update \
 && apt-get install nuget mono-complete -y \
 && ln -s /usr/share/dotnet/shared/Microsoft.NETCore.App/1.1.0/System.Native.so /usr/lib/libSystem.Native.so \ 
 && ln -s /usr/share/dotnet/shared/Microsoft.NETCore.App/1.1.0/libuv.so /usr/lib/libuv.so \
 && apt-get install nuget -y -q \
 && nuget update -self \
 && nuget restore -SolutionDirectory .. \
 && xbuild \
 && rm -rf ../packages 
    
ENTRYPOINT mono bin/Debug/onceandfuture.exe serve --environment=Production --url=http://0.0.0.0:$PORT