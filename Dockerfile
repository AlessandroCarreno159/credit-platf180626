# P8: despliegue Render (Free). SDK para compilar, runtime ASP.NET para correr.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY credit-platf.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
# El puerto lo inyecta Render via $PORT en el startCommand (no fijarlo aqui).
ENTRYPOINT ["dotnet", "credit-platf.dll"]
