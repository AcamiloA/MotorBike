FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/UniversityParking.Domain/ src/UniversityParking.Domain/
COPY src/UniversityParking.Contracts/ src/UniversityParking.Contracts/
COPY src/UniversityParking.Application/ src/UniversityParking.Application/
COPY src/UniversityParking.Infrastructure/ src/UniversityParking.Infrastructure/
COPY src/UniversityParking.Api/ src/UniversityParking.Api/
RUN dotnet publish src/UniversityParking.Api/UniversityParking.Api.csproj -c Release -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN mkdir -p /app/App_Data/private-files && chown -R app:app /app
WORKDIR /app
COPY --from=build /publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "UniversityParking.Api.dll"]
