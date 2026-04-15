using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyreaderGuild
{
    internal static class SkyreaderHeatmapGraphic
    {
        private const int Width = 384;
        private const int Height = 220;
        private const int PanelInset = 8;
        private const int MapInsetX = 24;
        private const int MapInsetY = 22;
        private const int LegendHeight = 18;

        public static Sprite Create(SkyreaderOnlineClient.HeatmapData data)
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Fill(texture, new Color32(20, 24, 28, 255));
            DrawPanel(texture);

            MapFrame frame;
            if (TryCreateMapFrame(out frame))
            {
                DrawWorldMap(texture, frame);
                DrawSiteMarkers(texture, frame, data?.Sites);
            }
            else
            {
                DrawFallback(texture);
            }

            DrawLegend(texture);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }

        private static bool TryCreateMapFrame(out MapFrame frame)
        {
            frame = default(MapFrame);
            if (EClass.world?.region?.elomap == null || EClass.scene?.elomapActor == null)
            {
                return false;
            }

            EloMap map = EClass.world.region.elomap;
            EClass.scene.elomapActor.Initialize(map);
            if (map.w <= 0 || map.h <= 0)
            {
                return false;
            }

            int mapWidthPixels = Width - (MapInsetX * 2);
            int mapHeightPixels = Height - MapInsetY - 34 - LegendHeight;
            float scale = Math.Min(mapWidthPixels / (float)map.w, mapHeightPixels / (float)map.h);
            if (scale <= 0f)
            {
                return false;
            }

            int drawWidth = Math.Max(1, Mathf.RoundToInt(map.w * scale));
            int drawHeight = Math.Max(1, Mathf.RoundToInt(map.h * scale));

            frame = new MapFrame(
                map,
                MapInsetX + (mapWidthPixels - drawWidth) / 2,
                MapInsetY + (mapHeightPixels - drawHeight) / 2,
                drawWidth,
                drawHeight,
                scale
            );
            return true;
        }

        private static void DrawPanel(Texture2D texture)
        {
            DrawRect(texture, PanelInset, PanelInset, Width - (PanelInset * 2), Height - (PanelInset * 2), new Color32(48, 50, 46, 255));
            DrawRect(texture, 12, 12, Width - 24, Height - 24, new Color32(62, 61, 54, 255));
            DrawRect(texture, 18, 18, Width - 36, Height - 36, new Color32(86, 82, 68, 255));
            DrawRect(texture, 22, 22, Width - 44, Height - 44, new Color32(24, 31, 34, 255));
        }

        private static void DrawWorldMap(Texture2D texture, MapFrame frame)
        {
            DrawRect(texture, frame.DrawX - 1, frame.DrawY - 1, frame.DrawWidth + 2, frame.DrawHeight + 2, new Color32(104, 97, 76, 255));
            DrawRect(texture, frame.DrawX, frame.DrawY, frame.DrawWidth, frame.DrawHeight, new Color32(14, 18, 21, 255));

            for (int mapY = 0; mapY < frame.Map.h; mapY++)
            {
                for (int mapX = 0; mapX < frame.Map.w; mapX++)
                {
                    int worldX = frame.Map.minX + mapX;
                    int worldY = frame.Map.minY + mapY;
                    EloMap.TileInfo tile = frame.Map.GetTileInfo(worldX, worldY);
                    EloMap.Cell cell = frame.Map.GetCell(worldX, worldY);
                    DrawCell(texture, frame, mapX, mapY, ColorForTile(tile, cell?.zone != null));
                }
            }
        }

        private static void DrawFallback(Texture2D texture)
        {
            DrawRect(texture, MapInsetX, MapInsetY, Width - (MapInsetX * 2), Height - MapInsetY - 34 - LegendHeight, new Color32(28, 36, 40, 255));
            DrawRect(texture, MapInsetX + 1, MapInsetY + 1, Width - (MapInsetX * 2) - 2, Height - MapInsetY - 36 - LegendHeight, new Color32(48, 60, 66, 255));
        }

        private static void DrawSiteMarkers(Texture2D texture, MapFrame frame, List<SkyreaderOnlineClient.SiteHeat> sites)
        {
            if (sites == null || sites.Count == 0)
            {
                return;
            }

            var grouped = new Dictionary<Vector2Int, MarkerAggregate>();
            foreach (SkyreaderOnlineClient.SiteHeat site in sites)
            {
                if (site == null)
                {
                    continue;
                }

                var key = new Vector2Int(site.WorldX, site.WorldY);
                MarkerAggregate aggregate;
                if (!grouped.TryGetValue(key, out aggregate))
                {
                    aggregate = new MarkerAggregate(site);
                }
                else
                {
                    aggregate.Add(site);
                }
                grouped[key] = aggregate;
            }

            foreach (KeyValuePair<Vector2Int, MarkerAggregate> pair in grouped)
            {
                if (!TryGetCellBounds(frame, pair.Key.x, pair.Key.y, out CellBounds bounds))
                {
                    continue;
                }

                Color32 markerColor = ColorForStatus(pair.Value.WorstStatus);
                int radius = Mathf.Clamp(3 + pair.Value.Count, 4, 7);
                DrawDiamond(texture, bounds.CenterX, bounds.CenterY, radius, markerColor);
                DrawDiamondOutline(texture, bounds.CenterX, bounds.CenterY, radius + 1, new Color32(246, 236, 196, 255));
            }
        }

        private static Color32 ColorForTile(EloMap.TileInfo tile, bool hasSite)
        {
            if (tile == null || tile.source == null)
            {
                return new Color32(18, 24, 27, 255);
            }

            string alias = tile.source.alias ?? string.Empty;
            if (tile.sea || alias == "sea" || alias == "underseas")
            {
                return new Color32(40, 78, 124, 255);
            }
            if (tile.shore || alias == "beach" || alias == "bridge")
            {
                return new Color32(170, 152, 93, 255);
            }
            if (tile.IsSnow || alias == "snow" || alias == "snow_edge")
            {
                return new Color32(198, 212, 224, 255);
            }
            if (tile.rock || alias == "mountain" || alias == "cliff" || alias == "wall")
            {
                return new Color32(106, 96, 104, 255);
            }
            if (alias == "forest" || alias == "forest_cherry")
            {
                return new Color32(56, 108, 72, 255);
            }
            if (alias == "road")
            {
                return new Color32(136, 118, 76, 255);
            }

            Color32 baseColor = new Color32(82, 126, 72, 255);
            if (hasSite)
            {
                baseColor = new Color32(104, 150, 86, 255);
            }
            return baseColor;
        }

        private static Color32 ColorForStatus(string status)
        {
            switch (status)
            {
                case "Calm": return new Color32(79, 166, 95, 240);
                case "Stirring": return new Color32(210, 184, 76, 240);
                case "Troubled": return new Color32(213, 112, 56, 240);
                case "Overrun": return new Color32(190, 57, 57, 240);
                default: return new Color32(180, 180, 180, 240);
            }
        }

        private static void DrawLegend(Texture2D texture)
        {
            int y = Height - 27;
            DrawRect(texture, 38, y, 46, 8, new Color32(79, 166, 95, 255));
            DrawRect(texture, 96, y, 46, 8, new Color32(210, 184, 76, 255));
            DrawRect(texture, 154, y, 46, 8, new Color32(213, 112, 56, 255));
            DrawRect(texture, 212, y, 46, 8, new Color32(190, 57, 57, 255));
        }

        private static void DrawCell(Texture2D texture, MapFrame frame, int mapX, int mapY, Color32 color)
        {
            CellBounds bounds;
            if (!TryGetCellBounds(frame, mapX, mapY, out bounds))
            {
                return;
            }

            DrawRect(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);
        }

        private static bool TryGetCellBounds(MapFrame frame, int mapX, int mapY, out CellBounds bounds)
        {
            bounds = default(CellBounds);
            if (mapX < 0 || mapY < 0 || mapX >= frame.Map.w || mapY >= frame.Map.h)
            {
                return false;
            }

            int drawMapY = frame.Map.h - mapY - 1;
            int x0 = frame.DrawX + Mathf.FloorToInt(mapX * frame.Scale);
            int x1 = frame.DrawX + Mathf.FloorToInt((mapX + 1) * frame.Scale);
            int y0 = frame.DrawY + Mathf.FloorToInt(drawMapY * frame.Scale);
            int y1 = frame.DrawY + Mathf.FloorToInt((drawMapY + 1) * frame.Scale);

            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;

            bounds = new CellBounds(x0, y0, x1 - x0, y1 - y0);
            return true;
        }

        private static void Fill(Texture2D texture, Color32 color)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawRect(Texture2D texture, int x, int y, int w, int h, Color32 color)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
            {
                for (int xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }

        private static void DrawDiamond(Texture2D texture, int cx, int cy, int radius, Color32 color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int span = radius - Math.Abs(dy);
                for (int dx = -span; dx <= span; dx++)
                {
                    SetPixel(texture, cx + dx, cy + dy, color);
                }
            }
        }

        private static void DrawDiamondOutline(Texture2D texture, int cx, int cy, int radius, Color32 color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int span = radius - Math.Abs(dy);
                SetPixel(texture, cx - span, cy + dy, color);
                SetPixel(texture, cx + span, cy + dy, color);
            }
        }

        private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }
            texture.SetPixel(x, y, color);
        }

        private readonly struct MapFrame
        {
            public readonly EloMap Map;
            public readonly int DrawX;
            public readonly int DrawY;
            public readonly int DrawWidth;
            public readonly int DrawHeight;
            public readonly float Scale;

            public MapFrame(EloMap map, int drawX, int drawY, int drawWidth, int drawHeight, float scale)
            {
                Map = map;
                DrawX = drawX;
                DrawY = drawY;
                DrawWidth = drawWidth;
                DrawHeight = drawHeight;
                Scale = scale;
            }
        }

        private readonly struct CellBounds
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;
            public readonly int CenterX;
            public readonly int CenterY;

            public CellBounds(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                CenterX = x + (width / 2);
                CenterY = y + (height / 2);
            }
        }

        private struct MarkerAggregate
        {
            public string WorstStatus;
            public double WorstRatio;
            public int Count;

            public MarkerAggregate(SkyreaderOnlineClient.SiteHeat site)
            {
                WorstStatus = site.Status;
                WorstRatio = site.Ratio;
                Count = 1;
            }

            public void Add(SkyreaderOnlineClient.SiteHeat site)
            {
                Count++;
                if (site.Ratio < WorstRatio)
                {
                    WorstRatio = site.Ratio;
                    WorstStatus = site.Status;
                }
            }
        }
    }
}
