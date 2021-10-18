using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using bae_trader.Configuration;
using bae_trader.SavedDtos;
using bae_trader.Services;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Spot.MarketData;
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

        private ConcurrentDictionary<string, decimal> _coinPriceDeltas = new ConcurrentDictionary<string, decimal>();
        private CryptoConfig _config;
        private ICryptoBuyService _buyService;
        private ICryptoSellService _sellService;
        
        private Dictionary<string, BinanceSymbol> _symbols = new Dictionary<string, BinanceSymbol>();

        private HashSet<string> _approvedBuys = new HashSet<string>();

        private HashSet<string> _openBuySymbols = new HashSet<string>();

        private Dictionary<string,decimal> _currentDollarsInvestedByCoin = new Dictionary<string, decimal>();
        private Dictionary<string,decimal> _totalDollarsInvestedByCoin = new Dictionary<string, decimal>();

        private Dictionary<string,decimal> _totalDollarsSoldByCoin = new Dictionary<string, decimal>();

        private Dictionary<string,decimal> _costBasis = new Dictionary<string, decimal>();

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
            _config = config;
            _buyService = new SmartCryptoBuyService();
            _sellService = new SmartCryptoSellService();
        }

        private decimal GetProfitableSaleRate(string symbol)
        {
            try
            {
                return (_costBasis[symbol] / _assets.Coins[symbol]) * (1 + (_config.SellPercentThreshold/100M));
            }
            catch(Exception ex)
            {
                return -1;
            }
        }

        private async Task StartSockets()
        {
            var listenKeyResultAccount = await _client.Spot.UserStream.StartUserStreamAsync();

            if(!listenKeyResultAccount.Success)
            {
                Console.WriteLine("Failed to start user session.");
                return;
            }

            var exchangeInfo = await _client.Spot.System.GetExchangeInfoAsync();

            var allNewCoins = exchangeInfo.Data.Symbols.Where(
                x => !_config.AllUsedSymbols.Contains(x.BaseAsset) && x.QuoteAsset == "USD" && !x.BaseAsset.Contains("USD")).Select(x => x.BaseAsset);


            Console.WriteLine("Subscribing to updates...");
            var subscribeResult = await _socketClient.Spot.SubscribeToUserDataUpdatesAsync(listenKeyResultAccount.Data, 
            data => 
            {
                try {
                    SocketOrderUpdate(data.Data);
                } catch (Exception ex) {
                    throw;
                }
                
            },
            data => 
            {
                try {
                    SocketOrdersUpdate(data.Data);
                } catch (Exception ex) {
                    throw;
                }
            },
            data => 
            { 
                try {
                    SocketAccountPosition(data.Data);
                } catch (Exception ex) {
                    throw;
                }
            },
            data =>
            {
                try {
                    SocketAccountBalance(data.Data);
                } catch (Exception ex) {
                    throw;
                }
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



            Console.WriteLine("Placing missing orders...");

            Console.WriteLine("Setting up sockets...");
            var tradeUpdateTasks = new List<Task>();
            foreach(var symbol in _config.AllUsedSymbols)
            {
                tradeUpdateTasks.Add(_socketClient.Spot.SubscribeToTradeUpdatesAsync(symbol + "USD",
                data => 
                {
                    SocketTrade(data.Data);
                }));
            }

            // Set up experimental trades
            if (_config.AutoTradeNewCoins)
            {
                foreach (var experimentalSymbol in allNewCoins)
                {
                    tradeUpdateTasks.Add(_socketClient.Spot.SubscribeToTradeUpdatesAsync(experimentalSymbol + "USD",
                    data => 
                    {
                        SocketTrade(data.Data);
                    }));
                }
            }

            await Task.WhenAll(tradeUpdateTasks);

            Console.WriteLine("Listening for coins to buy...");
            ListenForBuys();
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
            if (!_openBuySymbols.Contains(symbol))
            {
                SmartBuy(symbol);
            }
        }

        private void UpdateDisplay()
        {
            Console.Clear();
            foreach(var delta in _coinPriceDeltas)
            {
                var symbol = delta.Key;
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
            }
        }

        private async void SocketOrderUpdate(BinanceStreamOrderUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            if (data.Type == OrderType.Limit && data.Status == OrderStatus.Filled)
            {
                // todo remove this and just update order state
                await RefreshHeldAssets();
                await RefreshOrders();
                await RefreshCostBasis();
                if (data.Side == OrderSide.Buy)
                {
                    _buyService.ConsiderBuying(data.Symbol);
                    lock(_openBuySymbols)
                    {
                        _openBuySymbols.Add(data.Symbol);
                    }
                }
                else
                {
                    _sellService.ConsiderBuying(data.Symbol);
                    SmartSell(data.Symbol.Replace("USD", ""));
                }
            }
            //await PlaceMissingOrders();
        }

        private async void SmartBuy(string symbol)
        {
            if (!IsReadyForBuy(symbol))
            {
                return;
            }

            lock(_approvedBuys)
            {
                _approvedBuys.Add(symbol);
            }
        }

        private void ListenForBuys()
        {
            Task.Factory.StartNew(async() => 
            {
                var cycles = 0;
                while(true)
                {
                    var processedSymbols = new HashSet<string>();
                    foreach(var symbol in _approvedBuys)
                    {
                        if (!IsReadyForBuy(symbol))
                        {
                            processedSymbols.Add(symbol);
                            continue;
                        }
                        var targetSpend = _assets.Cash * await _buyService.GetPercentBuy(symbol);
                        if (targetSpend < 20M && _assets.Cash > 20M)
                        {
                            targetSpend = 20M;
                        }
                        if (targetSpend < 20M)
                        {
                            continue;
                        }
                        var targetPrice = _coinPrices[symbol] * (.995M);
                        var targetQuantity = targetSpend / targetPrice;
                        Console.WriteLine("Putting in a buy order for " + symbol + " price: " + targetPrice + " targetQuantity: " + targetQuantity);

                        var success = await TradeCoin(symbol, targetQuantity, targetPrice, TimeInForce.GoodTillCancel, OrderSide.Buy);
                        processedSymbols.Add(symbol);

                        _sellService.ResetSymbol(symbol);

                    }
                    await Task.Delay(100);
                    lock(_approvedBuys)
                    {
                        foreach(var symbol in processedSymbols)
                        {
                            _approvedBuys.Remove(symbol);
                        }
                    }
                    cycles++;

                    if (cycles > 100000)
                    {
                        await RefreshHeldAssets();
                        await RefreshOrders();
                        await Liquidate();
                        await PlaceMissingOrders();
                        await RefreshHeldAssets();
                        await RefreshOrders();
                        cycles = 0;
                    }
                }
            });
        }
        private bool IsReadyForBuy(string symbol)
        {
            if (!_config.AutoBuy.Contains(symbol) && !_config.AutoTradeNewCoins)
            {
                return false;
            }

            if (!_config.AutoBuy.Contains(symbol) && _config.AutoTradeNewCoins 
                && _totalDollarsInvestedByCoin.ContainsKey(symbol) && _totalDollarsSoldByCoin.ContainsKey(symbol))
            {
                var spent = _totalDollarsInvestedByCoin[symbol];
                var sold = _totalDollarsSoldByCoin[symbol];
                if (spent > 0M && spent > sold)
                {
                    return false;
                }
            }


            if (!_coinPriceDeltas.ContainsKey(symbol))
            {
                return false;
            }
            return _coinPriceDeltas[symbol] <= -_config.BuyPercentThreshold;
        }
        private async void SmartSell(string symbol)
        {
            if ((!_config.AutoSell.Contains(symbol) && _config.AutoBuy.Contains(symbol)) || !_coinPrices.ContainsKey(symbol))
            {
                return;
            }
            var targetPrice = Math.Max(GetProfitableSaleRate(symbol), _coinPrices[symbol] * (1M + _config.SellPercentThreshold/100M));

            // set up a more aggressive sell
            if (!_assets.Coins.ContainsKey(symbol))
            {
                return;
            }
            var targetSellOffQuantity = _assets.Coins[symbol] * await _sellService.GetPercentSell(symbol);
            
            if (targetSellOffQuantity * targetPrice < 10M)
            {
                return;
            }
            await TradeCoin(symbol,  targetSellOffQuantity, targetPrice, TimeInForce.GoodTillCancel, OrderSide.Sell);
            if (_assets.Coins[symbol] == targetSellOffQuantity)
            {
                _sellService.ResetSymbol(symbol);
            }
            _buyService.ResetSymbol(symbol);
        }

        private async void SocketOrdersUpdate(BinanceStreamOrderList data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            //await PlaceMissingOrders();
        }

        private async void SocketAccountPosition(BinanceStreamPositionsUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            //await PlaceMissingOrders();
        }

        private async void SocketAccountBalance(BinanceStreamBalanceUpdate data)
        {
            Console.WriteLine(JsonSerializer.Serialize(data));
            //await PlaceMissingOrders();
        }

        private async Task RefreshMarketPrices()
        {
            var priceCheckResult = await _client.Spot.Market.GetPricesAsync();
            if (!priceCheckResult.Success)
            {
                Console.WriteLine(priceCheckResult.Error);
                Console.WriteLine("Failed to fetch prices! Retrying...");
                await Task.Delay(5000);
                await RefreshMarketPrices();
                return;
            }
            foreach (var price in priceCheckResult.Data)
            {
                _coinPrices[price.Symbol.Replace("USD", "")] = price.Price;
            }

            Console.WriteLine("Refreshed market prices!");
        }

        private async Task RefreshOrders()
        {
            foreach (var symbol in _config.AllUsedSymbols)
            {
                var orders = await _client.Spot.Order.GetOrdersAsync(symbol + "USD");
                _orders[symbol] = orders.Data ?? new List<BinanceOrder>();
            }

            lock(_openBuySymbols)
            {
                _openBuySymbols.Clear();
                foreach (var order in _orders)
                {
                    if (order.Value.Any(x => x.Status == OrderStatus.New))
                    {
                        _openBuySymbols.Add(order.Key);
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
            foreach(var symbol in _config.AllUsedSymbols)
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

            var stepSize = _symbols[symbol].LotSizeFilter.StepSize;
            
            var quantityPrecision = 0;
            while(stepSize != 1)
            {
                quantityPrecision++;
                stepSize *= 10;
            }

            var priceStepSize = _symbols[symbol].PriceFilter.TickSize;
            
            var pricePrecision = 0;
            while(priceStepSize != 1)
            {
                pricePrecision++;
                priceStepSize *= 10;
            }

            var response = await _client.Spot.Order.PlaceOrderAsync(symbol+"USD", side, OrderType.Limit, quantity: Math.Round(quantity, quantityPrecision), 
                price: side == OrderSide.Buy ? Math.Round(price, pricePrecision) :  Math.Round(price, pricePrecision), timeInForce: timeInForce);
            if (response.Success)
            {
                Console.WriteLine("Successfully placed " + side + " order for " + symbol + "x" + quantity + " @ $" + price);
                _coinPriceDeltas[symbol] = 0;
                if (side == OrderSide.Buy)
                {
                    lock(_openBuySymbols)
                    {
                        _openBuySymbols.Add(symbol);
                    }
                }
                return true;
            }
            Console.WriteLine(side + " order failed: " + symbol + "x" + quantity + " @ $" + price);
            Console.WriteLine(response.Error);
            return false;
        }

        private async Task RefreshCostBasis()
        {

            var newAssets = new BinanceAssets();
            newAssets.Coins = new Dictionary<string,decimal>();

            foreach(var symbol in _orders)
            {
                newAssets.Coins[symbol.Key] = 0M;
                _currentDollarsInvestedByCoin[symbol.Key] = 0M;
                _totalDollarsInvestedByCoin[symbol.Key] = 0M;
                _totalDollarsSoldByCoin[symbol.Key] = 0M;

                _costBasis[symbol.Key] = 0M;

                var costBasisRemainingInCoins = _assets.Coins.ContainsKey(symbol.Key) ? _assets.Coins[symbol.Key] : 0;

                foreach(var order in _orders[symbol.Key].OrderByDescending(x => x.UpdateTime))
                {
                    switch(order.Status)
                    {
                        case OrderStatus.Expired:
                        case OrderStatus.Rejected:
                        case OrderStatus.PendingCancel:
                        case OrderStatus.Canceled:
                            if (order.Side == OrderSide.Buy)
                            {
                                // newAssets.Cash += order.Price;
                                // newAssets.Coins[symbol.Key] -= order.QuantityFilled;
                            }
                            else
                            {
                                // newAssets.Cash -= order.Price;
                                // newAssets.Coins[symbol.Key] += order.QuantityFilled;
                            }
                            break;
                        case OrderStatus.New:
                            if (order.Side == OrderSide.Buy)
                            {
                                newAssets.Cash -= order.QuoteQuantityFilled;
                                
                                // newAssets.Coins[symbol.Key] -= order.QuantityFilled;
                            }
                            else
                            {
                                // newAssets.Cash += order.Price;
                                // newAssets.Coins[symbol.Key] += order.QuantityFilled;
                            }
                            break;
                        case OrderStatus.Filled:
                            if (order.Side == OrderSide.Buy)
                            {
                                newAssets.Cash -= order.QuoteQuantityFilled;
                                newAssets.Coins[symbol.Key] += order.Quantity;
                                _currentDollarsInvestedByCoin[symbol.Key] += order.QuoteQuantityFilled;
                                _totalDollarsInvestedByCoin[symbol.Key] += order.QuoteQuantityFilled;
                                if (costBasisRemainingInCoins > 0)
                                {
                                    var percentOfOrder = Math.Min(costBasisRemainingInCoins / order.Quantity, 1);
                                    _costBasis[symbol.Key] += percentOfOrder * order.QuoteQuantityFilled;
                                    costBasisRemainingInCoins -= percentOfOrder * order.QuantityFilled;
                                }
                            }
                            else
                            {
                                newAssets.Cash += order.QuoteQuantityFilled;
                                newAssets.Coins[symbol.Key] -= order.Quantity;
                                _currentDollarsInvestedByCoin[symbol.Key] -= order.QuoteQuantityFilled;
                                _totalDollarsSoldByCoin[symbol.Key] += order.QuoteQuantityFilled;
                            }
                            break;
                    }
                }

                var dollarsSpent = _totalDollarsInvestedByCoin[symbol.Key] - _costBasis[symbol.Key];
                Console.WriteLine(symbol.Key + " total spend: $" + dollarsSpent + " total sold: $" + _totalDollarsSoldByCoin[symbol.Key]+ " profit: $" + (_totalDollarsSoldByCoin[symbol.Key] - dollarsSpent));
            }
        }

        private decimal GetTotalPortfolioValue()
        {
            var output = 0M;
            foreach (var coin in _assets.Coins)
            {
                var exchangeRate = _coinPrices[coin.Key];
                output += exchangeRate * coin.Value;
            }
            output += _assets.Cash;
            return output;
        }

        public async Task Liquidate()
        {
            if (_assets.Cash < GetTotalPortfolioValue() * .5M)
            {
                return;
            }
            Console.WriteLine("Attempting to profitably liquidate assets...");
            foreach (var symbol in _assets.Coins)
            {
                if (!_coinPrices.ContainsKey(symbol.Key) || !_costBasis.ContainsKey(symbol.Key))
                {
                    continue;
                }
                var coinValue = symbol.Value * _coinPrices[symbol.Key];
                var dollarDelta = coinValue - _costBasis[symbol.Key];
                decimal percentProfit;

                if (coinValue > _costBasis[symbol.Key])
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    percentProfit = coinValue / _costBasis[symbol.Key];
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    percentProfit = _costBasis[symbol.Key] / coinValue;
                }

                Console.WriteLine(symbol.Key + ": $" + dollarDelta + " " + percentProfit + "%");
                if (dollarDelta > 1)
                {
                    // sell everything
                    if (_config.AutoSell.Contains(symbol.Key))
                    {
                        await TradeCoin(symbol.Key,  _assets.Coins[symbol.Key], _coinPrices[symbol.Key], TimeInForce.GoodTillCancel, OrderSide.Sell);
                    }
                }
            }
        }

        private async Task RefreshSymbols()
        {
            var symbols = (await _client.Spot.System.GetExchangeInfoAsync()).Data.Symbols;
            foreach(var symbol in symbols)
            {
                if (symbol.QuoteAsset == "USD")
                {
                    _symbols.Add(symbol.Name.Replace("USD",""), symbol);
                }
            }
        }

        public async Task Trade(bool liquidate)
        {
            await Task.WhenAll(
                RefreshMarketPrices(),
                RefreshHeldAssets()
            );

            // Orders need to know our assets first.
            await RefreshSymbols();
            await RefreshOrders();
            await RefreshCostBasis();

            if (liquidate)
            {
                await Liquidate();
            }

            // Socket time
            await StartSockets();

            await PlaceMissingOrders();
        }
    }
}