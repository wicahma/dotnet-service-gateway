FROM registry.acc.co.id/devops/dotnet/aspnet:10.0-alpine

ENV ASPNETCORE_URLS=http://*:3000
RUN apk add --no-cache tzdata icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TZ=Asia/Jakarta
ARG SERVICE
WORKDIR /app/publish
COPY ${SERVICE} ./ 
RUN adduser --disabled-password --gecos '' AppUser && \
    chown -R AppUser /app

EXPOSE 3000
USER AppUser
ENTRYPOINT ["dotnet", "K8sGateway.Host.dll"]

