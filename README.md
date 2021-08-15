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

The autoinvest command will wake up and make trades every 5 minutes in the environment specified.

autoinvest optional arguments:

`-c chaos mode. Adds some randomness to your investment strategy so that it's diverse between runs`

`-b=100 budget=100, lets you specify how much money to invest at most, in dollars`

`-real invests in the real world. By default, bae-trader will run simulations against a "paper" stock environment`

You can also auto-start autoinvesting with a single command from dotnet:

`dotnet run -- paper auto`
