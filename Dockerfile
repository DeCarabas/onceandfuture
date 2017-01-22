FROM decarabas/monodev:latest
RUN mkdir app & mkdir ImageSharp
WORKDIR app
COPY ./onceandfuture .
COPY ./ImageSharp ../ImageSharp

RUN bash ./build-image.sh

ENTRYPOINT env MONO_TLS_PROVIDER="btls" /opt/mono/bin/mono ./bin/Debug/onceandfuture.exe serve --environment=Production --url=http://0.0.0.0:$PORT -vvvv
