# bae-trader
Bae-trader is an all purpose investment manager. Currently, bae-trader supports algorithmic stock trading via Alpaca Markets API, and crypto trading via Binance API.

# Required Software
1. Download and install the .NET core 5 runtime on your computer: https://dotnet.microsoft.com/download

# How to get started with stock trading
1. Create a free account on alpaca: https://app.alpaca.markets/
2. Set up a paper (simulation environment) and/or a live account.
3. Clone this repository on your local machine.
3. Generate API keys for each environment on the alpaca website. Put these keys somewhere safe.
4. Run `dotnet run -- paper auto` to configure your "just on paper environment, or `dotnet run -- live auto` to configure your real world stock trading environment!

# Running the app
To start bae-trader, you just need to navigate to the bae-trader directory in a terminal and run one of the following commands.

`dotnet run paper` to run the CLI in a paper environment

`dotnet run live` to run the CLI in a real stock trading environment

# Bae-trader commands
Once you run bae-trader, you can execute several commands for interacting with your target market.

## Command: Autoinvest
`autoinvest` or `auto`

The autoinvest command automatically manage your stock portfolio on Alpaca Markets.

## Command: Crypto
`crypto`

Use the crypto command to allow bae-trader to buy and sell crypto currencies through the Binance exchange.

# Running Bae-trader with a single command
To make things simple, you can also specify the starting command for Bae-trader. This will allow you to enable autonomous trading as soon as you launch Bae-trader. For example...

`dotnet run -- paper auto` to run an "on paper" stock trader
or
`dotnet run -- live crypto` to run a live crypto trader


# Configuring Bae-trader

Too lazy to fill this out. For now, just take a look at the json configs and figure out how to configure bae-trader to work how you want.
