using System;
using System.Collections.Generic;
using System.IO;

namespace bae_trader.SavedDtos
{
    public class CryptoPurchase
    {

        public string Currency { get;set; }
        public decimal Quantity { get;set; }
        public decimal TotalCost { get;set; }

        
        private CryptoPurchase(string fileLine)
        {
            var pieces = fileLine.Split(":");
            Currency = pieces[0];
            Quantity = Convert.ToDecimal(pieces[2]);
            TotalCost = Convert.ToDecimal(pieces[3]);
        }

        public async void WriteToFile()
        {
            using StreamWriter file = new("cryptobuy.log", append: true);
            //var transactionID = _buy.Transaction is null ? "fake_trans" : _buy.Transaction.Id;
            //await file.WriteLineAsync(_buy.Amount.Currency + ":" + transactionID + ":" + _buy.Amount.Amount + ":" + _buy.Total.Amount + ":" + _buy.Total.Currency);
        }

        public static IEnumerable<CryptoPurchase> LoadAllFromDisk()
        {
            using StreamReader file = new("cryptobuy.log");
            string line;
            var purchases = new List<CryptoPurchase>();
            while ((line = file.ReadLine()) != null)
            {
                purchases.Add(new CryptoPurchase(line));
            }
            return purchases;
        }
    }
}