FROM microsoft/dotnet:latest
ENV PORT=8080
RUN mkdir app
WORKDIR app
COPY ./onceandfuture .
# Set up Mono
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF &&\
    echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list &&\
    echo "deb http://download.mono-project.com/repo/debian wheezy-libjpeg62-compat main" | tee -a /etc/apt/sources.list.d/mono-xamarin.list &&\
    apt-get update &&\
    apt-get install mono-complete -y &&\
    ln -s /usr/share/dotnet/shared/Microsoft.NETCore.App/1.1.0/System.Native.so /usr/lib/libSystem.Native.so
RUN dotnet restore
RUN dotnet build
ENTRYPOINT dotnet run serve \
             --environment=Production \
             --url=http://0.0.0.0:$PORT