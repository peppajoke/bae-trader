# bae-trader
Stockbot for getting rich

# How to get started
1. Create a free account on alpaca: https://app.alpaca.markets/
2. Set up a paper (simulation environment) and a live account.
3. Clone this repository on your local machine.
3. Generate API keys for each environment on the alpaca website, and insert them into your local alpaca configuration within bae-trader
here: https://github.com/peppajoke/bae-trader/blob/master/Configuration/AlpacaCredentials.cs
4. Run the program in a terminal and start trading.

dotnet build

dotnet run

autoinvest

The autoinvest command will wake up and make trades every 5 minutes in the environment specified.

autoinvest optional arguments:
-c chaos mode. Adds some randomness to your investment strategy so that it's diverse between runs
-b=100 budget=100, lets you specify how much money to invest at most
-real invests in the real world. By default, bae-trader will run simulations against a "paper" stock environment
