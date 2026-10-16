FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ApexPay/ApexPay.csproj", "ApexPay/"]
RUN dotnet restore "ApexPay/ApexPay.csproj"

COPY . .
RUN dotnet publish "ApexPay/ApexPay.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["sh", "-c", "dotnet ApexPay.dll --urls http://0.0.0.0:${PORT:-10000}"]
