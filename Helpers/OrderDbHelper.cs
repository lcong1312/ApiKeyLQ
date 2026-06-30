using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ApiKey.Helpers
{
    public class PayosOrder
    {
        public long OrderCode { get; set; }
        public string Email { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Days { get; set; }
        public int Amount { get; set; }
        public string Status { get; set; } // "Pending", "PAID", "CANCELLED"
        public string GeneratedApiKey { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class OrderDbHelper
    {
        private static readonly string OrdersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "orders.json");
        private static readonly object LockObj = new object();

        public static List<PayosOrder> Read()
        {
            lock (LockObj)
            {
                if (!File.Exists(OrdersPath))
                {
                    string dir = Path.GetDirectoryName(OrdersPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    var list = new List<PayosOrder>();
                    File.WriteAllText(OrdersPath, JsonConvert.SerializeObject(list, Formatting.Indented));
                    return list;
                }

                try
                {
                    string json = File.ReadAllText(OrdersPath);
                    return JsonConvert.DeserializeObject<List<PayosOrder>>(json) ?? new List<PayosOrder>();
                }
                catch
                {
                    return new List<PayosOrder>();
                }
            }
        }

        public static void Write(List<PayosOrder> orders)
        {
            lock (LockObj)
            {
                File.WriteAllText(OrdersPath, JsonConvert.SerializeObject(orders, Formatting.Indented));
            }
        }

        public static void Add(PayosOrder order)
        {
            lock (LockObj)
            {
                var orders = Read();
                orders.Add(order);
                Write(orders);
            }
        }

        public static PayosOrder Get(long orderCode)
        {
            var orders = Read();
            return orders.Find(o => o.OrderCode == orderCode);
        }

        public static void Update(long orderCode, string status, string generatedKey = null)
        {
            lock (LockObj)
            {
                var orders = Read();
                var order = orders.Find(o => o.OrderCode == orderCode);
                if (order != null)
                {
                    order.Status = status;
                    if (generatedKey != null)
                    {
                        order.GeneratedApiKey = generatedKey;
                    }
                    Write(orders);
                }
            }
        }
    }
}
