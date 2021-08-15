# bae-trader
Stockbot for getting rich

# How to get started
1. Create a free account on alpaca: https://app.alpaca.markets/
2. Set up a paper (simulation environment) and a live account.
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

## Buy
`buy`

If you don't want Bae-trader buying autonomously, you can just ask it to buy stocks using the buy command. Bae-trader's buy settings will be pulled from your json configuration mentioned above.


## Sell
`sell`

If you don't want Bae-trader selling autonomously, you can just ask it to sell stocks using the sell command. Bae-trader's sell settings will be pulled from your json configuration mentioned above.

# Running Bae-trader with a single command
To make things simple, you can also specify the starting command for Bae-trader. This will allow you to enable autonomous trading as soon as you launch Bae-trader. For example...

`dotnet run -- paper auto`

This will automatically launch Bae-trader into auto-investing in the paper environment with a single command.
