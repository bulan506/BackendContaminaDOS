# ContaminaDOS API Deployment Guide 🚀

![Oracle Linux](https://img.shields.io/badge/Oracle%20Linux-8-red)
![Docker](https://img.shields.io/badge/Docker-20.10+-blue)
![MongoDB](https://img.shields.io/badge/MongoDB-Latest-success)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-Latest-blueviolet)
![ContaminaDOS](https://img.shields.io/badge/ContaminaDOS-API-blue)
![Version](https://img.shields.io/badge/Version-2024-brightgreen)
![UCR](https://img.shields.io/badge/UCR-blue)
![Redes](https://img.shields.io/badge/Redes-Paraíso-green)

## 📖 Description

This is the deployment guide for the ContaminaDOS API, an educational game developed for the Business Networks II course at the University of Costa Rica, Atlantic Campus - Paraíso.

The API source code is developed in .NET 8.0 and is hosted in this repository. We use GitHub Actions for continuous integration, which enables:

- Docker image building
- Automatic publication to Docker Hub as `bulan506/contaminados2024api:latest`

The API provides endpoints for:

- Game session management
- Turn system
- Game mechanics
- Player interactions

### 🔄 CI/CD Pipeline

- ✅ Each push to main branch triggers the pipeline
- 🐳 Docker image building
- 📦 Push to Docker Hub
- 🚀 Ready for deployment

### 🏗️ Infrastructure

The API is deployed on Oracle Cloud Infrastructure (OCI) with the following specifications:

#### Compute Instance Details

```yaml
Operating System: Oracle Linux
Version: 8
Image: Oracle-Linux-8.10-2024.09.30-0
Shape: VM.Standard.E5.Flex
OCPU Count: 1
Memory: 12 GB
Network Bandwidth: 1 Gbps
Storage: Block Storage Only
```

#### Infrastructure Components

- Compute Instance running Oracle Linux 8
- Virtual Cloud Network (VCN)
- Security Lists for network traffic control
- Subnet Configuration
- NGINX Reverse Proxy
- Docker Container Environment
- MongoDB Database

## 📋 Prerequisites

- Oracle Linux 8
- Root or sudo access
- Internet connection
- Open ports 80 and 443

## 🎯 Features

- MongoDB database
- Containerized API
- NGINX reverse proxy
- SSL support via Certbot
- Auto-restart capability
- Data persistence
- Docker network isolation

## 🛠 Installation

### 1. System Update

```bash
sudo yum update -y
```

### 2. NGINX Installation

```bash
sudo yum install -y nginx
sudo systemctl start nginx
sudo systemctl enable nginx
```

### 3. Docker Installation

```bash
sudo dnf config-manager --add-repo=https://download.docker.com/linux/centos/docker-ce.repo
sudo dnf install -y docker-ce docker-ce-cli containerd.io

# Start and enable Docker
sudo systemctl start docker
sudo systemctl enable docker
```

### 4. Docker Compose Installation

```bash
sudo curl -L "https://github.com/docker/compose/releases/download/v2.24.5/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
sudo ln -s /usr/local/bin/docker-compose /usr/bin/docker-compose

# Verify installation
docker-compose --version
```

### 5. Project Setup

```bash
sudo mkdir -p /opt/contaminados
cd /opt/contaminados
```

### 6. Docker Compose Configuration

Create `docker-compose.yml`:

```yaml
version: "3.8"

services:
  mongodb:
    image: mongo:latest
    container_name: contenedorMongo
    restart: always
    volumes:
      - mongodb_data:/data/db
    networks:
      - contaminados-net
    ports:
      - "27010:27017"

  api:
    image: bulan506/contaminados2024api:latest
    container_name: contaminaDOS
    restart: always
    depends_on:
      - mongodb
    environment:
      - DB_CONNECTION_STRING=mongodb://mongodb:27017
      - ASPNETCORE_ENVIRONMENT=Development
    networks:
      - contaminados-net
    ports:
      - "8000:8080"

networks:
  contaminados-net:
    driver: bridge

volumes:
  mongodb_data:
    driver: local
```

### 7. SSL Configuration

```bash
# Install EPEL repository
sudo dnf install epel-release -y
sudo dnf config-manager --set-enabled ol8_developer_EPEL
sudo dnf makecache

# Install Certbot and NGINX plugin
sudo dnf install certbot python3-certbot-nginx -y
sudo dnf install -y setroubleshoot-server setools-console
sudo setsebool -P httpd_can_network_connect 1

# Configure Firewall
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload
# Generate SSL certificate using Certbot
sudo certbot --nginx -d www.grupoe.oci.meseguercr.com
```

### 8. NGINX Configuration

Create `/etc/nginx/conf.d/contaminados.conf`:

```nginx
# HTTP Server
server {
    listen 80;
    server_name www.grupoe.oci.meseguercr.com;

    location / {
        proxy_pass http://localhost:8000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# HTTPS Server
server {
    listen 443 ssl;
    server_name www.grupoe.oci.meseguercr.com;

    ssl_certificate /etc/letsencrypt/live/www.grupoe.oci.meseguercr.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/www.grupoe.oci.meseguercr.com/privkey.pem;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass http://localhost:8000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
```

### 9. Security Configuration

```bash
# Configure SELinux
sudo dnf install -y setroubleshoot-server setools-console
sudo setsebool -P httpd_can_network_connect 1

# Configure Firewall
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload
```

### 10. Start Services

```bash
# Start containers
cd /opt/contaminados
sudo docker-compose up -d
# Restart NGINX
sudo firewall-cmd --reload
sudo systemctl restart nginx
```

## 🔍 Verification Commands

```bash
# Check container logs
sudo docker-compose logs

# Check container status
sudo docker-compose ps

# Check Docker networks
sudo docker network ls

# Check Docker volumes
sudo docker volume ls

# Check NGINX status
sudo systemctl status nginx
```

## 🔧 Maintenance Commands

```bash
# Restart containers
sudo docker-compose restart

# Stop containers
sudo docker-compose stop

# Remove containers
sudo docker-compose down

# Complete cleanup (including volumes)
sudo docker-compose down -v
```

## ⚠️ Important Notes

1. Default MongoDB port is mapped to 27010
2. The API is accessible on port 8000
3. SSL certificates are managed by Certbot
4. Certificate renewal is handled automatically
5. Logs are available via `docker-compose logs`

## 🔒 Security Considerations

- Regularly update containers and system
- Monitor logs for suspicious activity
- Implement proper backup strategies
- Keep SSL certificates up to date
- Monitor certificate renewal status
