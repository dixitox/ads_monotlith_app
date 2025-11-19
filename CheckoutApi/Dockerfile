# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CheckoutApi/CheckoutApi.csproj", "CheckoutApi/"]
RUN dotnet restore "CheckoutApi/CheckoutApi.csproj"
COPY CheckoutApi/ ./CheckoutApi/
WORKDIR "/src/CheckoutApi"
RUN dotnet build "CheckoutApi.csproj" -c Release -o /app/build
RUN dotnet publish "CheckoutApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Security: Run as non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CheckoutApi.dll"]
