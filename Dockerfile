FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copiar archivos de solución y proyectos
COPY *.sln ./
COPY contaminaDOS/*.csproj contaminaDOS/
COPY Core/*.csproj Core/

# Restaurar dependencias
RUN dotnet restore

# Copiar el resto de la aplicación
COPY . ./

# Compilar y publicar
RUN dotnet publish -c Release -o out

# Crear la imagen de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .

# Establecer el punto de entrada
ENTRYPOINT ["dotnet", "contaminaDOS.dll"]
