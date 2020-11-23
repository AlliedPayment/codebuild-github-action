FROM mcr.microsoft.com/dotnet/sdk:3.1-bionic AS build-env 
COPY . /Allied.Codebuild/
WORKDIR /Allied.Codebuild
RUN dotnet restore  ./Allied.Codebuild/Allied.Codebuild.csproj 
RUN dotnet build  ./Allied.Codebuild/Allied.Codebuild.csproj

FROM mcr.microsoft.com/dotnet/runtime:3.1-bionic
COPY --from=build-env /Allied.Codebuild/Allied.Codebuild/bin/Debug/netcoreapp3.1 ./app

ENTRYPOINT ["/app/Allied.Codebuild"]
