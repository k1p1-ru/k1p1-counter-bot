# k1p1-counter-bot
A knitting row counter Telegram bot

## Features:

- Adding new counters (up to 10 active)
  
- Archiving old ones

## Deploying

1. Clone this repository
```
git clone https://github.com/k1p1-ru/k1p1-counter-bot
```
2. Obtain a telegram bot token from [BotFather](https://t.me/botfather)
3. Set the bot token in docker-compose:
```
environment:
      BOT_TOKEN: "👉 here"
```
4. Launch (flags are useful after update via `git pull`)
```
docker-compose up -d --build --force-recreate
```
