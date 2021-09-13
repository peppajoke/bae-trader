using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using bae_trader.Configuration;
using bae_trader.SavedDtos;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Spot.MarketStream;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using CryptoExchange.Net.Authentication;

namespace bae_trader.Brains
{
    public class BinanceCryptoBrain : ICryptoBrain
    {
        private readonly BinanceClient _client;
        private readonly BinanceSocketClient _socketClient;

        private BinanceAssets _assets;

        private HashSet<string> _approvedSymbols = new HashSet<string>();

        private ConcurrentDictionary<string, decimal> _coinPrices = new ConcurrentDictionary<string, decimal>();
        private ConcurrentDictionary<string, IEnumerable<BinanceOrder>> _orders = new ConcurrentDictionary<string, IEnumerable<BinanceOrder>>();

        private ConcurrentDictionary<string, decimal> _coinPriceDeltas = new ConcurrentDictionary<string, decimal>();

        //private HashSet<string> _liquidatedCurrencies = new HashSet<string>();

        public BinanceCryptoBrain(CryptoConfig config)
        {
            _client = new BinanceClient(new BinanceClientOptions() 
            { 
                ApiCredentials = new ApiCredentials(config.BinanceAPIKey, config.BinanceSecret),
                BaseAddress = "https://api.binance.us/",
                AutoTimestamp = true
            });

            _socketClient = new BinanceSocketClient(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(config.BinanceAPIKey, config.BinanceSecret),
                BaseAddress = "wss://stream.binance.us:9443/",
                AutoReconnect = true,
                ReconnectInterval = TimeSpan.FromSeconds(15)
            });
            
            _approvedSymbols = new HashSet<string>(config.AutotradeCoins);
        }


        private async Task StartSockets()
        {
            var listenKeyResultAccount = await _client.Spot.UserStream.StartUserStreamAsync();
            if(!listenKeyResultAccount.Success)
            {
                Console.WriteLine("Failed to start user session.");
                return;
            }

            var subscribeResult = await _socketClient.Spot.SubscribeToUserDataUpdatesAsync(listenKeyResultAccount.Data, 
            data => 
            {
                SocketOrderUpdate(data.Data);
            },
            data => 
            {
                SocketOrdersUpdate(data.Data);
            },
            data => 
            { 
                SocketAccountPosition(data.Data);
            },
            data =>
            {
                SocketAccountBalance(data.Data);
            });

            if (!subscribeResult.Success)
            {
                Console.WriteLine("Failed to listen for account updates.");
                return;
            }
            else
            {
                Console.WriteLine("Listening for account updates...");
            }

            foreach(var symbol in _approvedSymbols)
            {
                var tradeResult = await _socketClient.Spot.SubscribeToTradeUpdatesAsync(symbol + "USD",
                data => 
                {
                    SocketTrade(data.Data);
                });
                if (!tradeResult.Success)
                {
                    Console.WriteLine("Failed to listen for trade updates for " + symbol);
                    return;
                }
                else
                {
                    Console.WriteLine("Listening for price updates for " + symbol);
                }
            }

            await PlaceMissingOrders();
        }

