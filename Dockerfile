FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim
WORKDIR scripts
COPY install.fsx ./
RUN ["apt", "install", "bash", "dbus-x11", "gnome-keyring"]
RUN ["dotnet", "fsi", "./install.fsx"]
