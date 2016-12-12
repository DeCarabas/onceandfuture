FROM debian:jessie
RUN mkdir app
WORKDIR app
COPY ./onceandfuture .

RUN bash ./build-image.sh

ENTRYPOINT env MONO_TLS_PROVIDER="btls" /opt/mono/bin/mono ./bin/Debug/onceandfuture.exe serve --environment=Production --url=http://0.0.0.0:$PORT -vvvv
