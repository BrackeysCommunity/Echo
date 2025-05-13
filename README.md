<h1 align="center">Echo</h1>
<p align="center"><img src="icon.png" width="128"></p>
<p align="center"><i>A Discord bot for managing temporary voice channels.</i></p>
<p align="center">
<a href="https://github.com/BrackeysCommunity/Echo/releases"><img src="https://img.shields.io/github/v/release/BrackeysCommunity/Echo?include_prereleases&style=flat-square"></a>
<a href="https://github.com/BrackeysCommunity/Echo/actions/workflows/dotnet.yml"><img src="https://img.shields.io/github/actions/workflow/status/BrackeysCommunity/Echo/dotnet.yml?branch=main&style=flat-square" alt="GitHub Workflow Status" title="GitHub Workflow Status"></a>
<a href="https://github.com/BrackeysCommunity/Echo/issues"><img src="https://img.shields.io/github/issues/BrackeysCommunity/Echo?style=flat-square" alt="GitHub Issues" title="GitHub Issues"></a>
<a href="https://github.com/BrackeysCommunity/Echo/blob/main/LICENSE.md"><img src="https://img.shields.io/github/license/BrackeysCommunity/Echo?style=flat-square" alt="MIT License" title="MIT License"></a>
</p>

## About
Echo is a Discord bot which allows users to create temporary voice channels.
It is designed to be used in conjunction with the [Brackeys Discord server](https://discord.gg/brackeys), but can be used on any server.

## Installing and configuring Echo 
Echo runs in a Docker container, and there is a [compose.yaml](compose.yaml) file which simplifies this process.

### Clone the repository
To start off, clone the repository into your desired directory:
```bash
git clone https://github.com/BrackeysCommunity/Echo.git
```
Step into the Echo directory using `cd Echo`, and continue with the steps below.

### Setting things up
The bot's token is passed to the container using the `DISCORD_TOKEN` environment variable.
This must be assigned in the compose.yaml file, or in an `.env` file in the same directory as the compose.yaml file.
```
DISCORD_TOKEN=your_token_here
```

Two directories are required to exist for Docker compose to mount as container volumes, `data` and `logs`:
```bash
mkdir data
mkdir logs
```
Copy the example `config.example.yaml` to `data/config.yaml`, and assign the necessary config keys. Below is breakdown of the config.yaml layout:
```yaml
"GUILD_ID":
  channel: # The ID of the channel to which users connect, to create a new channel
  category: # The ID of the category in which new channels will be created
```
The `logs` directory is used to store logs in a format similar to that of a Minecraft server. `latest.log` will contain the log for the current day and current execution. All past logs are archived.

The `data` directory is used to store persistent state of the bot, such as config values and the infraction database.

### Launch Echo
To launch Echo, simply run the following commands:
```bash
sudo docker-compose build
sudo docker-compose up --detach
```

## Updating Echo
To update Echo, simply pull the latest changes from the repo and restart the container:
```bash
git pull
sudo docker-compose stop
sudo docker-compose build
sudo docker-compose up --detach
```

## License
This bot is under the [MIT License](LICENSE.md).

## Disclaimer
This bot is tailored for use within the [Brackeys Discord server](https://discord.gg/brackeys). While this bot is open source and you are free to use it in your own servers, you accept responsibility for any mishaps which may arise from the use of this software. Use at your own risk.
