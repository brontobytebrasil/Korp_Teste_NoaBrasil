FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Estoque.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# O Npgsql procura a biblioteca de Kerberos ao abrir a conexão. Sem ela a
# aplicação funciona, mas registra erro no log logo na subida.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
# A imagem oficial já traz um usuário sem privilégio; rodar como root
# dentro do container não traz nada em troca.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Estoque.dll"]
