using System;
using System.Collections.Generic;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldNetworkState
    {
        private readonly object sync = new object();

        public DateTime? LastOrdersRefreshUtc { get; private set; }
        public DateTime? LastResultsRefreshUtc { get; private set; }
        public DateTime? LastTerritoriesRefreshUtc { get; private set; }
        public DateTime? LastPlayerStatusRefreshUtc { get; private set; }

        public List<UnderworldOrderDto> AvailableOrders { get; private set; } = new List<UnderworldOrderDto>();
        public List<UnderworldShipmentResultDto> ShipmentResults { get; private set; } = new List<UnderworldShipmentResultDto>();
        public List<UnderworldTerritoryDto> Territories { get; private set; } = new List<UnderworldTerritoryDto>();
        public UnderworldShipmentResultsResponse LastResultsResponse { get; private set; }
        public UnderworldPlayerStatusDto PlayerStatus { get; private set; }

        public void SetAvailableOrders(List<UnderworldOrderDto> orders)
        {
            lock (sync)
            {
                AvailableOrders = orders ?? new List<UnderworldOrderDto>();
                LastOrdersRefreshUtc = DateTime.UtcNow;
            }
        }

        public void RemoveAcceptedOrder(int orderId)
        {
            lock (sync)
            {
                AvailableOrders = AvailableOrders.Where(order => order == null || order.Id != orderId).ToList();
            }
        }

        public void AddShipmentResult(UnderworldShipmentResultDto result)
        {
            if (result == null)
            {
                return;
            }

            lock (sync)
            {
                ShipmentResults.Insert(0, result);
                LastResultsRefreshUtc = DateTime.UtcNow;
            }
        }

        public void SetResults(UnderworldShipmentResultsResponse response)
        {
            lock (sync)
            {
                LastResultsResponse = response;
                ShipmentResults = response?.Results ?? new List<UnderworldShipmentResultDto>();
                LastResultsRefreshUtc = DateTime.UtcNow;
            }
        }

        public void SetTerritories(List<UnderworldTerritoryDto> territories)
        {
            lock (sync)
            {
                Territories = territories ?? new List<UnderworldTerritoryDto>();
                LastTerritoriesRefreshUtc = DateTime.UtcNow;
            }
        }

        public void SetPlayerStatus(UnderworldPlayerStatusDto status)
        {
            lock (sync)
            {
                PlayerStatus = status;
                LastPlayerStatusRefreshUtc = DateTime.UtcNow;
            }
        }
    }
}
