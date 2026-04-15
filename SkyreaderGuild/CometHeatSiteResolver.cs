using UnityEngine;

namespace SkyreaderGuild
{
    internal static class CometHeatSiteResolver
    {
        internal readonly struct SiteInfo
        {
            public readonly string SiteId;
            public readonly string SiteName;
            public readonly int WorldX;
            public readonly int WorldY;

            public SiteInfo(string siteId, string siteName, int worldX, int worldY)
            {
                SiteId = siteId;
                SiteName = siteName;
                WorldX = worldX;
                WorldY = worldY;
            }
        }

        public static bool TryResolve(Zone zone, out SiteInfo site)
        {
            site = default(SiteInfo);
            if (!(zone is Zone_Civilized))
            {
                return false;
            }

            string siteId = zone?.id;
            if (string.IsNullOrWhiteSpace(siteId))
            {
                SkyreaderGuild.Log($"Skipping comet heat report for {zone?.Name ?? "<null>"} because it has no canonical zone id. source={zone?.source?.id ?? "<null>"}");
                return false;
            }

            Point regionPos = zone.RegionPos;
            if (regionPos == null)
            {
                SkyreaderGuild.Log($"Skipping comet heat report for {zone.Name} ({siteId}) because it has no world-map position.");
                return false;
            }

            site = new SiteInfo(siteId, zone.Name, regionPos.x, regionPos.z);
            return true;
        }
    }
}
