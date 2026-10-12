# syntax=docker/dockerfile:1

# The SDK stage runs on the build machine and cross-compiles, so multi-arch builds need no emulation
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
ARG TARGETARCH
ARG SOURCE_REVISION=unknown
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props BannedSymbols.txt .editorconfig ./
COPY src/InvoiceFlow.ServiceDefaults/InvoiceFlow.ServiceDefaults.csproj src/InvoiceFlow.ServiceDefaults/
COPY src/InvoiceFlow.Worker/InvoiceFlow.Worker.csproj src/InvoiceFlow.Worker/
RUN dotnet restore src/InvoiceFlow.Worker/InvoiceFlow.Worker.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/InvoiceFlow.Worker/InvoiceFlow.Worker.csproj -a $TARGETARCH --no-restore -c Release -o /app \
    -p:UseAppHost=false -p:SourceRevisionId=$SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra@sha256:00e0ad6a7ef8c0c1391b87f05c7ac757a15740455688f2bfcd146a3f4b987efd
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "InvoiceFlow.Worker.dll"]
