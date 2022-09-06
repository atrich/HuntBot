# HuntBot
Discord bot for puzzle hunts

# Running locally
- Build: `docker build -t hunt-bot-image:latest .`
- Run: `docker run -it hunt-bot-image:latest -e KeyVaultSecret=<secret>`

# Deploying
- Login: `az login`
- Build and push image: `az acr build -r HuntBotAcr -t hunt-bot-image:latest .`
- Container restart: `az container restart --resource-group HuntBot --name huntbot-container`
- Container creation: `az container create --resource-group HuntBot --name huntbot-container --image huntbotacr.azurecr.io/hunt-bot-image:latest --registry-username HuntBotAcr --registry-password <pwd> --secure-environment-variables KeyVaultSecret=<secret> --location westus3`
