using UnityEngine;

namespace SkyreaderGuild
{
    public class TraitSrgDebris : TraitItem
    {
        // Strictly prevents the engine from allowing the player to 
        // put this in their inventory or auto-pickup.
        public override bool CanBeHeld => false; 
    }
}
