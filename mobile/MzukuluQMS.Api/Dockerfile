# Build from the repository root:
# docker build -f mobile/MzukuluQMS.Api/Dockerfile -t mzukulu-qms-api:render .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj mobile/MzukuluQMS.Api/
RUN dotnet restore mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj
COPY mobile/MzukuluQMS.Api/ mobile/MzukuluQMS.Api/
RUN dotnet publish mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj \
    -c Release --no-restore -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /out/ ./
USER $APP_UID
# Documentation only; the runtime binding follows Render's PORT variable.
EXPOSE 10000
# exec forwards shutdown signals to dotnet. No secret is read during the build.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet MzukuluQMS.Api.dll --urls \"http://0.0.0.0:${PORT:-10000}\""]
