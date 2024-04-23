FROM amd64/buildpack-deps:bullseye-curl AS installer
RUN dotnet_version=8.0.4 \
    && curl -fSL --output dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-linux-x64.tar.gz \
    && mkdir -p /dotnet \
    && tar -oxzf dotnet.tar.gz -C /dotnet \
    && rm dotnet.tar.gz

FROM python:3-slim-bullseye AS migra
USER root
RUN pip install migra~=3.0.0 psycopg2-binary~=2.9.3

FROM migra AS postgres
RUN apt-get update && apt-get install wget gnupg -y
RUN wget --quiet -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | apt-key add -
RUN sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt bullseye-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
RUN apt-get update && apt-get install -y software-properties-common postgresql-14 postgresql-client-14 && apt-get clean
USER postgres
RUN /etc/init.d/postgresql start &&\
    psql --command "CREATE USER docker WITH SUPERUSER PASSWORD 'docker';" &&\
    createdb -O docker docker
RUN echo "local   all    all                    scram-sha-256" >> /etc/postgresql/14/main/pg_hba.conf
RUN echo "host    all    all    127.0.0.1/32    scram-sha-256" >> /etc/postgresql/14/main/pg_hba.conf
RUN echo "listen_addresses='127.0.0.1'" >> /etc/postgresql/14/main/postgresql.conf
RUN echo "port=9797" >> /etc/postgresql/14/main/postgresql.conf

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
WORKDIR /src
COPY ["DatabasePublisher/DatabasePublisher.csproj", "DatabasePublisher/"]
COPY . .
WORKDIR "/src/DatabasePublisher"
RUN dotnet publish "DatabasePublisher.csproj" -c Release -o /app/publish

FROM postgres AS final-with-postgres
ENV DOTNET_VERSION=8.0.4
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=installer ["/dotnet", "/usr/share/dotnet"] .
USER root
RUN echo "/etc/init.d/postgresql start && dotnet /app/DatabasePublisher.dll \"\$@\"" >> /usr/bin/entrypoint.sh
RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
WORKDIR /working

ENTRYPOINT ["bash", "entrypoint.sh"]
CMD ["--help"]