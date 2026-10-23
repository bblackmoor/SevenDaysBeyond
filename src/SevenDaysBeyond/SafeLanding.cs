using System;
using UnityEngine;

namespace SevenDaysBeyond
{
    internal static class SafeLanding
    {
        internal static string Explain(World world, Vector3 centre)
        {
            int x = (int)centre.x, z = (int)centre.z;
            var c = world.GetChunkFromWorldPos(x,z) as Chunk;
            if(c == null) return "chunk missing";
            string text = "chunk init="+c.IsInitialized+" decor="+c.NeedsDecoration+" progress="+c.InProgressDecorating+" light="+c.NeedsLightCalculation;
            int h = world.GetHeight(x,z);
            text += "; height="+h;
            for(int y=Math.Max(1,h-3);y<=Math.Min(253,h+3);y++)
            {
                var b = world.GetBlock(x,y,z);
                text += "; y="+y+" block="+b.Block.GetBlockName()+" density="+world.GetDensity(x,y,z)+" terrain="+b.Block.shape.IsTerrain()+" damage="+b.Block.Damage+" buffs="+(b.Block.BuffsWhenWalkedOn==null?0:b.Block.BuffsWhenWalkedOn.Length)+" water="+world.IsWater(x,y,z);
            }
            return text+"; radiated="+world.IsPositionRadiated(new Vector3(x,h+1,z))+" inBounds="+world.IsPositionInBounds(new Vector3(x,h+1,z));
        }
        // Loaded chunks only. Unknown terrain never counts as air or safe ground.
        internal static bool TryAt(World world, MapBounds bounds, int x, int z, out Vector3 point)
        {
            point = default(Vector3);
            if (x < bounds.MinX + BorderPolicy.Trigger + 10 || x > bounds.MaxX - BorderPolicy.Trigger - 10
                || z < bounds.MinZ + BorderPolicy.Trigger + 10 || z > bounds.MaxZ - BorderPolicy.Trigger - 10) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    {
                        var chunk = world.GetChunkFromWorldPos(x + dx, z + dz) as Chunk;
                        // IsInitialized also waits for lighting, which headless preload observers do not request.
                        if (chunk == null || chunk.NeedsDecoration || chunk.InProgressDecorating
                            || chunk.InProgressRegeneration || chunk.InProgressUnloading) return false;
                    }
            int top = world.GetHeight(x, z);
            for (int y = Math.Min(250, top + 2); y >= Math.Max(2, top - 4); y--)
            {
                BlockValue floor = world.GetBlock(x, y, z);
                Block block = floor.Block;
                if (block == null || !LandingRules.Ground(floor.isair, floor.isWater, block.shape.IsTerrain(), block.Damage,
                    block.BuffsWhenWalkedOn != null && block.BuffsWhenWalkedOn.Length > 0, world.GetDensity(x, y, z))) continue;
                bool safe = true;
                // A 3 x 3 platform, three clear blocks above it, no water, steep drop, or props.
                for (int dx = -1; dx <= 1 && safe; dx++)
                    for (int dz = -1; dz <= 1 && safe; dz++)
                    {
                        var ground = world.GetBlock(x + dx, y, z + dz);
                        if (!LandingRules.Ground(ground.isair, ground.isWater, ground.Block.shape.IsTerrain(), ground.Block.Damage,
                            ground.Block.BuffsWhenWalkedOn != null && ground.Block.BuffsWhenWalkedOn.Length > 0,
                            world.GetDensity(x + dx, y, z + dz))) { safe = false; break; }
                        for (int dy = 1; dy <= 3; dy++)
                            if (!world.GetBlock(x + dx, y + dy, z + dz).isair || world.IsWater(x + dx, y + dy, z + dz))
                            { safe = false; break; }
                    }
                var candidate = new Vector3(x + 0.5f, y + 1.1f, z + 0.5f);
                if (safe && !world.IsPositionRadiated(candidate) && world.IsPositionInBounds(candidate)
                    && !world.IsWithinTraderArea(new Vector3i(candidate))) { point = candidate; return true; }
            }
            return false;
        }
        internal static bool Find(World world, MapBounds bounds, Vector3 centre, out Vector3 point)
        {
            int x = (int)Math.Floor(centre.x), z = (int)Math.Floor(centre.z);
            // Search nearest points first; bounded to 32 m from the intended landing.
            for (int radius = 0; radius <= 32; radius += 4)
                for (int dx = -radius; dx <= radius; dx += 4)
                    for (int dz = -radius; dz <= radius; dz += 4)
                        if ((radius == 0 || Math.Abs(dx) == radius || Math.Abs(dz) == radius)
                            && TryAt(world, bounds, x + dx, z + dz, out point)) return true;
            point = default(Vector3); return false;
        }
    }
}
