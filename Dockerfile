FROM mcr.microsoft.com/dotnet/core/sdk:2.2
RUN mkdir app
WORKDIR app
COPY ./onceandfuture .

RUN bash ./build-image.sh

ENTRYPOINT dotnet run --configuration="Release" -- serve --environment=Production --url=http://0.0.0.0:$PORT
