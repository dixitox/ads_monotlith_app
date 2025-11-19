#!/bin/bash
echo "Installing mssql-tools"
# Install prerequisites
apt-get update
apt-get install -y curl gnupg2 apt-transport-https ca-certificates lsb-release

# Add Microsoft repository key using the new method
curl -sSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg

# Add Microsoft repository
DISTRO=$(lsb_release -is | tr '[:upper:]' '[:lower:]')
CODENAME=$(lsb_release -cs)
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/repos/microsoft-${DISTRO}-${CODENAME}-prod ${CODENAME} main" > /etc/apt/sources.list.d/microsoft.list

# Update and install mssql-tools
apt-get update
ACCEPT_EULA=Y apt-get -y install unixodbc-dev msodbcsql18 mssql-tools18

echo "Installing sqlpackage"
curl -sSL -o sqlpackage.zip "https://aka.ms/sqlpackage-linux"
mkdir /opt/sqlpackage
unzip sqlpackage.zip -d /opt/sqlpackage 
rm sqlpackage.zip
chmod a+x /opt/sqlpackage/sqlpackage
