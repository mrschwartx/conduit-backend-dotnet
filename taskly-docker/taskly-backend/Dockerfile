FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Taskly.Host/Taskly.Host.csproj src/Taskly.Host/
COPY src/Taskly.SharedKernel/Taskly.SharedKernel.csproj src/Taskly.SharedKernel/
COPY src/Modules/Users/Taskly.Modules.Users/Taskly.Modules.Users.csproj src/Modules/Users/Taskly.Modules.Users/
COPY src/Modules/Tasks/Taskly.Modules.Tasks/Taskly.Modules.Tasks.csproj src/Modules/Tasks/Taskly.Modules.Tasks/
RUN dotnet restore src/Taskly.Host/Taskly.Host.csproj
COPY src ./src
RUN dotnet publish src/Taskly.Host/Taskly.Host.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runner
ENV APP_HOST=0.0.0.0 \
    APP_PORT=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
WORKDIR /app
COPY --from=build --chown=app:app /out ./
COPY --chown=app:app public ./public
RUN mkdir -p public/images/users && chown -R app:app /app
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=60s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Taskly.Host.dll"]
