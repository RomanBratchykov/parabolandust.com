FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj Services/Newsletters/Newsletters.Api/
COPY Services/Newsletters/Newsletters.Bll/Newsletters.Bll.csproj Services/Newsletters/Newsletters.Bll/
COPY Services/Newsletters/Newsletters.Dal/Newsletters.Dal.csproj Services/Newsletters/Newsletters.Dal/
COPY Services/Newsletters/Newsletters.Domain/Newsletters.Domain.csproj Services/Newsletters/Newsletters.Domain/
RUN dotnet restore Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj
COPY Services/Newsletters/ Services/Newsletters/
RUN dotnet publish Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Newsletters.Api.dll"]
