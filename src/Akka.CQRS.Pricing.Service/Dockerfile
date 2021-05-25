FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS base
WORKDIR /app

# should be a comma-delimited list
ENV CLUSTER_SEEDS "[]"
ENV CLUSTER_IP ""
ENV CLUSTER_PORT "6055"
ENV MONGO_CONNECTION_STR "" #MongoDb connection string for Akka.Persistence

# 9110 - Petabridge.Cmd
# 6055 - Akka.Cluster
EXPOSE 9110 6055

# Install Petabridge.Cmd client
RUN dotnet tool install --global pbm 

COPY ./bin/Release/netcoreapp3.1/publish/ /app

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS app
WORKDIR /app

COPY --from=base /app /app

# copy .NET Core global tool
COPY --from=base /root/.dotnet /root/.dotnet/

# Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
ENV PATH="${PATH}:/root/.dotnet/tools"

CMD ["dotnet", "Akka.CQRS.Pricing.Service.dll"]