        private async void SocketTrade(BinanceStreamTrade trade)
        {
            var symbol = trade.Symbol.Replace("USD", "");
            if (!_coinPriceDeltas.ContainsKey(symbol))
            {
                _coinPriceDeltas[symbol] = 0;
            }
            _coinPriceDeltas[symbol] += trade.Price - _coinPrices[symbol];
            _coinPrices[symbol] = trade.Price;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            Console.Clear();
            foreach(var delta in _coinPriceDeltas)
            {
                var symbol = delta.Key;
                if (!_orders.ContainsKey(symbol))
                {
                    continue;
                }
                Console.ResetColor();
                var percentChange = (_coinPriceDeltas[symbol] / _coinPrices[symbol])*100;

                var buys = _orders[symbol].Where(x=> x.Side == OrderSide.Buy && x.Status == OrderStatus.New);
                var sells = _orders[symbol].Where(x=> x.Side == OrderSide.Sell && x.Status == OrderStatus.New);
                var buyThresh = "none";
                var sellThresh = "none";

                if (buys.Any())
                {
                    buyThresh = buys.First().Price.ToString();
                }

                if (sells.Any())
                {
                    sellThresh = sells.First().Price.ToString();
                }
                if (sellThresh != "none" && Convert.ToDecimal(sellThresh) <= _coinPrices[symbol] )
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                }
                else if (buyThresh != "none" && Convert.ToDecimal(buyThresh) >= _coinPrices[symbol] )
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                else if (percentChange > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else if (percentChange < 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                
                Console.WriteLine(symbol+ ": $" + _coinPrices[symbol] + " Delta: " + Math.Round(percentChange, 2) + "% Sell @ $" + sellThresh + " Buy @ $" + buyThresh);
                
                if (sellThresh != "none" )
                {
                    var blocksToSell = _coinPriceDeltas[symbol] / (Convert.ToDecimal(sellThresh) - (_coinPrices[symbol] - _coinPriceDeltas[symbol]));
                    for(var i=0;i<=10;i++)
                    {
                        if (blocksToSell > 0)
                        {
                            Console.Write("-");
                            blocksToSell--;
                        }
                        else
                        {
                            Console.Write(" ");
                        }
                    }
                    Console.Write("Sell");
                    Console.WriteLine("");
                }

                if (buyThresh != "none" && _coinPriceDeltas[symbol] > 0)
                {
                    var blocksToBuy = (1-((_coinPrices[symbol] - _coinPriceDeltas[symbol]) / (_coinPrices[symbol] - Convert.ToDecimal(buyThresh))))*10;
                    for(var i=0;i<=10;i++)
                    {
                        if (blocksToBuy > 0)
                        {
                            Console.Write("-");
                            blocksToBuy--;
                        }
                        else
                        {
                            Console.Write(" ");
                        }
                    }
                    Console.Write("Buy");
                    Console.WriteLine("");
                }
            }
        }

        private async void SocketOrderUpdate(BinanceStreamOrderUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            if (data.Type == OrderType.Limit && data.Status == OrderStatus.Filled)
            {
                // todo remove this and just update order state
                await RefreshHeldAssets();
                if (data.Side == OrderSide.Buy)
                {
                    SmartBuy(data.Symbol.Replace("USD", ""));
                }
                else
                {
                    SmartSell(data.Symbol.Replace("USD", ""));
                }
            }
            await PlaceMissingOrders();
        }

        private async void SmartBuy(string symbol)
        {
            var targetSpend = _assets.Cash * .1M;
            if (targetSpend < 20M)
            {
                return;
            }
            var targetPrice = _coinPrices[symbol] * .98M;
            var targetQuantity = targetSpend / targetPrice;
            Console.WriteLine("AGGRESSIVE BUY TIME... " + symbol + " price: " + targetPrice + " targetQuantity: " + targetQuantity);
            await TradeCoin(symbol, targetQuantity, targetPrice, TimeInForce.GoodTillCancel, OrderSide.Buy);
        }

        private async void SmartSell(string symbol)
        {
            var targetPrice = _coinPrices[symbol] * 1.02M;
            if (_assets.Coins[symbol] * targetPrice < 40M)
            {
                return;
            }
            // set up a more aggressive sell
            var targetSellOffQuantity = _assets.Coins[symbol] * .5M;
            await TradeCoin(symbol,  targetSellOffQuantity, targetPrice, TimeInForce.GoodTillCancel, OrderSide.Sell);
        }

        private async void SocketOrdersUpdate(BinanceStreamOrderList data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            await PlaceMissingOrders();
        }

        private async void SocketAccountPosition(BinanceStreamPositionsUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            await PlaceMissingOrders();
        }

        private async void SocketAccountBalance(BinanceStreamBalanceUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            await PlaceMissingOrders();
        }

        private async Task RefreshMarketPrices()
        {
            var priceCheckResult = await _client.Spot.Market.GetPricesAsync();
            foreach (var price in priceCheckResult.Data)
            {
                _coinPrices[price.Symbol.Replace("USD", "")] = price.Price;
            }

            Console.WriteLine("Refreshed market prices!");
        }

        private async Task RefreshOrders()
        {
            foreach (var symbol in _approvedSymbols)
            {
                var orders = await _client.Spot.Order.GetOrdersAsync(symbol + "USD");
                _orders[symbol] = orders.Data ?? new List<BinanceOrder>();
                var price = _coinPrices[symbol];
                if (orders.Data.Where(x=> x.Status == OrderStatus.New).Any())
                {
                    Console.WriteLine(symbol + " price: " + price);
                    Console.WriteLine(symbol + " orders...");
                }
                foreach(var order in orders.Data)
                {
                    if (order.Status == OrderStatus.New)
                    {
                        Console.WriteLine("Open order: " + order.Side + " $" + order.Price + " " + order.Quantity);
                    }
                }
            }
        }

        private async Task RefreshHeldAssets()
        {
            var assets = new BinanceAssets();
            assets.Coins = new Dictionary<string,decimal>();
            assets.Cash = 0;
            var account = await _client.General.GetAccountInfoAsync();

             foreach (var balance in account.Data.Balances.Where(x=> x.Free > 0))
             {
                 if (balance.Asset == "USD")
                 {
                     assets.Cash = balance.Free;
                 }
                 else
                 {
                     assets.Coins.Add(balance.Asset, balance.Free);
                 }
             }
             _assets = assets;
             Console.WriteLine("Refreshed held assets!");
        }

        private async Task PlaceMissingOrders()
        {
            await RefreshOrders();
            foreach(var symbol in _approvedSymbols)
            {
                if (!_orders.ContainsKey(symbol))
                {
                    continue;
                }
                var openBuys = _orders[symbol].Where(x=> x.Status == OrderStatus.New && x.Side == OrderSide.Buy);
                var openSells = _orders[symbol].Where(x=> x.Status == OrderStatus.New && x.Side == OrderSide.Sell);
                if (!openBuys.Any())
                {
                    SmartBuy(symbol);
                }
                if (!openSells.Any())
                {
                    if (!_assets.Coins.ContainsKey(symbol))
                    {
                        _assets.Coins[symbol] = 0M;
                    }
                    SmartSell(symbol);
                }
            }
        }

        private async Task<bool> TradeCoin(string symbol, decimal quantity, decimal price, TimeInForce timeInForce, OrderSide side)
        {
            if (side == OrderSide.Buy && _assets.Cash < price * quantity || quantity * price < 10)
            {
                Console.WriteLine("Not enough cash to invest in buys!");
                return false;
            }
            var response = await _client.Spot.Order.PlaceOrderAsync(symbol+"USD", side, OrderType.Limit, quantity: Math.Round(quantity, 1), price: Math.Round(price, 2), timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed " + side + " order for " + symbol + "x" + quantity + " @ $" + price);
                return true;
            }
            Console.WriteLine(side + " order failed: " + symbol + "x" + quantity + " @ $" + price);
            Console.WriteLine(response.Error);
            return false;
        }

        public async Task Trade()
        {
            try
            {
                await Task.WhenAll(
                    RefreshMarketPrices(),
                    RefreshHeldAssets()
                );

                // Orders need to know our assets first.
                await RefreshOrders();

                // Socket time
                await StartSockets();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Trade();
            }
        }
    }
}