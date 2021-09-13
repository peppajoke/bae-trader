# bae-trader
Bae-trader is an all purpose investment manager. Currently, bae-trader supports algorithmic stock trading via Alpaca Markets API, and crypto trading via Binance API.

# How to get started
1. Create a free account on alpaca: https://app.alpaca.markets/
2. Set up a paper (simulation environment) and/or a live account.
3. Clone this repository on your local machine.
3. Generate API keys for each environment on the alpaca website, and insert them into your local alpaca configurations within bae-trader
here: https://github.com/peppajoke/bae-trader/blob/master/stonksettings.paper.json and here https://github.com/peppajoke/bae-trader/blob/master/stonksettings.live.json
4. Run the program in a terminal and start trading.

`dotnet build`

`dotnet run paper` to run the CLI in a paper environment

`dotnet run live` to run the CLI in a real stock trading environment

# Bae-trader commands
Once you run bae-trader, you can execute several commands for interacting with your target market.

## Autoinvest
`autoinvest` or `auto`

The autoinvest command will wake up and make trades every n minutes (configured in json settings) in the environment specified.

## Crypto
`crypto`

Use the crypto command to allow bae-trader to buy and sell crypto currencies through the Binance exchange.

# Running Bae-trader with a single command
To make things simple, you can also specify the starting command for Bae-trader. This will allow you to enable autonomous trading as soon as you launch Bae-trader. For example...

`dotnet run -- paper auto` to run an "on paper" stock trader
or
`dotnet run -- live crypto` to run a live crypto trader


# Configuring Bae-trader

Too lazy to fill this out. For now, just take a look at the json configs and figure out how to configure bae-trader to work how you want.
