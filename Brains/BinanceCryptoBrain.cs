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
                    Console.WriteLine("Failed to listen for trade updates.");
                    return;
                }
                else
                {
                    Console.WriteLine("Listening for price updates for " + symbol);
                }
            }

            PlaceMissingOrders();
            while(true)
            {
                Console.ReadLine();
            }
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
                var percentChange = (_coinPriceDeltas[symbol] / _coinPrices[symbol])*100;

                var buys = _orders[symbol.Replace("USD", "")].Where(x=> x.Side == OrderSide.Buy && x.Status == OrderStatus.New);
                var sells = _orders[symbol.Replace("USD", "")].Where(x=> x.Side == OrderSide.Sell && x.Status == OrderStatus.New);
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

                Console.WriteLine(symbol+ ": $" + _coinPrices[symbol] + " " + Math.Round(percentChange, 2) + "% Sell @ $" + sellThresh + " Buy @ $" + buyThresh);
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
                    SmartBuy(data.Symbol);
                }
                else
                {
                    SmartSell(data.Symbol);
                }
            }
        }

        private async void SmartBuy(string symbol)
        {
            var targetSpend = _assets.Cash * .3M;
            var targetPrice = _coinPrices[symbol] * .98M;
            var targetQuantity = targetSpend / targetPrice;
            Console.WriteLine("AGGRESSIVE BUY TIME... " + symbol + " price: " + targetPrice + " targetQuantity: " + targetQuantity);
            await BuyCoin(symbol, targetQuantity, targetPrice, TimeInForce.GoodTillCancel);
        }

        private async void SmartSell(string symbol)
        {
            // set up a more aggressive sell
            var targetSellOffQuantity = _assets.Coins[symbol.Replace("USD", "")] * .2M;
            var targetPrice = _coinPrices[symbol] * 1.02M;
            Console.WriteLine("AGGRESSIVE SELL TIME... " + symbol + " price: " + targetPrice + " targetQuantity: " + targetSellOffQuantity);
            await SellCoin(symbol,  targetSellOffQuantity, targetPrice, TimeInForce.GoodTillCancel);
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
            foreach (var coin in _assets.Coins)
            {
                var orders = await _client.Spot.Order.GetOrdersAsync(coin.Key + "USD");
                _orders[coin.Key] = orders.Data;
                var price = _coinPrices[coin.Key];
                if (orders.Data.Where(x=> x.Status == OrderStatus.New).Any())
                {
                    Console.WriteLine(coin.Key + " price: " + price);
                    Console.WriteLine(coin.Key + " orders...");
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
                var openBuys = _orders[symbol].Where(x=> x.Status == OrderStatus.New && x.Side == OrderSide.Buy);
                var openSells = _orders[symbol].Where(x=> x.Status == OrderStatus.New && x.Side == OrderSide.Sell);
                if (!openBuys.Any())
                {
                    SmartBuy(symbol);
                }
                if (!openSells.Any())
                {
                    SmartSell(symbol);
                }
            }
        }

        private async Task<bool> BuyCoin(string symbol, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            if (_assets.Cash < price * quantity || quantity * price < 10)
            {
                Console.WriteLine("Not enough cash to invest in buys!");
                return false;
            }
            var response = await _client.Spot.Order.PlaceOrderAsync(symbol+"USD", OrderSide.Buy, OrderType.Limit, quantity: Math.Round(quantity, 1), price: Math.Round(price, 2), timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed buy order for " + symbol + "x" + quantity + " @ $" + price);
                return true;
            }
            Console.WriteLine("Buy order failed!");
            Console.WriteLine(response.Error);
            return false;
        }

        private async Task<bool> SellCoin(string symbol, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            var response = await _client.Spot.Order.PlaceOrderAsync(symbol+"USD", OrderSide.Sell, OrderType.Limit, quantity: Math.Round(quantity, 1), price: Math.Round(price, 2), timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed sell order for " + symbol + "x" + quantity + " @ $" + price);
                return true;
            }
            Console.WriteLine("Sell order failed: " + symbol + "x" + quantity + " @ $" + price);
            Console.WriteLine(response.Error);
            return false;
        }

        public async Task Trade()
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
    }
}