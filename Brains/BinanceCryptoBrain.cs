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

        private ConcurrentDictionary<string, decimal> _coinPrices = new ConcurrentDictionary<string, decimal>();
        private ConcurrentDictionary<string, IEnumerable<BinanceOrder>> _orders = new ConcurrentDictionary<string, IEnumerable<BinanceOrder>>();

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

            var tradeResult = await _socketClient.Spot.SubscribeToTradeUpdatesAsync("BNBUSD",
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
                Console.WriteLine("Listening for trade updates...");
                while(true)
                {
                    Console.ReadLine();
                }
            }

        }

        private async void SocketTrade(BinanceStreamTrade trade)
        {
            //Console.WriteLine(JsonSerializer.Serialize(trade));
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
                    var targetSpend = _assets.Cash * .1M;
                    var targetPrice = (data.Price / data.Quantity) * .98M;
                    var targetQuantity = targetPrice / targetSpend;
                    //data.
                    Console.WriteLine("AGGRESSIVE BUY TIME... " + data.Symbol + " price: " + targetPrice + " targetQuantity: " + targetQuantity);
                    //await BuyCoin(data.Symbol, targetQuantity, targetPrice, TimeInForce.GoodTillCancel);
                }
                else
                {
                    // set up a more aggressive sell
                    var targetSellOffQuantity = _assets.Coins[data.Symbol] * .2M;
                    var targetPrice = (data.Price / data.Quantity) * 1.02M;
                    //var targetQuantity = targetPrice / targetSpend;
                    //data.
                    Console.WriteLine("AGGRESSIVE SELL TIME... " + data.Symbol + " price: " + targetPrice + " targetQuantity: " + targetSellOffQuantity);
                }
            }
            // is buy or sell?
        }

        private async void SocketOrdersUpdate(BinanceStreamOrderList data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
        }

        private async void SocketAccountPosition(BinanceStreamPositionsUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
        }

        private async void SocketAccountBalance(BinanceStreamBalanceUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
        }

        private async Task RefreshMarketPrices()
        {
            var priceCheckResult = await _client.Spot.Market.GetPricesAsync();
            foreach (var price in priceCheckResult.Data)
            {
                _coinPrices[price.Symbol] = price.Price;
            }

             Console.WriteLine("Refreshed market prices!");
        }

        private async Task RefreshOrders()
        {
            foreach (var coin in _assets.Coins)
            {
                var orders = await _client.Spot.Order.GetOrdersAsync(coin.Key + "USD");
                _orders[coin.Key] = orders.Data;
                var price = _coinPrices[coin.Key+"USD"];
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

        private async Task<bool> BuyCoin(string symbol, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            var response = await _client.Spot.Order.PlaceOrderAsync(symbol, OrderSide.Buy, OrderType.Limit, quantity: quantity, price: price, timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed buy order for " + symbol + "x" + quantity + " @ $" + price);
                return true;
            }
            Console.WriteLine("Buy order failed!");
            Console.WriteLine(response.Data);
            return false;
        }

        private async Task<bool> SellCoin(string symbol, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            var response = await _client.Spot.Order.PlaceOrderAsync(symbol, OrderSide.Sell, OrderType.Limit, quantity: quantity, price: price, timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed sell order for " + symbol + "x" + quantity + " @ $" + price);
                return true;
            }
            Console.WriteLine("Sell order failed!");
            Console.WriteLine(response.Data);
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