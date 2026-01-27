FROM registry.acc.co.id/devops/dotnet/aspnet:10.0-alpine

ENV ASPNETCORE_URLS=http://*:5142
RUN tzutil /s "SE Asia Standard Time"
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ARG SERVICE
WORKDIR C:\app\publish
COPY ${SERVICE} .

EXPOSE 5142
USER ContainerUser
ENTRYPOINT ["dotnet", "K8sGateway.Host.dll"]

